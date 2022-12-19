namespace Irene.Interactables;

using System.Diagnostics.CodeAnalysis;
using System.Timers;

// `PagesOptions` can be individually set and passed to the static
// `Pages` factory constructor. Any unspecified options default to
// the specified values.
class PagesOptions {
	public const int DefaultPageSize = 8;
	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

	// Whether or not the pagination buttons are enabled.
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
		Renderer(IReadOnlyList<object> data, bool isEnabled);
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
	public readonly int PageCount;

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
	private readonly List<object> _data;
	private readonly int _pageSize;
	private int _page;

	// Protected fields for dependency injection.
	protected Renderer _renderer;
	protected Decorator? _decorator;

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
		MessagePromise promise,
		IReadOnlyList<object> data,
		Renderer renderer,
		PagesOptions? options=null
	) {
		options ??= new ();

		// Construct partial (uninitialized) object.
		Pages pages = new (
			options.IsEnabled,
			interaction,
			options.Timeout,
			new (data),
			options.PageSize,
			renderer,
			options.Decorator
		);

		// Set up registration and auto-discard.
		pages.FinalizeInstance(promise);

		return pages;
	}

	// Since the protected constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	protected Pages(
		bool isEnabled,
		Interaction interaction,
		TimeSpan timeout,
		List<object> data,
		int pageSize,
		Renderer renderer,
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
		_timer = Util.CreateTimer(timeout, false);
		_renderer = renderer;
		_data = data;
		_page = 0;
		_pageSize = pageSize;
		_decorator = decorator;
	}

	// The entire `Pages` object cannot be constructed in one stage;
	// this second stage registers the object after the message promise
	// is fulfilled and sets up auto-discard.
	protected void FinalizeInstance(MessagePromise promise) {
		Task<DiscordMessage> promiseTask = promise.Task;

		// Registers instance.
		promiseTask.ContinueWith(t => {
			DiscordMessage message = t.Result;
			_message = message;
			_pages.TryAdd(message.Id, this);
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
			if (!HasMessage)
				return;

			await _interaction.EditResponseAsync(GetContentAsWebhook());
		}));

	// Assumes `_message` has been set; returns immediately if it hasn't.
	protected virtual async Task Cleanup() {
		if (!HasMessage)
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
	// This delegate handles dispatch for all derived classes as well,
	// and so doesn't need to be overridden.
	private static Task InteractionDispatcherAsync(
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
				if (!pages.HasMessage)
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
	// This should behave the same for all derived classes, and therefore
	// doesn't need to be overridden.
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
