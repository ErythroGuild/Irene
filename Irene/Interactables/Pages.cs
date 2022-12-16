namespace Irene.Interactables;

using System.Timers;

// `PagesOptions` can be individually set and passed to the static
// `Pages` factory constructor. Any unspecified options default to
// the specified values.
class PagesOptions {
	public static int DefaultPageSize => 8;
	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

	// Whether or not the pagination buttons are disabled.
	public bool IsEnabled { get; init; } = true;

	// The number of data items to render per page.
	public int PageSize { get; init; } = DefaultPageSize;

	// The duration each `Pages` lasts before being automatically
	// disabled. Ephemeral responses MUST have a timeout less than
	// Discord's limit of 15 mins/interaction--past that the message
	// itself cannot be updated anymore.
	public TimeSpan Timeout { get; init; } = DefaultTimeout;

	// An additional delegate to run after a page is constructed,
	// enabling extended formatting (e.g. adding extra components
	// under the pagination buttons).
	public Pages.Decorator? Decorator { get; init; } = null;
}

class Pages {
	// The `Renderer` delegate transforms data into rendered messages,
	// allowing the data itself to be stored more concisely/naturally.
	public delegate IDiscordMessageBuilder
		Renderer(List<object> data, bool isEnabled);
	// The `Decorator` delegate runs after a page's message content has
	// been rendered, allowing for extended formatting (e.g. appending
	// extra components under the pagination buttons).
	public delegate IDiscordMessageBuilder
		Decorator(IDiscordMessageBuilder content, bool isEnabled);


	// --------
	// Constants and static properties:
	// --------

	// Master table of all `Pages` being tracked, indexed by the message
	// ID of the containing message (since this will be unique).
	// This also serves as a way to hold fired timers, preventing them
	// from going out of scope and being destroyed prematurely.
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
	// This means each event doesn't have to filter through all handlers
	// of the same type until it hits the right one.
	static Pages() {
		CheckErythroInit();
		Erythro.Client.ComponentInteractionCreated +=
			InteractionHandlerAsync;
	}


	// --------
	// Instance properties and fields:
	// --------

	public bool IsEnabled { get; private set; }
	public DiscordUser Owner => _interaction.User;
	public readonly int PageCount;

	// This event is raised both when the interactable auto-times out,
	// and also when `Discard()` is manually called.
	public event EventHandler? InteractableDiscarded;
	// Wrapper method to allow derived classes to invoke this event.
	protected virtual void OnInteractableDiscarded() =>
		InteractableDiscarded?.Invoke(this, new());

	private readonly TaskQueue _queueUpdates = new ();
	private readonly Interaction _interaction;
	private DiscordMessage? _message = null;
	private readonly Timer _timer;
	private readonly Renderer _renderer;
	private readonly List<object> _data;
	private readonly int _pageSize;
	private readonly Decorator? _decorator;
	private int _page;

	private DiscordComponent[] Buttons =>
		GetButtons(_page, PageCount, IsEnabled);


	// --------
	// Factory method and constructor:
	// --------

	// The interactable is registered to the table of `Pages` (and the
	// auto-discard timer starts running) only when the `DiscordMessage`
	// promise is fulfilled.
	public static Pages Create(
		Interaction interaction,
		Task<DiscordMessage> messageTask,
		Renderer renderer,
		IReadOnlyList<object> data,
		PagesOptions? options=null
	) {
		options ??= new PagesOptions();

		// Construct partial (uninitialized) object.
		Timer timer = Util.CreateTimer(options.Timeout, false);
		Pages pages = new (
			options.IsEnabled,
			interaction,
			timer,
			renderer,
			new List<object>(data),
			options.PageSize,
			options.Decorator
		);

		// Set up registration and auto-discard.
		messageTask.ContinueWith(t => {
			DiscordMessage message = t.Result;
			pages._message = message;
			_pages.TryAdd(message.Id, pages);
			pages._timer.Start();
		});
		timer.Elapsed += async (_, _) => {
			// Run (or schedule to run) auto-discard.
			if (!messageTask.IsCompleted)
				await messageTask.ContinueWith(e => pages.Cleanup());
			else
				await pages.Cleanup();
		};

		return pages;
	}

	// Since the private constructor only partially constructs the object,
	// it should never be called directly. Always use the public factory
	// method instead.
	private Pages(
		bool isEnabled,
		Interaction interaction,
		Timer timer,
		Renderer renderer,
		List<object> data,
		int pageSize,
		Decorator? decorator
	) {
		// Calculate page count.
		// This should be cached since it's constant, and used on every
		// single button interaction.
		double pageCount = data.Count / (double)pageSize;
		pageCount = Math.Round(Math.Ceiling(pageCount));

		IsEnabled = isEnabled;
		PageCount = (int)pageCount;

		_interaction = interaction;
		_timer = timer;
		_renderer = renderer;
		_data = data;
		_page = 0;
		_pageSize = pageSize;
		_decorator = decorator;
	}


	// --------
	// Public methods:
	// --------

	// Returns the current page content as a message.
	public DiscordMessageBuilder GetContentAsBuilder() => new (GetContent());
	public DiscordWebhookBuilder GetContentAsWebhook() => new (GetContent());
	public IDiscordMessageBuilder GetContent() {
		// Calculate data range for current page.
		int i_start = _page * _pageSize;
		int i_end = Math.Min(i_start + _pageSize, _data.Count);
		int i_range = i_end - i_start;

		// Render page.
		List<object> dataContent = _data.GetRange(i_start, i_range);
		IDiscordMessageBuilder content = _renderer(dataContent, IsEnabled);

		// Add pagination buttons as appropriate.
		if (PageCount > 1)
			content.AddComponents(Buttons);

		// Decorate, if configured to.
		if (_decorator is not null)
			content = _decorator(content, IsEnabled);

		return content;
	}

	// Enable/disable the attached pagination buttons.
	public Task Enable() {
		IsEnabled = true;
		return Update();
	}
	public Task Disable() {
		IsEnabled = false;
		return Update();
	}

	// Trigger the auto-discard by manually timing-out the timer.
	// This disables the pagination buttons, but not any components that
	// the `Decorator` added later.
	public async Task Discard() {
		_timer.Stop();

		// Set the timeout to an arbitrarily small interval, (`Timer`
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
	private Task Update() =>
		_queueUpdates.Run(new Task<Task>(async () => {
			if (_message is null)
				return;

			await _interaction.EditResponseAsync(GetContentAsWebhook());
		}));

	// Assumes `_message` has been set; returns immediately if it hasn't.
	private async Task Cleanup() {
		if (_message is null)
			return;

		// Remove held references.
		_pages.TryRemove(_message.Id, out _);

		await Disable();

		// Raise discard event.
		OnInteractableDiscarded();

		Log.Debug("Cleaned up Pages interactable.");
		Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
		Log.Debug("  Message ID: {MessageId}", _message.Id);
	}

	// Filter and dispatch any interactions to be properly handled.
	private static Task InteractionHandlerAsync(
		DiscordClient c,
		ComponentInteractionCreateEventArgs e
	) {
		_ = Task.Run(async () => {
			ulong id = e.Message.Id;

			// Consume all interactions originating from a registered
			// message, and created by the corresponding component.
			if (_pages.TryGetValue(id, out Pages? pages)) {
				if (!_ids.Contains(e.Id))
					return;
				e.Handled = true;

				// Can only update if message was already created.
				if (pages._message is null)
					return;

				// Only respond to interactions created by the owner
				// of the interactable.
				Interaction interaction = Interaction.FromComponent(e);
				if (e.User != pages.Owner) {
					await interaction.RespondComponentNotOwned(pages.Owner);
					return;
				}

				// Handle buttons.
				await pages.HandleButtonAsync(interaction, e.Id);
			}
		});
		return Task.CompletedTask;
	}

	// Handle any button presses for this interactable. The passed in
	// `Interaction` allows for button press-initiated message edits
	// to work reliably even if the message itself has timed out.
	private Task HandleButtonAsync(Interaction interaction, string buttonId) =>
		_queueUpdates.Run(new Task<Task>(async () => {
			switch (buttonId) {
			case _idButtonPrev:
				_page--;
				break;
			case _idButtonNext:
				_page++;
				break;
			}
			_page = Math.Max(_page, 0);
			_page = Math.Min(_page, PageCount);

			// Update original message.
			await interaction.UpdateComponentAsync(GetContentAsBuilder());
		}));

	// Creates a row of pagination buttons.
	private static DiscordComponent[] GetButtons(
		int page,
		int total,
		bool isEnabled=true
	) =>
		new DiscordComponent[] {
			new DiscordButton(
				ButtonStyle.Secondary,
				_idButtonPrev,
				_labelPrev,
				disabled: !isEnabled || (page + 1 == 1)
			),
			new DiscordButton(
				ButtonStyle.Secondary,
				_idButtonPage,
				$"{page + 1} / {total}",
				disabled: !isEnabled
			),
			new DiscordButton(
				ButtonStyle.Secondary,
				_idButtonNext,
				_labelNext,
				disabled: !isEnabled || (page + 1 == total)
			),
		};
}
