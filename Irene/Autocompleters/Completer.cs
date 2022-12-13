namespace Irene.Autocompleters;

class Completer {
	public delegate Task<IReadOnlyList<(string, string)>> AllOptionsHandler(
		string arg,
		ParsedArgs args,
		Interaction interaction
	);
	public delegate IReadOnlyList<(string, string)> DefaultOptionsHandler(
		ParsedArgs args,
		Interaction interaction
	);

	public int MaxOptions { get; }
	public const int MaxOptionsDefault = 16;

	protected AllOptionsHandler GetOptionsAll { get; init; }
	protected DefaultOptionsHandler GetOptionsDefault { get; init; }

	// Leaving the default options handler as null will result in an
	// empty list for the default options.
	// Note: An alternate constructor isn't provided for derived classes,
	// so those must explicitly set temporary handlers (e.g. empty lambdas)
	// and then remember to override them.
	public Completer(
		AllOptionsHandler getOptionsAll,
		DefaultOptionsHandler? getOptionsDefault,
		int maxOptions=MaxOptionsDefault
	) {
		// If default options are null/unspecified, just have it return
		// an empty list.
		getOptionsDefault ??= (_, _) => new List<(string, string)>();

		MaxOptions = maxOptions;
		GetOptionsDefault = getOptionsDefault;
		GetOptionsAll = getOptionsAll;
	}

	public async Task<IList<(string, string)>> GetOptions(
		string arg,
		ParsedArgs args,
		Interaction interaction
	) {
		// Return default options when string is empty.
		arg = arg.Trim();
		if (arg == "") {
			List<(string, string)> optionsDefault =
				new (GetOptionsDefault.Invoke(args, interaction));

			// Limit option count.
			if (optionsDefault.Count > MaxOptions)
				optionsDefault = optionsDefault.GetRange(0, MaxOptions);

			return optionsDefault;
		}

		// Fetch all options.
		List<(string, string)> options =
			new (await GetOptionsAll.Invoke(arg, args, interaction));
		
		// Limit option count.
		if (options.Count > MaxOptions)
			options = options.GetRange(0, MaxOptions);

		return options;
	}
}
