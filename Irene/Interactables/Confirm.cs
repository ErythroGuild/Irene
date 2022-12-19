namespace Irene.Interactables;

using System.Diagnostics.CodeAnalysis;
using System.Timers;

// `ConfirmOptions` can be individually set and passed to the static
// `Confirm` factory constructor. Any unspecified options default to
// the specified values.
class ConfirmOptions {
	public static TimeSpan DefaultTimeout => TimeSpan.FromSeconds(90);
	public const string
		DefaultPrompt   = "Are you sure you want to proceed?",
		DefaultLabelYes = "Confirm",
		DefaultLabelNo  = "Cancel",
		DefaultReplyYes = "Request confirmed.",
		DefaultReplyNo  = "Request canceled. No changes made.";
	public const ButtonStyle DefaultButtonYesStyle = ButtonStyle.Danger;

	// Whether or not to show a message after the `Confirm` is clicked.
	// If false, the followup containing the `Confirm` is deleted.
	public bool DoPersist { get; init; } = false;

	// The duration each `Confirm` lasts before being defaulting to
	// being canceled.
	public TimeSpan Timeout { get; init; } = DefaultTimeout;

	// The text prompt given on the initial followup response.
	public string Prompt { get; init; } = DefaultPrompt;

	// The label text on each button.
	public string LabelYes { get; init; } = DefaultLabelYes;
	public string LabelNo  { get; init; } = DefaultLabelNo ;

	// The reply text edited in after a button is clicked.
	public string ReplyYes { get; init; } = DefaultReplyYes;
	public string ReplyNo  { get; init; } = DefaultReplyNo ;

	// The color of the "confirm" option button. (The "cancel" button
	// is always gray.)
	public ButtonStyle ButtonYesStyle { get; init; } = DefaultButtonYesStyle;
}

class Confirm {
	// A delegate to be called after the user makes a choice. The passed
	// parameter is false if the interactable timed out.
	public delegate Task Callback(bool isConfirmed);


	// --------
	// Constants and static properties:
	// --------

	// Master table of all `Confirm`s being tracked, indexed by the
	// message ID of the containing followup message.
	// This also serves as a way to hold fired timers, preventing them
	// from going out of scope and being destroyed prematurely.
	private static readonly ConcurrentDictionary<ulong, Confirm> _confirms = new ();
	private const string
		_idButtonYes = "confirm_yes",
		_idButtonNo  = "confirm_no" ;

	// All events are handled by a single delegate, registered on init.
	// This means each event doesn't have to filter through all handlers
	// of the same type until it hits the right one.
	static Confirm() {
		CheckErythroInit();
		Erythro.Client.ComponentInteractionCreated +=
			InteractionDispatchAsync;
	}


	// --------
	// Instance properties and fields:
	// --------

	// Public properties.
	[MemberNotNullWhen(true, nameof(_message))]
	public bool HasMessage => _message is not null;
	public DiscordUser Owner => _interaction.User;
	
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
	private readonly Callback _callback;
	private readonly bool _doPersist;
	private readonly DiscordFollowupMessageBuilder _prompt;
	private readonly string _replyYes, _replyNo;
	private bool _isProcessed = false;


	// --------
	// Factory method and constructor:
	// --------

	// The interactable is registered to the table of `Confirm`s (and
	// the auto-discard timer starts running) only when `Prompt()` is
	// called.
	public static Confirm Create(
		Interaction interaction,
		Callback callback,
		ConfirmOptions? options=null
	) {
		options ??= new ();

		// Construct partial Confirm object.
		Confirm confirm = new (interaction, callback, options);

		// Set up auto-discard.
		confirm.FinalizeInstance();

		return confirm;
	}

	// Since this private constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	private Confirm(
		Interaction interaction,
		Callback callback,
		ConfirmOptions options
	) {
		_interaction = interaction;
		_timer = Util.CreateTimer(options.Timeout, false);
		_callback = callback;
		_doPersist = options.DoPersist;

		DiscordComponent[] buttons = GetButtons(
			options.LabelYes,
			options.LabelNo,
			options.ButtonYesStyle
		);
		_prompt =
			new DiscordFollowupMessageBuilder()
			.WithContent(options.Prompt)
			.AddComponents(buttons);
		
		_replyYes = options.ReplyYes;
		_replyNo  = options.ReplyNo ;
	}

	// The entire `Confirm` object cannot be constructed in one stage;
	// this second stage sets up auto-discard.
	// NOTE: The timer is not started here because `Confirm` encapsulates
	// the entire response process, and controls the entire lifetime of
	// its own object.
	private void FinalizeInstance() {
		// Run (or schedule to run) auto-discard.
		_timer.Elapsed += async (_, _) => await Cleanup();
	}


	// --------
	// Public methods:
	// --------

	// Create and send followup message with the confirmation prompt.
	// This also registers the `Confirm` to the button handler (since
	// the buttons only start existing after this), and starts the auto-
	// discard timer.
	public async Task Prompt() {
		_message = await _interaction.FollowupAsync(_prompt, true);
		_confirms.TryAdd(_message.Id, this);
		_timer.Start();
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
	private async Task Cleanup() {
		if (!HasMessage)
			return;

		// Remove held references.
		_confirms.TryRemove(_message.Id, out _);

		// Send canceled response if the confirmation wasn't processed.
		if (!_isProcessed)
			await Submit(false);

		// Raise discard event.
		OnInteractableDiscarded();

		Log.Debug("Cleaned up Confirm interactable.");
		Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
		Log.Debug("  Message ID: {MessageId}", _message.Id);
	}

	// Assumes `_message` has been set; returns immediately if it hasn't.
	private async Task Submit(bool isConfirmed) {
		if (!HasMessage)
			return;

		// Modify the original followup message accordingly.
		if (_doPersist) {
			// If persisting followup message, edit in the appropriate
			// reply based on the response.
			string reply = isConfirmed ? _replyYes : _replyNo;
			await _interaction.EditFollowupAsync(_message.Id, reply);
		} else {
			// If not persisting followup message, delete the original
			// followup message.
			await _interaction.DeleteFollowupAsync(_message.Id);
		}

		// Trigger callback.
		await _callback.Invoke(isConfirmed);
	}
	
	// Filter and dispatch any interactions to be properly handled.
	private static Task InteractionDispatchAsync(
		DiscordClient c,
		ComponentInteractionCreateEventArgs e
	) {
		_ = Task.Run(async () => {
			ulong id = e.Message.Id;

			// Consume all interactions originating from a registered
			// message, and created by the corresponding component.
			if (_confirms.TryGetValue(id, out Confirm? confirm)) {
				if (e.Id is not (_idButtonYes or _idButtonNo))
					return;
				e.Handled = true;

				// Can only update if message was already created.
				if (!confirm.HasMessage)
					return;

				// Skip component owner check--`Confirm`s are always
				// ephemeral.

				// Handle buttons.
				Interaction interaction = Interaction.FromComponent(e);
				await confirm.HandleButtonAsync(interaction, e.Id);
			}
		});
		return Task.CompletedTask;
	}
	
	// Handle any button presses for this interactable. The passed in
	// `Interaction` allows for button press-initiated message edits
	// to work reliably even if the message itself has timed out.
	private Task HandleButtonAsync(Interaction interaction, string buttonId) =>
		_queueUpdates.Run(new Task<Task>(async () => {
			await interaction.DeferComponentAsync();

			bool isConfirmed = buttonId is _idButtonYes;
			await Submit(isConfirmed);
			// This needs to be set so `Discard()` knows it doesn't
			// need to invoke the callback again.
			_isProcessed = true;

			await Discard();
		}));

	// Create button components according to the selected options.
	private static DiscordComponent[] GetButtons(
		string labelYes,
		string labelNo,
		ButtonStyle buttonYesStyle
	) =>
		new DiscordComponent[] {
			new DiscordButton(
				ButtonStyle.Secondary,
				_idButtonNo,
				labelNo
			),
			new DiscordButton(
				buttonYesStyle,
				_idButtonYes,
				labelYes
			),
		};
}
