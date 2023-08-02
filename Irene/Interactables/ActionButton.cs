namespace Irene.Interactables;

using System.Diagnostics.CodeAnalysis;
using System.Timers;

// `ActionButtonOptions` can be individually set and passed to the static
// `ActionButton` factory constructor. Any unspecified options default
// to the specified values.
class ActionButtonOptions {
	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

	// If this is true, the button filters out all responses that aren't
	// from the original owner of the containing message. The generic
	// response for interacting with someone else's component will be
	// shown instead.
	public bool IsOwnerOnly { get; init; } = true;

	// Whether or not the select component is enabled.
	public bool IsEnabled { get; init; } = true;

	// The style (color) of the button.
	public ButtonStyle ButtonStyle { get; init; } = ButtonStyle.Secondary;

	// The duration each `ActionButton` lasts before being automatically
	// disabled. Ephemeral responses MUST have a timeout less than
	// Discord's limit of 15 mins/interaction--past that the message
	// itself cannot be updated anymore.
	public TimeSpan Timeout { get; init; } = DefaultTimeout;
}

class ActionButton {
	// A unique identifier for each `ActionButton`, consisting of the
	// message that button is attached to, and what button it is.
	public readonly record struct Id(ulong MessageId, string CustomId);

	// A delegate to be called after the button is clicked.
	// The callback is passed the interaction created from that click,
	// and must handle responding to the interaction (e.g. deciding to
	// defer or not).
	public delegate Task Callback(Interaction interaction);


	// --------
	// Constants and static properties:
	// --------

	// Master table of all `ActionButton`s being tracked, indexed by
	// the message ID of the containing message, and the custom ID of
	// the button.
	// This also serves as a way to hold fired timers, preventing them
	// from going out of scope and being destroyed prematurely.
	private static readonly ConcurrentDictionary<Id, ActionButton> _buttons = new ();

	// All events are handled by a single delegate, registered on init.
	// This means each event doesn't have to filter through all handlers
	// of the same type until it hits the right one.
	static ActionButton() {
		CheckErythroInit();
		Erythro.Client.ComponentInteractionCreated +=
			InteractionDispatcherAsync;
	}


	// --------
	// Instance properties and fields:
	// --------

	// Public properties.
	public bool IsEnabled { get; private set; }
	[MemberNotNullWhen(true, nameof(_message))]
	public bool HasMessage => _message is not null;
	public DiscordUser Owner => _interaction.User;
	public string CustomId { get; private set; }

	// This event is raised both when the interactable auto-times out,
	// and also when `Discard()` is manually called.
	public event EventHandler? InteractableDiscarded;
	// Wrapper method to allow derived classes to invoke this event.
	protected virtual void OnInteractableDiscarded() =>
		InteractableDiscarded?.Invoke(this, new ());

	// Private fields.
	private readonly TaskQueue _queueUpdates = new ();
	private readonly Interaction _interaction;
	private DiscordMessage? _message = null;
	private readonly Timer _timer;
	private readonly bool _isOwnerOnly;
	private readonly ButtonStyle _buttonStyle;
	private readonly string? _label;
	private readonly DiscordComponentEmoji? _emoji;

	protected Callback _callback;


	// --------
	// Factory method and constructor:
	// --------

	// The interactable is registered to the table of `ActionButton`s
	// (and the auto-discard timer starts running) only when the message
	// promise is fulfilled.
	// At least one of `label` or `emoji` must be non-null.
	public static ActionButton Create(
		Interaction interaction,
		MessagePromise promise,
		Callback callback,
		string customId,
		string? label,
		DiscordComponentEmoji? emoji,
		ActionButtonOptions? options=null
	) {
		options ??= new ();

		// Construct partial (uninitialized) object.
		ActionButton button = new (
			interaction,
			callback,
			customId,
			label,
			emoji,
			options
		);

		// Set up registration and auto-discard.
		button.FinalizeInstance(promise);

		return button;
	}

	// Since the protected constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	protected ActionButton(
		Interaction interaction,
		Callback callback,
		string customId,
		string? label,
		DiscordComponentEmoji? emoji,
		ActionButtonOptions options
	) {
		IsEnabled = options.IsEnabled;
		CustomId = customId;

		_interaction = interaction;
		_timer = Util.CreateTimer(options.Timeout, false);
		_isOwnerOnly = options.IsOwnerOnly;
		_callback = callback;
		_buttonStyle = options.ButtonStyle;
		_label = label;
		_emoji = emoji;
	}

	// The entire `ActionButton` object cannot be constructed in one
	// stage; this second stage registers the object after the message
	// promise is fulfilled, and sets up auto-discard.
	protected void FinalizeInstance(MessagePromise promise) {
		Task<DiscordMessage> promiseTask = promise.Task;

		// Register instance.
		promiseTask.ContinueWith(t => {
			DiscordMessage message = t.Result;
			_message = message;
			_buttons[new (message.Id, CustomId)] = this;
			_timer.Start();
		});

		// Run (or schedule to run) auto-discard.
		_timer.Elapsed += async (_, _) => {
			if (!promiseTask.IsCompleted)
				await promiseTask.ContinueWith(e => Cleanup());
			else
				await Cleanup();
		};
	}


	// --------
	// Public methods:
	// --------

	// Constructs the current `DiscordButton` component from internal
	// state.
	// The "enabled" state could change on every call, so having this
	// constructed every time is the most functional way to do it.
	public DiscordButton GetButton() => new (
		_buttonStyle,
		CustomId,
		_label,
		disabled: !IsEnabled,
		emoji: _emoji
	);

	// Enable/disable the button component.
	public Task Enable() {
		IsEnabled = true;
		return Update();
	}
	public Task Disable() {
		IsEnabled = false;
		return Update();
	}

	// Trigger the auto-discard by manually timing-out the timer.
	public async Task Discard() {
		_timer.Stop();

		// Set the timeout to an arbitrarily small interval (`Timer`
		// disallows setting to 0), triggering the auto-discard.
		const double delta = 0.1;
		_timer.Interval = delta;

		_timer.Start();
		await Task.Delay(TimeSpan.FromMilliseconds(delta));
	}


	// --------
	// Private helper methods:
	// --------

	// Assumes `_message` has been set; returns immediately if it hasn't.
	protected virtual Task Update() =>
		_queueUpdates.Run(new Task<Task>(async () => {
			CheckErythroInit();
			if (!HasMessage)
				return;

			// Re-fetch message.
			_message = await Util.RefetchMessage(Erythro.Client, _message);

			// Rebuild message with button component updated.
			await _interaction.EditResponseAsync(ReplaceButton(_message));
		}));

	// Assumes `_message` has been set; returns immediately if it hasn't.
	protected virtual async Task Cleanup() {
		if (!HasMessage)
			return;

		// Remove held references.
		_buttons.TryRemove(new (_message.Id, CustomId), out _);

		await Disable();

		// Raise discard event.
		OnInteractableDiscarded();

		Log.Debug("Cleaned up ActionButton interactable.");
		Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
		Log.Debug("  Message ID: {MessageId}", _message.Id);
		Log.Debug("  Button ID: {CustomId}", CustomId);
	}

	// Iterate through all components on the message, and only replace
	// the one with a matching `CustomId` to the current instance.
	private IDiscordMessageBuilder ReplaceButton(DiscordMessage message) {
		DiscordMessageBuilder replacement = new (message);
		replacement.ClearComponents();

		foreach (DiscordComponentRow row in message.Components) {
			List<DiscordComponent> components = new ();
			foreach (DiscordComponent component in row.Components) {
				if ((component is DiscordButton) &&
					(component.CustomId == CustomId)
				) {
					components.Add(GetButton());
				} else {
					components.Add(component);
				}
			}
			replacement.AddComponents(components);
		}

		return replacement;
	}

	// Invoke the callback, passing it the interaction which triggered
	// it. The callback must handle responding to the interaction, e.g.
	// deciding to defer the response or not.
	private Task ActivateButton(Interaction interaction) =>
		_callback.Invoke(interaction);

	// Filter and dispatch any interactions to be properly handled.
	// This delegate handles dispatch for all derived classes as well,
	// and so doesn't need to be overridden.
	private static async Task InteractionDispatcherAsync(
		DiscordClient c,
		ComponentInteractionCreateEventArgs e
	) {
		Id id = new (e.Message.Id, e.Id);

		// Consume all interactions originating from a registered
		// message, and created by the corresponding component.
		if (_buttons.TryGetValue(id, out ActionButton? button)) {
			// Can only update if message was already created.
			if (!button.HasMessage)
				return;

			// Only respond to interactions created by the owner
			// of the interactable.
			Interaction interaction = Interaction.FromComponent(e);
			if (button._isOwnerOnly && e.User != button.Owner) {
				await interaction.RespondComponentNotOwned(button.Owner);
				return;
			}

			// Handle button. Passing the interaction itself lets
			// the callback decide how to respond (to defer or not).
			await button.HandleButtonAsync(interaction);
		}
	}

	// Handle any button presses for this interactable. The passed in
	// `Interaction` allows for button press-initiated message edits
	// to work reliably even if the message itself has timed out.
	// This should behave the same for all derived classes, and therefore
	// doesn't need to be overridden.
	private Task HandleButtonAsync(Interaction interaction) =>
		_queueUpdates.Run(new Task<Task>(async () => {
			// Let the button invoke the callback.
			// The callback decides how to respond (to defer or not).
			await ActivateButton(interaction);
		}));
}
