namespace Irene.Interactables;

using System.Diagnostics.CodeAnalysis;
using System.Timers;

using Id = ISelector.Id;
using Entry = ISelector.Entry;

interface ISelector {
	// A unique identifier for each `Selector` selectEntry (corresponding
	// to a component on a message somewhere).
	public readonly record struct Id(ulong MessageId, string CustomId);
	// An entry representing the data needed to create one DiscordSelect
	// entry choice.
	public readonly record struct Entry(
		string Label,
		string Id,
		DiscordComponentEmoji? Emoji,
		string? Description
	);

	public bool IsEnabled { get; }
	public bool HasMessage { get; }
	public DiscordUser Owner { get; }
	public string CustomId { get; }

	public DiscordSelect GetSelect();
	public Task Enable();
	public Task Disable();
	public Task Discard();
	// Update the `DiscordSelect` and invoke the selector's callback.
	public Task UpdateSelected(IReadOnlySet<string> selectedValues);
}

// Since `Selector`s are generic, unifying the handlers requires using
// a separate class.
static class SelectorDispatcher {
	// Master table of all `Selector`s being tracked, indexed by the
	// message ID of the owning message + selector custom ID.
	// This also serves as a way to hold fired timers, preventing them
	// from going out of scope and being destroyed prematurely.
	private static readonly ConcurrentDictionary<Id, ISelector> _selectors = new ();

	// Public methods for updating the master table.
	public static void Add(Id id, ISelector selector) =>
		_selectors.TryAdd(id, selector);
	public static void Remove(Id id) =>
		_selectors.TryRemove(id, out _);

	// All events are handled by a single delegate, registered on init.
	// This means each event doesn't have to filter through all handlers
	// of the same type until it hits the right one.
	static SelectorDispatcher() {
		CheckErythroInit();
		Erythro.Client.ComponentInteractionCreated += DispatchAsync;
	}

	// Filter and dispatch any interactions to be properly handled.
	private static async Task DispatchAsync(
		DiscordClient c,
		ComponentInteractionCreateEventArgs e
	) {
		Id id = new (e.Message.Id, e.Id);

		// Consume all interactions originating from a registered
		// message, and created by the corresponding component.
		if (_selectors.TryGetValue(id, out ISelector? selector)) {
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

			// Acknowledge the interaction. Let the selector update
			// the message and invoke the callback.
			await interaction.DeferComponentAsync();
			HashSet<string> selected = new (interaction.Values);
			await selector.UpdateSelected(selected);
		}
	}
}


// --------
// Types, delegates, and default options:
// --------

// `SelectorOptions` can be individually set and passed to the static
// `Selector` factory constructor. Any unspecified options default to
// the specified values.
class SelectorOptions {
	public static TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

	// Whether or not the select component is enabled.
	public bool IsEnabled { get; init; } = true;

	// Whether or not multiple entries can be selected at once.
	// Takes precedence over `MaxOptions`.
	public bool IsMultiple { get; init; } = false;

	// The text for the select component to display when no entries
	// are selected.
	public string Placeholder { get; init; } = "";

	// If non-null, specifies the bounds on the number of entries that
	// can be selected at once. If this conflicts with `IsMultiple`,
	// `IsMultiple` takes precedence.
	// If null and `IsMultiple` is true, defaults to 0.
	public int? MinOptions { get; init; } = null;
	// If null and `IsMultiple` is true, defaults to the entry count.
	public int? MaxOptions { get; init; } = null;

	// The duration each `Selector` lasts before being automatically
	// disabled. Ephemeral responses MUST have a timeout less than
	// Discord's limit of 15 mins/interaction--past that the message
	// itself cannot be updated anymore.
	public TimeSpan Timeout { get; init; } = DefaultTimeout;
}

class Selector<T> : ISelector where T : Enum {
	// A delegate to be called after receiving a selector update event.
	// The callback is passed the selected entries' keys.
	public delegate Task Callback(IReadOnlySet<T> selected);


	// --------
	// Instance properties and fields:
	// --------

	// Public properties.
	public bool IsEnabled { get; private set; }
	[MemberNotNullWhen(true, nameof(_message))]
	public bool HasMessage => _message is not null;
	public DiscordUser Owner => _interaction.User;
	public string CustomId { get; private set; }
	public IReadOnlySet<T> Selected => _selected;

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
	private readonly List<(T, Entry)> _entries;
	private HashSet<T> _selected;
	private readonly string _placeholder;
	private readonly int _minSelected;
	private readonly int _maxSelected;


	// --------
	// Factory method and constructor:
	// --------

	// The interactable is registered to the table of `Selector`s (and
	// the auto-discard timer starts running) only when the `DiscordMessage`
	// promise is fulfilled.
	public static Selector<T> Create(
		Interaction interaction,
		MessagePromise promise,
		Callback callback,
		string customId,
		IReadOnlyList<(T, Entry)> entries,
		IReadOnlySet<T> selected,
		SelectorOptions? options=null
	) {
		options ??= new ();

		// Construct partial (uninitialized) object.
		Selector<T> selector = new (
			interaction,
			callback,
			customId,
			new (entries),
			new (selected),
			options
		);

		// Set up registration and auto-discard.
		selector.FinalizeInstance(promise);

		return selector;
	}

	// Since this private constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	private Selector(
		Interaction interaction,
		Callback callback,
		string customId,
		List<(T, Entry)> entries,
		HashSet<T> selected,
		SelectorOptions options
	) {
		// Determine the correct bounds on the number of entries to
		// select. (`IsMultiple` takes precedence.)
		int minSelected = options.IsMultiple
			? (options.MinOptions ?? 0)
			: 1;
		int maxSelected = options.IsMultiple
			? (options.MaxOptions ?? entries.Count)
			: 1;

		IsEnabled = options.IsEnabled;
		CustomId = customId;

		_interaction = interaction;
		_timer = Util.CreateTimer(options.Timeout, false);
		_callback = callback;
		_entries = entries;
		_selected = selected;
		_placeholder = options.Placeholder;
		_minSelected = minSelected;
		_maxSelected = maxSelected;
	}

	// The entire `Selector` object cannot be constructed in one stage;
	// this second stage registers the object after the message promise
	// is fulfilled and sets up auto-discard.
	private void FinalizeInstance(MessagePromise promise) {
		Task<DiscordMessage> promiseTask = promise.Task;

		// Register instance.
		promiseTask.ContinueWith(t => {
			DiscordMessage message = t.Result;
			_message = message;
			SelectorDispatcher.Add(new (message.Id, CustomId), this);
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
		_placeholder,
		GetSelectEntries(),
		disabled: !IsEnabled,
		minOptions: _minSelected,
		maxOptions: _maxSelected
	);
	private List<DiscordSelectOption> GetSelectEntries() {
		List<DiscordSelectOption> selectEntries = new ();

		foreach ((T Key, Entry Entry) entry_i in _entries) {
			Entry entry = entry_i.Entry;
			DiscordSelectOption selectEntry = new (
				entry.Label,
				entry.Id,
				entry.Description,
				_selected.Contains(entry_i.Key),
				entry.Emoji
			);
			selectEntries.Add(selectEntry);
		}

		return selectEntries;
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

	// Update the selected entries' state, pass the new selected values
	// to the callback, and trigger a message update.
	public async Task UpdateSelected(IReadOnlySet<string> selectedValues) {
		// Search through existing entries for matching values.
		HashSet<T> selected = new ();
		foreach ((T Key, Entry Entry) entry_i in _entries) {
			Entry entry = entry_i.Entry;
			if (selectedValues.Contains(entry.Id))
				selected.Add(entry_i.Key);
		}

		// Assign the new selected values.
		_selected = selected;

		// Update message with new selected values.
		await Update();

		// Invoke callback.
		await _callback.Invoke(selected);
	}


	// --------
	// Private helper methods:
	// --------

	// Assumes `_message` has been set; returns immediately if it hasn't.
	private Task Update() =>
		_queueUpdates.Run(new Task<Task>(async () => {
			CheckErythroInit();
			if (!HasMessage)
				return;

			// Re-fetch message.
			_message = await Util.RefetchMessage(Erythro.Client, _message);

			// Rebuild message with select component updated.
			await _interaction.EditResponseAsync(ReplaceSelector(_message));
		}));

	// Assumes `_message` has been set; returns immediately if it hasn't.
	private async Task Cleanup() {
		if (!HasMessage)
			return;

		// Remove held references.
		SelectorDispatcher.Remove(new (_message.Id, CustomId));

		await Disable();

		// Raise discard event.
		OnInteractableDiscarded();

		Log.Debug("Cleaned up Selector interactable.");
		Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
		Log.Debug("  Message ID: {MessageId}", _message.Id);
		Log.Debug("  Selector ID: {CustomId}", CustomId);
	}

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
