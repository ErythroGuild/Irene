using System.Timers;

using ConfirmCallback = System.Func<bool, DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs, System.Threading.Tasks.Task>;
using Component = DSharpPlus.Entities.DiscordComponent;

namespace Irene.Components;

class Confirm {
	public const string
		DefaultStringPrompt = "Are you sure you want to proceed?",
		DefaultStringYes = "Request confirmed.",
		DefaultStringNo  = "Request canceled. No changes made.",
		DefaultLabelYes  = "Confirm",
		DefaultLabelNo   = "Cancel";
	public static TimeSpan DefaultTimeout { get => TimeSpan.FromSeconds(90); }

	// Table of all Confirms to handle, indexed by the message ID of the
	// owning message.
	// This also serves as a way to "hold" fired timers, preventing them
	// from going out of scope and being destroyed.
	private static readonly ConcurrentDictionary<ulong, Confirm> _confirms = new ();
	private const string
		_idButtonYes = "confirm_yes",
		_idButtonNo  = "confirm_no" ;

	// Force static initializer to run.
	public static void Init() { return; }
	// All events are handled by a single delegate, registered on init.
	// This means there doesn't need to be a large amount of delegates
	// that each event has to filter through until it hits the correct
	// handler.
	static Confirm() {
		Client.ComponentInteractionCreated += async (client, e) => {
			ulong id = e.Message.Id;

			// Consume all interactions originating from a registered
			// message, and created by the corresponding component.
			if (_confirms.ContainsKey(id)) {
				e.Handled = true;
				Confirm confirm = _confirms[id];

				// Only respond to interactions created by the "owner"
				// of the component.
				if (e.User != confirm._interaction.User) {
					await e.Interaction.AcknowledgeComponentAsync();
					return;
				}

				// Delete message and pass response to callback.
				bool isConfirmed = e.Id == _idButtonYes;
				await e.Interaction.AcknowledgeComponentAsync();
				await confirm._interaction.DeleteOriginalResponseAsync();
				await confirm._callback(isConfirmed, e);
				confirm._isConfirmed = isConfirmed;
				await confirm.Discard();
			}
		};
	}

	// Instance (configuration) properties.
	private readonly ConfirmCallback _callback;
	private readonly string _stringYes, _stringNo;
	private DiscordMessage? _message;
	private readonly DiscordInteraction _interaction;
	private readonly Timer _timer;
	private bool _isConfirmed;

	// Private constructor.
	// Use Confirm.Create() to create a new instance.
	private Confirm(
		ConfirmCallback callback,
		string? string_prompt,
		string? string_yes,
		string? string_no,
		string? label_yes,
		string? label_no,
		DiscordInteraction interaction,
		Timer timer
	) {
		_stringPrompt = string_prompt ?? DefaultStringPrompt;
		_stringYes = string_yes ?? DefaultStringYes;
		_stringNo  = string_no  ?? DefaultStringNo;
		_labelYes  = label_yes  ?? DefaultLabelYes;
		_labelNo   = label_no   ?? DefaultLabelNo;
		_callback = callback;
		_interaction = interaction;
		_timer = timer;
		_isConfirmed = false;
	}

	// Manually time-out the timer (and fire the elapsed handler).
	public async Task Discard() {
		const double delay = 0.1;
		_timer.Stop();
		_timer.Interval = delay;
		_timer.Start();
		await Task.Delay(TimeSpan.FromMilliseconds(delay));
	}

	// Cleanup task to dispose of all resources.
	// Does not check for _message being completed yet.
	private async Task Cleanup() {
		if (_message is null)
			return;

		// Remove held references.
		_confirms.TryRemove(_message.Id, out _);

		// Update message to disable component.
		// Interaction responses behave as webhooks and need to be
		// constructed as such.
		_message = await _interaction.GetOriginalResponseAsync();
		string string_result = _isConfirmed
			? _stringYes
			: _stringNo;
		DiscordWebhookBuilder message_new =
			new DiscordWebhookBuilder()
				.WithContent(string_result);

		// Edit original message.
		// This must be done through the original interaction, as
		// responses to interactions don't actually "exist" as real
		// messages.
		await _interaction.EditOriginalResponseAsync(message_new);
	}

	public static DiscordMessageBuilder Create(
		DiscordInteraction interaction,
		Task<DiscordMessage> messageTask,
		ConfirmCallback callback,
		string? string_prompt=null,
		string? string_yes=null,
		string? string_no=null,
		string? label_yes=null,
		string? label_no=null,
		TimeSpan? timeout=null
	) {
		timeout ??= DefaultTimeout;
		Timer timer = Util.CreateTimer(timeout.Value, false);

		// Construct partial Confirm object.
		Confirm confirm = new (
			callback,
			string_prompt,
			string_yes,
			string_no,
			label_yes,
			label_no,
			interaction,
			timer
		);
		messageTask.ContinueWith((messageTask) => {
			DiscordMessage message = messageTask.Result;
			confirm._message = message;
			_confirms.TryAdd(message.Id, confirm);
			confirm._timer.Start();
		});
		timer.Elapsed += async (obj, e) => {
			// Run (or schedule to run) cleanup task.
			if (!messageTask.IsCompleted)
				await messageTask.ContinueWith((e) => confirm.Cleanup());
			else
				await confirm.Cleanup();
		};

		// Construct DiscordMessageBuilder from data.
		return new DiscordMessageBuilder()
			.WithContent(string_prompt)
			.AddComponents(GetButtons(label_yes, label_no));
	}

	// Returns the buttons used to confirm/cancel the request.
	private static Component[] GetButtons(string? label_yes, string? label_no) =>
		new Component[] {
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonNo,
				label_no ?? DefaultLabelNo
			),
			new DiscordButtonComponent(
				ButtonStyle.Danger,
				_idButtonYes,
				label_yes ?? DefaultLabelYes
			),
		};
}
