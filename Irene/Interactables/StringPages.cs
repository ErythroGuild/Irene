namespace Irene.Interactables;

// `StringPagesOptions` can be individually set and passed to the static
// `StringPages` factory constructor. Any unspecified options default
// to the specified values.
class StringPagesOptions : PagesOptions {
	// "Sticky" strings which will always be shown on the page, no
	// matter which page is shown.
	// These do not include spacing so extra newlines may be necessary.
	public string? Header { get; init; } = null;
	public string? Footer { get; init; } = null;
}

class StringPages : Pages {
	// Additional instance properties.
	private readonly string? _header;
	private readonly string? _footer;

	// The interactable is registered to the table of `Pages` (and the
	// auto-discard timer starts running) only when the `DiscordMessage`
	// promise is fulfilled. (There is no separate table of instances
	// for `StringPages`.)
	public static StringPages Create(
		Interaction interaction,
		MessagePromise promise,
		IReadOnlyList<string> data,
		StringPagesOptions? options=null
	) {
		options ??= new ();

		// Construct partial (uninitialized) object.
		StringPages pages = new (
			options.IsEnabled,
			interaction,
			options.Timeout,
			new (data),
			options.PageSize,
			options.Decorator,
			options.Header,
			options.Footer
		);

		// Set up registration and auto-discard.
		pages.FinalizeInstance(promise);

		return pages;
	}

	// Since the protected constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	protected StringPages(
		bool isEnabled,
		Interaction interaction,
		TimeSpan timeout,
		List<string> data,
		int pageSize,
		Decorator? decorator,
		string? header,
		string? footer
	) : base (
		isEnabled,
		interaction,
		timeout,
		ToObjectList(data),
		pageSize,
		null!,
		decorator
	) {
		_header = header;
		_footer = footer;

		_renderer = RenderData;
	}

	// All `StringPages` render their underlying data the same way.
	private IDiscordMessageBuilder RenderData(
		IReadOnlyList<object> data,
		bool isEnabled
	) {
		string content = ToStringList(data).ToLines();

		if (_header is not null)
			content = $"{_header}\n{content}";
		if (_footer is not null)
			content = $"{content}\n{_footer}";

		return new DiscordMessageBuilder().WithContent(content);
	}

	// Convenience functions for converting the `_data` into/from what
	// the base `Pages` class expects.
	private static List<object> ToObjectList(IReadOnlyList<string> list) =>
		new List<string>(list).ConvertAll(s => (object)s);
	private static List<string> ToStringList(IReadOnlyList<object> list) =>
		new List<object>(list).ConvertAll(o => (string)o);
}
