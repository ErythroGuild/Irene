namespace Irene.Interactables;

using System.Timers;

// `ModalOptions` can be individually set and passed to the static
// `Modal` factory constructor. Any unspecified options default to
// the specified values.
class ModalOptions {
	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(40);

	// The duration each `Modal` lasts before being discarded (and no
	// more responses accepted).
	public TimeSpan Timeout { get; init; } = DefaultTimeout;
}

class Modal {
	// A unique identifier for each created `Modal`.
	public readonly record struct Id(ulong UserId, string CustomId);

	// The `Callback` delegate is called when the modal response has
	// been submitted. The interaction should be responded to as if it
	// was the original interaction (e.g. respond/defer if it came from
	// a command, defer/update if it came from a component).
	public delegate Task Callback(
		IReadOnlyDictionary<string, string> data,
		Interaction interaction
	);


	// --------
	// Constants and static properties:
	// --------

	// Master table of all `Modal`s being tracked, indexed by the user
	// ID + the custom ID of the tracked modal.
	// This also serves as a way to hold fired timers, preventing them
	// from going out of scope and being destroyed prematurely.
	private static readonly ConcurrentDictionary<Id, Modal> _modals = new ();

	// All events are handled by a single delegate, registered on init.
	// This means each event doesn't have to filter through all handlers
	// of the same type until it hits the right one.
	static Modal() {
		CheckErythroInit();
		Erythro.Client.ModalSubmitted +=
			InteractionDispatchAsync;
	}


	// --------
	// Instance fields:
	// --------
	
	// This event is raised both when the interactable auto-times out,
	// and also when `Discard()` is manually called.
	public event EventHandler? InteractableDiscarded;
	// Wrapper method to allow derived classes to invoke this event.
	protected virtual void OnInteractableDiscarded() =>
		InteractableDiscarded?.Invoke(this, new ());

	private readonly TaskQueue _queueUpdates = new ();
	private readonly Interaction _interaction;
	private readonly Timer _timer;
	private readonly Callback _callback;
	private readonly string _customId;
	private readonly DiscordInteractionResponseBuilder _modal;


	// --------
	// Factory method and constructor:
	// --------
	
	// The interactable is registered to the table of `Modal`s (and
	// the auto-discard timer starts running) only after `Send()` is
	// called. (The response builder itself cannot be publicly accessed,
	// since the `Modal` needs full control of registration.)
	public static Modal Create(
		Interaction interaction,
		Callback callback,
		string customId,
		string title,
		IReadOnlyList<DiscordTextInput> components,
		ModalOptions? options=null
	) {
		options ??= new ();

		// Construct partial Modal object.
		Modal modal = new (
			interaction,
			callback,
			customId,
			title,
			components,
			options
		);

		// Set up auto-discard.
		modal.FinalizeInstance();

		return modal;
	}

	// Since this private constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	private Modal(
		Interaction interaction,
		Callback callback,
		string customId,
		string title,
		IReadOnlyList<DiscordTextInput> components,
		ModalOptions options
	) {
		_interaction = interaction;
		_timer = Util.CreateTimer(options.Timeout, false);
		_callback = callback;
		_customId = customId;
		
		_modal =
			new DiscordInteractionResponseBuilder()
			.WithTitle(title)
			.WithCustomId(customId);
		foreach (DiscordComponent component in components)
			_modal = _modal.AddComponents(component);
	}

	// The entire `Confirm` object cannot be constructed in one stage;
	// this second stage sets up auto-discard.
	// NOTE: The timer is not started here because `Modal` encapsulates
	// the entire response process, and controls the entire lifetime of
	// its own object.
	private void FinalizeInstance() {
		// Run (or schedule to run) auto-discard.
		_timer.Elapsed += (_, _) => Cleanup();
	}


	// --------
	// Public methods:
	// --------

	// Create and send modal as interaction response.
	// This also registers the `Modal` to the event handler (since the
	// modal only starts existing after this), and also starts the auto-
	// discard timer.
	public async Task Send() {
		await _interaction.RespondModalAsync(_modal);
		_modals.TryAdd(GetId(), this);
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

	private void Cleanup() {
		// Remove held references.
		_modals.TryRemove(GetId(), out _);

		// Raise discard event.
		OnInteractableDiscarded();

		Log.Debug("Cleaned up Modal interactable.");
		Log.Debug("  User: {UserTag}", _interaction.User.Tag());
		Log.Debug("  Modal custom ID: {CustomId}", _customId);
	}

	// Helper methods for conveniently creating an `Id` record.
	private Id GetId() => GetId(_interaction, _customId);
	private static Id GetId(Interaction interaction, string customId) =>
		new (interaction.User.Id, customId);
	
	// Filter and dispatch any interactions to be properly handled.
	private static Task InteractionDispatchAsync(
		DiscordClient c,
		ModalSubmitEventArgs e
	) {
		_ = Task.Run(async () => {
			Id id = new (
				e.Interaction.User.Id,
				e.Interaction.Data.CustomId
			);

			if (_modals.TryGetValue(id, out Modal? modal)) {
				e.Handled = true;
				Interaction interaction = Interaction.FromModal(e);
				await modal.HandleModalAsync(e.Values, interaction);
			}
		});
		return Task.CompletedTask;
	}
	
	// Handle any corresponding modal submissions.
	private Task HandleModalAsync(
		IReadOnlyDictionary<string, string> data,
		Interaction interaction
	) =>
		_queueUpdates.Run(new Task<Task>(async () => {
			await _callback.Invoke(data, interaction);

			_modals.TryRemove(GetId(), out _);
			await Discard();
		}));
}
