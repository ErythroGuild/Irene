namespace Irene.Interactables;

using System.Diagnostics.CodeAnalysis;
using System.Timers;

using Entry = ISelector.Entry;
using Decorator = Pages.Decorator;

// `SelectorPagesOptions` can be individually set and passed to the
// static `SelectorPages` factory constructor. Any unspecified options
// default to the specified values.
class SelectorPagesOptions {
	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

	// Whether or not the pagination select menu is enabled.
	public bool IsEnabled { get; init; } = true;

	// The duration each `Pages` lasts before being automatically
	// disabled. Ephemeral responses MUST have a timeout less than
	// Discord's limit of 15 mins/interaction--past that the message
	// itself cannot be updated anymore.
	public TimeSpan Timeout { get; init; } = DefaultTimeout;

	// An additional delegate to run after a page is constructed,
	// enabling extended formatting (e.g. adding extra components
	// under the pagination select menu).
	public Decorator? Decorator { get; init; } = null;
}

class SelectorPages {
	// The `Renderer` delegate transforms data into rendered messages,
	// allowing the data itself to be stored more concisely/naturally.
	public delegate IDiscordMessageBuilder Renderer(object data, bool isEnabled);


	// --------
	// Constants and static properties:
	// --------

	// Master table of all `SelectorPages` being tracked, indexed by
	// the message ID of the containing message (since this is unique).
	// This also serves as a way to hold fired timers, preventing them
	// from going out of scope and being destroyed prematurely.
	private static readonly ConcurrentDictionary<ulong, SelectorPages> _pages = new ();

	private const string _idSelectorPages = "selectorpages_select";


	// --------
	// Instance properties and fields:
	// --------

	// Public properties.
	public bool IsEnabled { get; private set; }
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
	private readonly Dictionary<string, object> _data;
	private readonly PagedSelector _selector;
	private string _idSelected;

	// Protected fields for dependency injection.
	protected Renderer _renderer;
	protected Decorator? _decorator;
	

	// --------
	// Factory method and constructor:
	// --------

	// The interactable is registered to the table of `SelectorPages`
	// (and the auto-discard timer starts running) only when the message
	// promise is fulfilled.
	public static SelectorPages Create(
		Interaction interaction,
		MessagePromise promise,
		IReadOnlyList<Entry> entries,
		IReadOnlyDictionary<string, object> data,
		string? idSelected,
		Renderer renderer,
		SelectorPagesOptions? options=null
	) {
		options ??= new ();

		// Construct partial (uninitialized) object.
		SelectorPages pages = new (
			interaction,
			promise,
			entries,
			new (data),
			idSelected,
			renderer,
			options
		);

		// Set up registration and auto-discard.
		pages.FinalizeInstance(promise);

		return pages;
	}

	// Since the protected constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	protected SelectorPages(
		Interaction interaction,
		MessagePromise promise,
		IReadOnlyList<Entry> pages,
		Dictionary<string, object> data,
		string? idSelected,
		Renderer renderer,
		SelectorPagesOptions options
	) {
		IsEnabled = options.IsEnabled;

		_interaction = interaction;
		_timer = Util.CreateTimer(options.Timeout, false);
		_renderer = renderer;
		_data = data;
		_selector = PagedSelector.Create(
			interaction,
			promise,
			entry => Task.Run(async () => {
				if (entry is null)
					return;
				_idSelected = entry.Value.Id;
				await Update();
			}),
			_idSelectorPages,
			pages,
			idSelected,
			new PagedSelectorOptions() {
				IsEnabled = options.IsEnabled,
				Timeout = options.Timeout,
			}
		);
		_idSelected = idSelected ?? pages[0].Id;

		_renderer = renderer;
		_decorator = options.Decorator;
	}

	// The entire `SelectorPages` object cannot be constructed in one
	// stage; this second stage registers the object after the message
	// promise is fulfilled and sets up auto-discard.
	protected void FinalizeInstance(MessagePromise promise) {
		Task<DiscordMessage> promiseTask = promise.Task;

		// Register instance.
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
		// Render page.
		object dataPage = _data[_idSelected];
		IDiscordMessageBuilder content = _renderer(dataPage, IsEnabled);

		// Add pagination buttons as appropriate.
		content.AddComponents(_selector.GetSelect());

		// Decorate, if configured to.
		if (_decorator is not null)
			content = _decorator(content, IsEnabled);

		return content;
	}

	// Enable/disable the attached select menu.
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

		Log.Debug("Cleaned up SelectorPages interactable.");
		Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
		Log.Debug("  Message ID: {MessageId}", _message.Id);
	}
}
