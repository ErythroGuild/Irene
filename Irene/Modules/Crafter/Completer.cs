namespace Irene.Modules.Crafter;

using static Types;

using Autocompleter = Autocompleters.Completer;

class Completer {
	public static readonly Autocompleter Items =
		new StringCompleter(
			(_, _) => GetItems(),
			(_, _) => GetRandomItems(),
			18
		);
	public static readonly Autocompleter Roster =
		new StringCompleter(
			(_, _) => GetRoster(),
			(_, _) => GetRandomCharacters(),
			18
		);
	public static readonly Autocompleter Crafters =
		new StringCompleter(
			(args, interaction) => GetCrafters(interaction.User.Id),
			(args, interaction) => GetCrafters(interaction.User.Id),
			12
		);
	public static readonly Autocompleter Servers =
		new StringCompleter(
			(_, _) => GetServers(),
			(_, _) => GetDefaultServers(),
			12
		);

	// Default options.
	private static readonly List<string> _defaultServers = new () {
		"Moon Guard",
		"Wyrmrest Accord",
		// Alphabetize the rest.
		"Blackrock",
		"Cenarius",
		"Dalaran",
		"Darkspear",
		"Elune",
		"Emerald Dream",
		"Korgath",
		"Moonrunner",
		"Terokkar",
		"Tichondrius",
	};

	// Implementation of the `Items` completer.
	private static IReadOnlyList<string> GetItems() =>
		new List<string>(Database.GetItems()); // no need for sorting
	private static IReadOnlyList<string> GetRandomItems() {
		List<string> items = new (Database.GetItems());

		// Generate random indices.
		HashSet<int> indices = new ();
		for (int i=0; i<12; i++) {
			int i_items =
				(int)Random.RandomWithFallback(0, items.Count);
			while (indices.Contains(i_items))
				i_items++;
			indices.Add(i_items);
		}

		// Convert indices to actual characters.
		List<string> options = new ();
		foreach (int i in indices)
			options.Add(items[i]);

		return options;
	}

	// Implementation of the `Roster` completer.
	private static IReadOnlyList<string> GetRoster() => Database.Roster;
	private static IReadOnlyList<string> GetRandomCharacters() {
		// Generate random indices.
		HashSet<int> indices = new ();
		for (int i = 0; i<4; i++) {
			int i_roster =
				(int)Random.RandomWithFallback(0, Database.Roster.Count);
			while (indices.Contains(i_roster))
				i_roster++;
			indices.Add(i_roster);
		}

		// Convert indices to actual characters.
		List<string> options = new ();
		foreach (int i in indices)
			options.Add(Database.Roster[i]);

		return options;
	}

	// Implementation of the `Crafters` completer.
	private static IReadOnlyList<string> GetCrafters(ulong id) {
		List<string> crafterNames = new ();
		foreach (Character crafter in Database.GetCrafters(id))
			crafterNames.Add(crafter.LocalName());
		crafterNames.Sort();
		return crafterNames;
	}

	// Implementation of the `Servers` completer.
	private static IReadOnlyList<string> GetServers() => Database.Servers;
	private static IReadOnlyList<string> GetDefaultServers() => _defaultServers;
}
