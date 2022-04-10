using System.Timers;

namespace Irene.Components;

using SelectionCallback = Func<ComponentInteractionCreateEventArgs, Task>;
using ComponentRow = DiscordActionRowComponent;
using Component = DiscordComponent;
using SelectOption = DiscordSelectComponentOption;

class Selection {
	public readonly record struct Option (
		string Label,
		string Id,
		DiscordComponentEmoji? Emoji,
		string? Description
	);

	public static TimeSpan DefaultTimeout { get => TimeSpan.FromMinutes(10); }

	// Table of all Selections to handle, indexed by the message ID of
	// the owning message.
	// This also serves as a way to "hold" fired timers, preventing them
	// from going out of scope and being destroyed.
	private static readonly ConcurrentDictionary<ulong, Selection> _selections = new ();
	private const string _id = "selection";
	
	// Force static initializer to run.
	public static void Init() { return; }
	// All events are handled by a single delegate, registered on init.
	// This means there doesn't need to be a large amount of delegates
	// that each event has to filter through until it hits the correct
	// handler.
	static Selection() {
		Client.ComponentInteractionCreated += async (client, e) => {
			ulong id = e.Message.Id;

			// Consume all interactions originating from a registered
			// message, and created by the corresponding component.
			if (_selections.ContainsKey(id) && e.Id == _id) {
				e.Handled = true;
				Selection selection = _selections[id];

				// Only respond to interactions created by the "owner"
				// of the component.
				if (e.User != selection._interaction.User) {
					await e.Interaction.AcknowledgeComponentAsync();
					return;
				}
				
				selection._timer.Restart();
				await selection._callback(e);
				return;
			}
		};
	}

	public DiscordSelectComponent Component { get; private set; }

	// Instanced (configuration) properties.
	private DiscordMessage? _message;
	private readonly DiscordInteraction _interaction;
	private readonly Timer _timer;
	private readonly SelectionCallback _callback;

	// Private constructor.
	// Use Selection.Create() to create a new instance.
	private Selection(
		DiscordSelectComponent component,
		DiscordInteraction interaction,
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
		const double delay = 0.1;
		_timer.Stop();
		_timer.Interval = delay;	// arbitrarily small interval, must be >0
		_timer.Start();
		await Task.Delay(TimeSpan.FromMilliseconds(delay));
	}

	// Update the selected entries of the select component.
	public async Task Update(List<Option> selected) {
		// Can only update if message was already created.
		if (_message is null)
			return;

		// Update component by constructing a new DiscordMessage
		// from the data of the old one.
		// Interaction responses behave as webhooks and need to be
		// constructed as such.
		DiscordWebhookBuilder message =
			new DiscordWebhookBuilder()
				.WithContent(_message.Content);
		List<ComponentRow> rows =
			UpdateSelect(new (_message.Components), selected);
		message.AddComponents(rows);

		// Edit original message.
		// This must be done through the original interaction, as
		// responses to interactions don't actually "exist" as real
		// messages.
		await _interaction
			.EditOriginalResponseAsync(message);
	}

	public static Selection Create<T>(
		DiscordInteraction interaction,
		SelectionCallback callback,
		Task<DiscordMessage> messageTask,
		IDictionary<T, Option> optionTable,
		List<T> selected,
		string placeholder,
		bool isMultiple,
		TimeSpan? timeout=null
	) where T : Enum {
		timeout ??= DefaultTimeout;
		Timer timer = new (timeout.Value.TotalMilliseconds) {
			AutoReset = false,
		};

		// Construct select component options.
		List<SelectOption> options = new ();
		foreach (T key in optionTable.Keys) {
			Option option = optionTable[key];
			SelectOption option_discord = new (
				option.Label,
				option.Id,
				option.Description,
				selected.Contains(key),
				option.Emoji
			);
			options.Add(option_discord);
		}

		// Construct select component.
		DiscordSelectComponent component = new (
			_id,
			placeholder,
			options,
			disabled: false,
			minOptions: isMultiple ? 0 : 1,
			maxOptions: isMultiple ? options.Count : 1
		);

		// Construct partial Selection object.
		Selection selection =
			new (component, interaction, timer, callback);
		messageTask.ContinueWith((messageTask) => {
			DiscordMessage message = messageTask.Result;
			selection._message = message;
			_selections.TryAdd(message.Id, selection);
			selection._timer.Start();
		});
		timer.Elapsed += async (obj, e) => {
			async Task cleanup(Task<DiscordMessage> e) {
				if (selection._message is null)
					return;
				DiscordMessage message = selection._message;

				// Remove held references.
				_selections.TryRemove(message.Id, out _);

				// Update message to disable component, constructing a new
				// DiscordMessage from the data of the old one.
				// Interaction responses behave as webhooks and need to be
				// constructed as such.
				DiscordWebhookBuilder message_new =
					new DiscordWebhookBuilder()
						.WithContent(message.Content);
				List<ComponentRow> rows =
					DisableSelect(new (message.Components));
				message_new.AddComponents(rows);

				// Edit original message.
				// This must be done through the original interaction, as
				// responses to interactions don't actually "exist" as real
				// messages.
				await selection._interaction
					.EditOriginalResponseAsync(message_new);
			}

			// Run or schedule to run the above.
			if (!messageTask.IsCompleted)
				await messageTask.ContinueWith(cleanup);
			else
				await cleanup(messageTask);
		};

		return selection;
	}

	// Return a new list of components, with any DiscordSelectComponents
	// (with a matching ID) disabled.
	private static List<ComponentRow> DisableSelect(List<ComponentRow> rows) {
		List<ComponentRow> rows_new = new ();

		foreach (ComponentRow row in rows) {
			List<Component> components_new = new ();

			foreach (Component component in row.Components) {
				if (component is
					DiscordSelectComponent select &&
					component.CustomId == _id
				) {
					select.Disable();
					components_new.Add(select);
				} else {
					components_new.Add(component);
				}
			}

			rows_new.Add(new ComponentRow(components_new));
		}

		return rows_new;
	}

	// Return a new list of components, with any DiscordSelectComponents
	// (with a matching ID) updated as selected.
	private static List<ComponentRow> UpdateSelect(
		List<ComponentRow> rows,
		List<Option> selected
	) {
		List<ComponentRow> rows_new = new ();

		foreach (ComponentRow row in rows) {
			List<Component> components_new = new ();

			foreach (Component component in row.Components) {
				if (component is
					DiscordSelectComponent select &&
					component.CustomId == _id
				) {
					List<SelectOption> options_new = new ();
					foreach (SelectOption option in select.Options) {
						bool isSelected = selected.Exists(
							(option_i) => option_i.Id == option.Value
						);
						SelectOption option_new = new (
							option.Label,
							option.Value,
							option.Description,
							isSelected,
							option.Emoji
						);
						options_new.Add(option_new);
					}
					DiscordSelectComponent select_new = new (
						select.CustomId,
						select.Placeholder,
						options_new,
						select.Disabled,
						select.MinimumSelectedValues ?? 1,
						select.MaximumSelectedValues ?? 1
					);
					components_new.Add(select_new);
				} else {
					components_new.Add(component);
				}
			}

			rows_new.Add(new ComponentRow(components_new));
		}

		return rows_new;
	}
}
