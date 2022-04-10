using System.Timers;

namespace Irene.Components;

using ModalCallback = Func<ModalSubmitEventArgs, Task>;

class Modal {
	public static TimeSpan DefaultTimeout { get => TimeSpan.FromMinutes(20); }

	private static readonly ConcurrentDictionary<string, Modal> _modals = new ();

	// Force static initializer to run.
	public static void Init() { return; }
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
	}

	private readonly Timer _timer;
	private readonly ModalCallback _callback;

	public static async Task<Modal> CreateAsync(
		DiscordInteraction interaction,
		string title,
		List<TextInputComponent> components,
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
		Timer timer = new (timeout.Value.TotalMilliseconds) {
			AutoReset = false,
		};
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
	// Use Modal.CreateAsync() to create a new modal.
	private Modal(Timer timer, ModalCallback callback) {
		_timer = timer;
		_callback = callback;
	}
}
