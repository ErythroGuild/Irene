using System.Timers;

using ModalCallback = System.Func<DSharpPlus.EventArgs.ModalSubmitEventArgs, System.Threading.Tasks.Task>;

namespace Irene.Components;

class Modal {
	public static TimeSpan DefaultTimeout { get => TimeSpan.FromMinutes(20); }

	private static readonly ConcurrentDictionary<string, Modal> _modals = new ();

	public static void Init() { }
	// All events are handled by a single delegate, registered on init.
	// This means there doesn't need to be a large amount of delegates
	// that each event has to filter through until it hits the correct
	// handler.
	static Modal() {
		Client.ModalSubmitted += async (client, e) => {
			string id = e.Interaction.Data.CustomId;
			if (_modals.ContainsKey(id)) {
				e.Handled = true;
				await _modals[id]._callback(e);
				_modals[id]._timer.Stop();
				_modals.TryRemove(id, out _);
			}
		};
		Log.Debug("  Created handler for component: Modal");
	}

	private readonly Timer _timer;
	private readonly ModalCallback _callback;

	public static async Task<Modal> RespondAsync(
		DiscordInteraction interaction,
		string title,
		IReadOnlyList<TextInputComponent> components,
		ModalCallback callback,
		TimeSpan? timeout=null
	) {
		string id = interaction.Id.ToString();

		// Construct modal.
		DiscordInteractionResponseBuilder response =
			new DiscordInteractionResponseBuilder()
			.WithTitle(title)
			.WithCustomId(id);
		foreach (TextInputComponent component in components)
			response = response.AddComponents(component);

		// Setup timer.
		timeout ??= DefaultTimeout;
		Timer timer = Util.CreateTimer(timeout.Value, false);
		timer.Elapsed +=
			(t, e) => _modals.TryRemove(id, out _);

		// Instantiate object.
		Modal modal = new (timer, callback);

		// Submit interaction response.
		await interaction.CreateResponseAsync(
			InteractionResponseType.Modal,
			response
		);

		// Start timer.
		_modals.TryAdd(id, modal);
		modal._timer.Start();

		return modal;
	}

	// Returns the custom-id that would be created for a modal from
	// this DiscordInteraction.
	public static string GetId(DiscordInteraction interaction) =>
		interaction.Id.ToString();

	// Private constructor.
	// Use Modal.RespondAsync() to create a new instance.
	private Modal(Timer timer, ModalCallback callback) {
		_timer = timer;
		_callback = callback;
	}
}
