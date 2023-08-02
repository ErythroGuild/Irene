namespace Irene.Interactables;

using System.Diagnostics.CodeAnalysis;
using System.Timers;

using Id = ISelector.Id;
using Entry = ISelector.Entry;

// `PagedSelectorOptions` can be individually set and passed to the
// static `PagedSelector` factory constructor. Any unspecified options
// default to the specified values.
class PagedSelectorOptions {
	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

	// Whether or not the select component is enabled.
	public bool IsEnabled { get; init; } = true;

	// Whether or not deselecting an option is allowed. If false, then
	// any attempt to deselect the active option is coerced into the
	// originally active option (or first option, during init).
	public bool CanDeselect { get; init; } = false;

	// The text for the select component to display when no entries
	// are idSelected.
	public string Placeholder { get; init; } = "";

	// The duration each `PagedSelector` lasts before being automatically
	// disabled. Ephemeral responses MUST have a timeout less than
	// Discord's limit of 15 mins/interaction--past that the message
	// itself cannot be updated anymore.
	public TimeSpan Timeout { get; init; } = DefaultTimeout;
}

class PagedSelector {
	// A delegate to be called after receiving a selector update event.
	// The callback is passed the idSelected entry, null is passed if an
	// existing selection was deselected.
	public delegate Task Callback(Entry? idSelected);


	// --------
	// Constants and static properties:
	// --------

	// Master table of all `PagedSelector`s being tracked, indexed by
	// the message ID of the owning message + selector custom ID.
	// This also serves as a way to hold fired timers, preventing them
	// from going out of scope and being destroyed prematurely.
	private static readonly ConcurrentDictionary<Id, PagedSelector> _selectors = new ();

	// Limiting entries to 20 ensures there will always be extra room
	// for pagination entries, without having to dynamically vary the
	// number of entries per page depending on if (or which) pagination
	// entries are needed.
	// Discord's limit for select menu options is 25.
	private const int _pageSize = 20;
	// This should never actually be hit, because a label's max length
	// is only 100.
	private const int _placeholderLimit = 150;

	private const string
		_idPrev = "_page_prev",
		_idNext = "_page_next";
	private const string
		_labelPrev = "\u25B2",
		_labelNext = "\u25BC";

	// All events are handled by a single delegate, registered on init.
	// This means each event doesn't have to filter through all handlers
	// of the same type until it hits the right one.
	static PagedSelector() {
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
	public Entry? Selected =>
		(_i_selected is null)
			? null
			: _entries[_i_selected.Value];
	public readonly int PageCount;

	// This event is raised both when the interactable auto-times out,
	// and also when `Discard()` is manually called.
	public event EventHandler? InteractableDiscarded;
	// Wrapper method to allow derived classes to invoke this event.
	protected virtual void OnInteractableDiscarded() =>
		InteractableDiscarded?.Invoke(this, new ());

	// Private fields and properties.
	private readonly TaskQueue _queueUpdates = new ();
	private readonly Interaction _interaction;
	private DiscordMessage? _message = null;
	private readonly Timer _timer;
	private readonly Callback _callback;
	private readonly List<Entry> _entries;
	private readonly bool _canDeselect;
	private int? _i_selected;
	private readonly string _placeholder;
	private int _page;
	private int? PageOfSelected =>
		GetPageOfEntry(_i_selected);


	// --------
	// Factory method and constructor:
	// --------

	// The interactable is registered to the table of `PagedSelector`s
	// (and the auto-discard timer starts running) only when the message
	// promise is fulfilled.
	public static PagedSelector Create(
		Interaction interaction,
		MessagePromise promise,
		Callback callback,
		string customId,
		IReadOnlyList<Entry> entries,
		string? idSelected,
		PagedSelectorOptions? options=null
	) {
		options ??= new ();

		// Construct partial (uninitialized) object.
		PagedSelector selector = new (
			interaction,
			callback,
			customId,
			entries,
			idSelected,
			options
		);

		// Set up registration and auto-discard.
		selector.FinalizeInstance(promise);

		return selector;
	}

	// Since the private constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	private PagedSelector(
		Interaction interaction,
		Callback callback,
		string customId,
		IReadOnlyList<Entry> entries,
		string? idSelected,
		PagedSelectorOptions options
	) {
		// Calculate page count.
		// This should be cached since it's constant, and used on every
		// single button interaction.
		double pageCount = entries.Count / (double)_pageSize;
		pageCount = Math.Round(Math.Ceiling(pageCount));

		IsEnabled = options.IsEnabled;
		CustomId = customId;
		PageCount = (int)pageCount;

		_interaction = interaction;
		_timer = Util.CreateTimer(options.Timeout, false);
		_callback = callback;
		_entries = new (entries);
		_canDeselect = options.CanDeselect;
		_i_selected = (idSelected is null)
			? (_canDeselect ? null : 0)
			: _entries.FindIndex(e => e.Id == idSelected);
		_placeholder = options.Placeholder;
		_page = GetPageOfEntry(_i_selected) ?? 0;
	}

	// The entire `PagedSelector` object cannot be constructed in one
	// stage; this second stage registers the object after the message
	// promise is fulfilled and sets up auto-discard.
	private void FinalizeInstance(MessagePromise promise) {
		Task<DiscordMessage> promiseTask = promise.Task;

		// Register instance.
		promiseTask.ContinueWith(t => {
			DiscordMessage message = t.Result;
			_message = message;
			_selectors.TryAdd(new (message.Id, CustomId), this);
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

	// Constructs the current `DiscordSelect` component from internal
	// state.
	// This is actually more efficient than storing a copy and modifying
	// it on every call, since the cost of construction stays constant,
	// and subsequent constructions are cheaper than reading back and
	// then modifying.
	public DiscordSelect GetSelect() => new (
		CustomId,
		GetPlaceholder(),
		GetSelectEntries(),
		disabled: !IsEnabled,
		minOptions: 0, // unselected pages have 0 options idSelected
		maxOptions: 1
	);
	// Returns the placeholder (potentially a idSelected label).
	private string GetPlaceholder() {
		if (_i_selected is null)
			return _placeholder;

		string selected = $"{Selected!.Value.Label}";
		string page = $"\u2003(pg. {PageOfSelected+1})"; // em space

		// Shorten label if needed.
		int length = selected.Length + page.Length;
		if (length > _placeholderLimit) {
			int selectedLength = _placeholderLimit - page.Length -1;
			selected = $"{selected[..selectedLength]}\u2026"; // ellipsis
		}

		return selected + page;
	}
	// Returns the select entries for the current page.
	private List<DiscordSelectOption> GetSelectEntries() {
		List<DiscordSelectOption> entries = new ();

		// Conditionally add "previous page" pagination entry.
		if (_page > 0)
			entries.Add(GetEntryPrev());

		// Calculate data range for current page.
		int i_start = _page * _pageSize;
		int i_end = Math.Min(i_start + _pageSize, _entries.Count);
		int i_range = i_end - i_start;

		// Add entries in current page's range.
		foreach (Entry entry in _entries.GetRange(i_start, i_range)) {
			DiscordSelectOption selectEntry = new (
				entry.Label,
				entry.Id,
				entry.Description,
				entry.Id == Selected?.Id,
				entry.Emoji
			);
			entries.Add(selectEntry);
		}

		// Conditionally add "next page" pagination entry.
		if (_page + 1 < PageCount)
			entries.Add(GetEntryNext());

		return entries;
	}

	// Enable/disable the associated `DiscordSelect` component.
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
	private Task Update() =>
		_queueUpdates.Run(new Task<Task>(UpdateUnguarded));
	private async Task UpdateUnguarded() {
		CheckErythroInit();
		if (!HasMessage)
			return;

		// Re-fetch message.
		_message = await Util.RefetchMessage(Erythro.Client, _message);

		// Rebuild message with select component updated.
		await _interaction.EditResponseAsync(ReplaceSelector(_message));
	}

	// Assumes `_message` has been set; returns immediately if it hasn't.
	private async Task Cleanup() {
		if (!HasMessage)
			return;

		// Remove held references.
		_selectors.TryRemove(new (_message.Id, CustomId), out _);

		// Switch to the page with the idSelected entry (if one exists).
		// The update happens in the `Disable()` call.
		if (Selected is not null)
			_page = PageOfSelected ?? throw new ImpossibleException();

		await Disable();

		// Raise discard event.
		OnInteractableDiscarded();

		Log.Debug("Cleaned up PagedSelector interactable.");
		Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
		Log.Debug("  Message ID: {MessageId}", _message.Id);
		Log.Debug("  Selector ID: {CustomId}", CustomId);
	}
	
	// Filter and dispatch any interactions to be properly handled.
	private static async Task InteractionDispatcherAsync(
		DiscordClient c,
		ComponentInteractionCreateEventArgs e
	) {
		Id id = new (e.Message.Id, e.Id);

		// Consume all interactions originating from a registered
		// message, and created by the corresponding component.
		if (_selectors.TryGetValue(id, out PagedSelector? selector)) {
			// Can only update if message was already created.
			if (!selector.HasMessage)
				return;

			// Only respond to interactions created by the owner
			// of the interactable.
			Interaction interaction = Interaction.FromComponent(e);
			if (e.User != selector.Owner) {
				await interaction.RespondComponentNotOwned(selector.Owner);
				return;
			}

			// Handle selection.
			string? selected = (e.Values.Length == 0)
				? null
				: e.Values[0];
			await selector.HandleSelectorAsync(interaction, selected);
		}
	}

	// Handle any button presses for this interactable. The passed in
	// `Interaction` allows for button press-initiated message edits
	// to work reliably even if the message itself has timed out.
	// This should behave the same for all derived classes, and therefore
	// doesn't need to be overridden.
	private Task HandleSelectorAsync(Interaction interaction, string? selected) =>
		_queueUpdates.Run(new Task<Task>(async () => {
			// Update internal state.
			int? i_selectedPrev = _i_selected;
			switch (selected) {
			case null:
				if (_canDeselect)
					_i_selected = null;
				break;
			case _idPrev:
				_page--;
				break;
			case _idNext:
				_page++;
				break;
			default:
				_i_selected =
					_entries.FindIndex(e => e.Id == selected);
				break;
			}

			// Respond to interaction.
			await interaction.DeferComponentAsync();

			// Actually update DiscordSelect menu.
			// Note: Must use unguarded method to prevent deadlocking
			// on the same task queue.
			await UpdateUnguarded();

			// Invoke callback if the actual selection changed.
			if (_i_selected != i_selectedPrev)
				await _callback.Invoke(Selected);
		}));

	// Returns the (0-based) index of the page of the entry index.
	// This needs to be a separate static method because it's used in
	// the constructor.
	private static int? GetPageOfEntry(int? i_selected) =>
		(i_selected is null)
			? null
			: i_selected / _pageSize;

	// Helper methods to instantiate pagination entries.
	private DiscordSelectOption GetEntryPrev() => new (
		_labelPrev,
		_idPrev,
		$"more options\u2026\u2003(pg. {_page+1-1}/{PageCount})"
		// Ellipsis & em space.
		// The increment is because `_page` is 0-indexed.
		// The decrement is to get the previous page's value.
	);
	private DiscordSelectOption GetEntryNext() => new (
		_labelNext,
		_idNext,
		$"more options\u2026\u2003(pg. {_page+1+1}/{PageCount})"
		// Ellipsis & em space.
		// The first increment is because `_page` is 0-indexed.
		// The second increment is to get the next page's value.
	);
	
	// Iterate through all components on the message, and only replace
	// the one with a matching `CustomId` to the current instance.
	private IDiscordMessageBuilder ReplaceSelector(DiscordMessage message) {
		DiscordMessageBuilder replacement = new (message);
		replacement.ClearComponents();

		foreach (DiscordComponentRow row in message.Components) {
			List<DiscordComponent> components = new ();
			foreach (DiscordComponent component in row.Components) {
				if ((component is DiscordSelect) &&
					(component.CustomId == CustomId)
				) {
					components.Add(GetSelect());
				} else {
					components.Add(component);
				}
			}
			replacement.AddComponents(components);
		}

		return replacement;
	}
}
