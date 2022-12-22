namespace Irene.Interactables;

using Entry = ISelector.Entry;

// `SelectorStringPagesOptions` can be individually set and passed to
// the static `SelectorStringPages` factory constructor. Any unspecified
// options default to the specified values.
class SelectorStringPagesOptions : SelectorPagesOptions {
	// "Sticky" strings which will always be shown on the page, no
	// matter which page is shown.
	// These do not include spacing so extra newlines may be necessary.
	public string? Header { get; init; } = null;
	public string? Footer { get; init; } = null;
}

class SelectorStringPages : SelectorPages {
	// Additional instance properties.
	private readonly string? _header;
	private readonly string? _footer;

	// The interactable is registered to the table of `SelectorPages`
	// (and the auto-discard timer starts running) only when the message
	// promise is fulfilled. (There is no separate table of instances
	// for `StringPages`.)
	public static SelectorStringPages Create(
		Interaction interaction,
		MessagePromise promise,
		IReadOnlyList<Entry> entries,
		IReadOnlyDictionary<string, string> data,
		string? idSelected,
		SelectorStringPagesOptions? options=null
	) {
		options ??= new ();

		// Construct partial (uninitialized) object.
		SelectorStringPages pages = new (
			interaction,
			promise,
			entries,
			new (data),
			idSelected,
			options
		);

		// Set up registration and auto-discard.
		pages.FinalizeInstance(promise);

		return pages;
	}

	// Since the protected constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	protected SelectorStringPages(
		Interaction interaction,
		MessagePromise promise,
		IReadOnlyList<Entry> entries,
		Dictionary<string, string> data,
		string? idSelected,
		SelectorStringPagesOptions options
	) : base (
		interaction,
		promise,
		entries,
		ToObjectTable(data),
		idSelected,
		null!,
		options
	) {
		_header = options.Header;
		_footer = options.Footer;

		_renderer = RenderData;
	}

	// All `SelectorStringPages` render underlying data the same way.
	private IDiscordMessageBuilder RenderData(object data, bool isEnabled) {
		string content = (string)data;

		if (_header is not null)
			content = $"{_header}\n{content}";
		if (_footer is not null)
			content = $"{content}\n{_footer}";

		return new DiscordMessageBuilder().WithContent(content);
	}

	// Convenience functions for converting the `_data` into/from what
	// the base `SelectorPages` class expects.
	private static Dictionary<string, object> ToObjectTable(
		IReadOnlyDictionary<string, string> table
	) {
		Dictionary<string, object> tableObjects = new ();
		foreach (string key in table.Keys)
			tableObjects.Add(key, table[key]);
		return tableObjects;
	}
}
