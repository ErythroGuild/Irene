using System.Timers;

using Component = DSharpPlus.Entities.DiscordComponent;

namespace Irene.Interactables;

class Pages {
	public const int DefaultPageSize = 8;
	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

	// Master table of all Pages to handle, indexed by the message ID
	// of the owning message.
	// This also serves as a way to "hold" fired timers, preventing them
	// from prematurely going out of scope and being destroyed.
	private static readonly ConcurrentDictionary<ulong, Pages> _pages = new ();
	private const string
		_idButtonPrev = "pages_list_prev",
		_idButtonNext = "pages_list_next",
		_idButtonPage = "pages_list_page";
	private static readonly IReadOnlySet<string> _ids =
		new HashSet<string> { _idButtonPrev, _idButtonNext, _idButtonPage };
	private const string
		_labelPrev = "\u25B2",
		_labelNext = "\u25BC";

	// All events are handled by a single delegate, registered on init.
	// This means there doesn't need to be a large amount of delegates
	// that each event has to filter through until it hits the correct
	// handler.
	static Pages() {
		Client.ComponentInteractionCreated += (client, e) => {
			_ = Task.Run(async () => {
				ulong id = e.Message.Id;

				// Consume all interactions originating from a registered
				// message, and created by the corresponding component.
				if (_pages.ContainsKey(id)) {
					Pages pages = _pages[id];
					if (!_ids.Contains(e.Id))
						return;
					e.Handled = true;

					// Can only update if message was already created.
					if (pages._message is null)
						return;

					// Only respond to interactions created by the owner
					// of the interactable.
					if (e.User != pages._interaction.User)
						return;

					// Handle buttons.
					switch (e.Id) {
					case _idButtonPrev:
						pages._page--;
						break;
					case _idButtonNext:
						pages._page++;
						break;
					}
					pages._page = Math.Max(pages._page, 0);
					pages._page = Math.Min(pages._page, pages._pageCount);

					// Update original message.
					Interaction interaction = Interaction.FromComponent(e);
					await interaction.UpdateComponentAsync(pages.BuildMessage());
				}
			});
			return Task.CompletedTask;
		};
	}

	// Instance properties.
	private readonly Interaction _interaction;
	private DiscordMessage? _message = null;
	private readonly Timer _timer;
	private readonly List<string> _data;
	private int _page;
	private readonly int _pageCount;
	private readonly int _pageSize;
	private string CurrentContent { get {
		int i_start = _page * _pageSize;
		int i_end = Math.Min(i_start + _pageSize, _data.Count);
		int i_range = i_end - i_start;

		return _data.GetRange(i_start, i_range).ToLines();
	} }

	// Public factory method constructor.
	// Use this method to instantiate a new interactable.
	public static DiscordMessageBuilder Create(
		Interaction interaction,
		Task<DiscordMessage> messageTask,
		IReadOnlyList<string> data,
		int? pageSize=null,
		TimeSpan? timeout=null
	) {
		pageSize ??= DefaultPageSize;
		timeout ??= DefaultTimeout;
		Timer timer = Util.CreateTimer(timeout.Value, false);

		// Construct partial Pages object.
		Pages pages = new (
			interaction,
			new List<string>(data),
			pageSize.Value,
			timer
		);
		messageTask.ContinueWith((messageTask) => {
			DiscordMessage message = messageTask.Result;
			pages._message = message;
			_pages.TryAdd(message.Id, pages);
			pages._timer.Start();
		});
		timer.Elapsed += async (obj, e) => {
			// Run (or schedule to run) cleanup task.
			if (!messageTask.IsCompleted)
				await messageTask.ContinueWith((e) => pages.Cleanup());
			else
				await pages.Cleanup();
		};

		return pages.BuildMessage();
	}
	private Pages(
		Interaction interaction,
		List<string> data,
		int pageSize,
		Timer timer
	) {
		// Calculate page count.
		// It's convenient to save this result, since it's used on every
		// button press.
		double pageCount = data.Count / (double)pageSize;
		pageCount = Math.Round(Math.Ceiling(pageCount));

		_interaction = interaction;
		_timer = timer;
		_data = data;
		_page = 0;
		_pageCount = (int)pageCount;
		_pageSize = pageSize;
	}

	// Manually time-out the timer (and fire the elapsed handler).
	public async Task Discard() {
		_timer.Stop();
		const double delay = 0.1;
		_timer.Interval = delay; // arbitrarily small interval, must be >0
		_timer.Start();
		await Task.Delay(TimeSpan.FromMilliseconds(delay));
	}

	// Cleanup task to dispose of all resources.
	// Assumes _message has been set; returns immediately if it hasn't.
	private async Task Cleanup() {
		if (_message is null)
			return;

		// Remove held references.
		_pages.TryRemove(_message.Id, out _);

		// Re-fetch message.
		_message = await Util.RefetchMessage(_message);

		// Rebuild message as disabled.
		await _interaction.EditResponseAsync(BuildWebhook(false));

		Log.Debug("Cleaned up Pages interactable.");
		Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
		Log.Debug("  Message ID: {MessageId}", _message.Id);
	}

	// Construct a message based on the current instance's data.
	private DiscordMessageBuilder BuildMessage(bool isEnabled=true) {
		DiscordMessageBuilder message =
			new DiscordMessageBuilder()
			.WithContent(CurrentContent);
		if (_pageCount > 1)
			message = message.AddComponents(GetButtons(isEnabled));
		return message;
	}
	private DiscordWebhookBuilder BuildWebhook(bool isEnabled=true) {
		DiscordWebhookBuilder webhook =
			new DiscordWebhookBuilder()
			.WithContent(CurrentContent);
		if (_pageCount > 1)
			webhook = webhook.AddComponents(GetButtons(isEnabled));
		return webhook;
	}

	// Returns a row of buttons for paginating the data.
	private Component[] GetButtons(bool isEnabled=true) =>
		GetButtons(_page, _pageCount, isEnabled);
	private static Component[] GetButtons(int page, int total, bool isEnabled=true) =>
		new Component[] {
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonPrev,
				_labelPrev,
				disabled: !isEnabled || (page + 1 == 1)
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonPage,
				$"{page + 1} / {total}",
				disabled: !isEnabled
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonNext,
				_labelNext,
				disabled: !isEnabled || (page + 1 == total)
			),
		};
}
