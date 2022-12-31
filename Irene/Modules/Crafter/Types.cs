namespace Irene.Modules.Crafter;

// These are all within a single class, since even if the types are in
// the namespace itself, a `using static` directive would still be needed
// in order to use the utility functions. It makes sense to just always
// require the `using static Modules.Crafter.Types;` declaration.

static class Types {
	public const string ServerGuild = "Moon Guard";


	// --------
	// Type definitions:
	// --------

	// The declaration order is also the sort order for the enum.
	public enum Profession {
		Cooking,
		Alchemy,
		Jewelcrafting, Enchanting,
		Engineering, Inscription,
		Blacksmithing, Leatherworking, Tailoring,
	}

	public readonly record struct Character {
		public readonly string Name;
		public readonly string Server;

		// All instances have proper capitalization of the name and only
		// allow valid server names (an exception is thrown if otherwise).
		public Character(string name, string server=ServerGuild) {
			name = name.Trim().ToLower();
			if (name.Length == 0)
				throw new ArgumentException("Name must not be empty.", nameof(name));

			// Properly capitalize name.
			name = (name.Length == 1)
				? char.ToUpper(name[0]).ToString()
				: char.ToUpper(name[0]) + name[1..];
			Name = name;

			// Select server from list.
			server = server.Trim().ToLower();
			bool foundServer = false;
			foreach (string server_i in _servers) {
				if (server == server_i.ToLower()) {
					foundServer = true;
					server = server_i;
					break;
				}
			}
			if (!foundServer)
				throw new ArgumentException("Invalid server.", nameof(server));
			Server = server;
		}

		// Indicate (and optionally show/don't show) when a character
		// isn't from the default server.
		// `doEscape` escapes the '*' (if present) with a backslash.
		public string LocalName(bool doAbbreviate=false, bool doEscape=false) {
			if (Server == ServerGuild)
				return Name;

			string append = doAbbreviate
				? (doEscape ? " (\\*)" : " (*)")
				: $"-{ServerNameToNormalized(Server)}";
			return Name + append;
		}

		private const string _separator = ", ";

		// Serialization/deserialization methods.
		// The deserialization method accepts an input with no separator
		// (no server) as having that name + the default server.
		public static Character FromString(string input) {
			string[] split = input.Split(_separator, 2);
			return (split.Length > 1)
				? new (split[0], split[1])
				: new (split[0]);
		}
		public override string ToString() => $"{Name}{_separator}{Server}";
	}


	// --------
	// Utility methods:
	// --------

	// The job title of somebody with this profession.
	public static string Title(Profession profession) => profession switch {
		Profession.Cooking        => "Cook"         ,
		Profession.Alchemy        => "Alchemist"    ,
		Profession.Jewelcrafting  => "Jewelcrafter" ,
		Profession.Enchanting     => "Enchanter"    ,
		Profession.Engineering    => "Engineer"     ,
		Profession.Inscription    => "Scribe"       ,
		Profession.Blacksmithing  => "Blacksmith"   ,
		Profession.Leatherworking => "Leatherworker",
		Profession.Tailoring      => "Tailor"       ,
		_ => throw new UnclosedEnumException(typeof(Profession), profession),
	};

	// Convert a server name into the in-game character name format.
	public static string ServerNameToNormalized(string name) =>
		name.Replace(" ", "")
			.Replace("-", "");
	// Converts a server name into the Blizzard API slug format.
	public static string ServerNameToSlug(string name) =>
		name.ToLower()
			.Replace(" ", "-")
			.Replace("'", "");
}
