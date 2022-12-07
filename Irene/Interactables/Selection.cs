namespace Irene.Interactables;

using System.Timers;

using SelectionCallback = Func<ComponentInteractionCreateEventArgs, Task>;
using ComponentCallback = Func<DiscordSelect, DiscordSelect>;

class Selection {
	public readonly record struct SelectionId (
		ulong MessageId,
		string ComponentId
	);
	public readonly record struct Option (
		string Label,
		string Id,
		DiscordComponentEmoji? Emoji,
		string? Description
	);

	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

	// Master table of all Selections to handle, indexed by the message
	// ID of the owning message + selection custom ID.
	// This also serves as a way to "hold" fired timers, preventing them
	// from prematurely going out of scope and being destroyed.
	private static readonly ConcurrentDictionary<SelectionId, Selection> _selections = new ();
	
	// All events are handled by a single delegate, registered on init.
	// This means there doesn't need to be a large amount of delegates
	// that each event has to filter through until it hits the correct
	// handler.
	static Selection() {
		CheckErythroInit();

		Erythro.Client.ComponentInteractionCreated += (c, e) => {
			_ = Task.Run(async () => {
				SelectionId id = new (e.Message.Id, e.Id);

				// Consume all interactions originating from a registered
				// message, and created by the corresponding component.
				if (_selections.ContainsKey(id)) {
					Selection selection = _selections[id];
					if (selection.Id != e.Id)
						return;
					e.Handled = true;

					// Can only update if message was already created.
					if (selection._message is null)
						return;

					// Only respond to interactions created by the owner
					// of the interactable.
					if (e.User != selection._interaction.User)
						return;

					// Acknowledge interaction and update the original
					// message later (inside the callback itself).
					Interaction interaction = Interaction.FromComponent(e);
					await interaction.DeferComponentAsync();

					// Execute callback and update original message.
					await selection._callback(e);
				}
			});
			return Task.CompletedTask;
		};
	}

	// Instance properties.
	public DiscordSelect Component { get; private set; }
	public string Id => Component.CustomId;
	private readonly Interaction _interaction;
	private DiscordMessage? _message = null;
	private readonly Timer _timer;
	private readonly SelectionCallback _callback;

	// Public factory method constructor.
	// Use this method to instantiate a new interactable.
	// NOTE: The selection callback should probably call `Update()`.
	public static Selection Create<T>(
		Interaction interaction,
		Task<DiscordMessage> messageTask,
		SelectionCallback callback,
		string id,
		IReadOnlyList<(T, Option)> options,
		IReadOnlySet<T> selected,
		bool isMultiple,
		string? placeholder=null,
		TimeSpan? timeout=null
	) where T : Enum {
		placeholder ??= "";
		timeout ??= DefaultTimeout;
		Timer timer = Util.CreateTimer(timeout.Value, false);

		// Construct select component options.
		List<DiscordSelectOption> options_obj = new ();
		foreach ((T Key, Option Value) option in options) {
			Option value = option.Value;
			DiscordSelectOption optionObj = new (
				value.Label,
				value.Id,
				value.Description,
				selected.Contains(option.Key),
				value.Emoji
			);
			options_obj.Add(optionObj);
		}

		// Construct select component.
		DiscordSelect component = new (
			id,
			placeholder,
			options_obj,
			disabled: false,
			minOptions: isMultiple ? 0 : 1,
			maxOptions: isMultiple ? options.Count : 1
		);

		// Construct partial Selection object.
		Selection selection = new (interaction, component, timer, callback);
		messageTask.ContinueWith((messageTask) => {
			DiscordMessage message = messageTask.Result;
			selection._message = message;
			_selections.TryAdd(new (message.Id, id), selection);
			selection._timer.Start();
		});
		timer.Elapsed += async (obj, e) => {
			// Run (or schedule to run) cleanup task.
			if (!messageTask.IsCompleted)
				await messageTask.ContinueWith((e) => selection.Cleanup());
			else
				await selection.Cleanup();
		};

		return selection;
	}
	private Selection(
		Interaction interaction,
		DiscordSelect component,
		Timer timer,
		SelectionCallback callback
	) {
		Component = component;
		_interaction = interaction;
		_timer = timer;
		_callback = callback;
	}

	// Manually time-out the timer (and fire the elapsed handler).
	public async Task Discard() {
		_timer.Stop();
		const double delay = 0.1;
		_timer.Interval = delay; // arbitrarily small interval, must be >0
		_timer.Start();
		await Task.Delay(TimeSpan.FromMilliseconds(delay));
	}
	// Update the selected entries of the select component.
	// Assumes _message has been set; returns immediately if it hasn't.
	public async Task Update(IReadOnlySet<string> selected) {
		CheckErythroInit();
		if (_message is null)
			return;

		// Cancel operation if interactable has been disabled.
		if (!_selections.ContainsKey(new (_message.Id, Id)))
			return;

		// Re-fetch message.
		_message = await Util.RefetchMessage(Erythro.Client, _message);

		// Rebuild message with select component updated.
		await _interaction.EditResponseAsync(GetUpdatedSelect(_message, selected));
	}

	// Cleanup task to dispose of all resources.
	// Assumes _message has been set; returns immediately if it hasn't.
	private async Task Cleanup() {
		CheckErythroInit();
		if (_message is null)
			return;

		// Remove held references.
		_selections.TryRemove(new (_message.Id, Id), out _);

		// Re-fetch message.
		_message = await Util.RefetchMessage(Erythro.Client, _message);

		// Rebuild message with select component disabled.
		await _interaction.EditResponseAsync(GetDisabledSelect(_message));

		Log.Debug("Cleaned up Selection interactable.");
		Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
		Log.Debug("  Message ID: {MessageId}", _message.Id);
	}

	// Modify an existing message object to return a webhook builder.
	// NOTE: Only message content, embeds, and components are preserved.
	private DiscordWebhookBuilder GetDisabledSelect(DiscordMessage original) {
		List<DiscordComponentRow> components = ModifyComponent(
			original,
			(select) => select.Disable()
		);
		return CloneMessageToWebhook(original, components);
	}
	private DiscordWebhookBuilder GetUpdatedSelect(
		DiscordMessage original,
		IReadOnlySet<string> selected
	) {
		List<DiscordComponentRow> components = ModifyComponent(
			original,
			(select) => UpdateSelect(select, selected)
		);
		return CloneMessageToWebhook(original, components);
	}

	// Copy a DiscordMessage to a DiscordWebhookBuilder, but with replaced
	// components.
	// NOTE: Only message content, embeds, and components are preserved.
	private static DiscordWebhookBuilder CloneMessageToWebhook(
		DiscordMessage original,
		IReadOnlyList<DiscordComponentRow> components
	) {
		DiscordWebhookBuilder output =
			new DiscordWebhookBuilder()
			.WithContent(original.Content)
			.AddEmbeds(original.Embeds)
			.AddComponents(components);
		return output;
	}

	// Iterate through all components, and modify the current instance's
	// corresponding select component according to the callback.
	private List<DiscordComponentRow> ModifyComponent(
		DiscordMessage message,
		ComponentCallback callback
	) {
		List<DiscordComponentRow> rows = new ();
		foreach (DiscordComponentRow row in message.Components) {
			List<DiscordComponent> components = new ();
			foreach (DiscordComponent component in row.Components) {
				if ((component is DiscordSelect select) &&
					(component.CustomId == Id)
				) {
					select = callback.Invoke(select);
					components.Add(select);
				} else {
					components.Add(component);
				}
			}
			rows.Add(new (components));
		}
		return rows;
	}

	// Updating a select component with a new set of options selected.
	// Nothing is validated.
	private static DiscordSelect UpdateSelect(
		DiscordSelect select,
		IReadOnlySet<string> selected
	) {
		// Create a list of options with updated "selected" state.
		List<DiscordSelectOption> options = new ();
		foreach (DiscordSelectOption option in select.Options) {
			// Check that the option is selected.
			bool isSelected = selected.Contains(option.Value);
			// Construct a new option, copied from the original, but
			// with the appropriate "selected" state.
			DiscordSelectOption option_new = new (
				option.Label,
				option.Value,
				option.Description,
				isSelected,
				option.Emoji
			);
			options.Add(option_new);
		}

		// Construct new select component with the updated options.
		return new DiscordSelect(
			select.CustomId,
			select.Placeholder,
			options,
			select.Disabled,
			select.MinimumSelectedValues ?? 1,
			select.MaximumSelectedValues ?? 1
		);
	}
}
