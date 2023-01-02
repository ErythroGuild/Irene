namespace Irene.Modules.Crafter;

using Class = ClassSpec.Class;

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

		// Serialization/deserialization methods.
		// The deserialization method accepts an input with no separator
		// (no server) as having that name + the default server.
		private const string _separator = ", ";
		public static Character FromString(string input) {
			string[] split = input.Split(_separator, 2);
			return (split.Length > 1)
				? new (split[0], split[1])
				: new (split[0]);
		}
		public override string ToString() => $"{Name}{_separator}{Server}";
	}

	public record class CharacterData {
		// Basic properties.
		public readonly ulong UserId;
		public readonly Character Character;
		public readonly Class Class;

		// Properties and convenience methods for accessing profession
		// data (and related queries), on the whole.
		private IReadOnlyDictionary<Profession, ProfessionData> _professions;
		public IReadOnlySet<Profession> Professions =>
			new HashSet<Profession>(_professions.Keys);
		public bool HasProfession(Profession profession) =>
			_professions.ContainsKey(profession);
		public ProfessionData GetProfessionData(Profession profession) =>
			_professions[profession];
		public IReadOnlyDictionary<Profession, ProfessionData> GetProfessionData() =>
			_professions;
		public void SetProfessions(ConcurrentDictionary<Profession, ProfessionData> professions) =>
			_professions = professions;

		// Convenience methods for accessing specific profession data
		// properties (summary, tier skill).
		public string GetSummary(Profession profession) =>
			_professions[profession].Summary;
		public void SetSummary(Profession profession, string summary) =>
			_professions[profession].Summary = summary;
		public TierSkill GetSkill(Profession profession, string tier) =>
			_professions[profession].GetSkill(tier);
		// Not including set skill since that's an uncommon operation,
		// and should only happen on character database updates.

		public CharacterData(
			ulong userId,
			Character character,
			Class @class,
			ConcurrentDictionary<Profession, ProfessionData>? professions=null
		) {
			professions ??= new ();

			UserId = userId;
			Character = character;
			Class = @class;
			_professions = professions;
		}

		// Serialization/deserialization methods.
		public const string Indent = "\t";
		private const string _separator = " | ";
		// The input data is trimmed, so it can be left indented (as-read).
		public static CharacterData Deserialize(ulong userId, List<string> lines) {
			// Parse character data.
			string[] split = lines[0].Trim().Split(_separator, 2);
			Class @class = Enum.Parse<Class>(split[0]);
			Character character = Character.FromString(split[1]);

			// Parse all profession data.
			ConcurrentDictionary<Profession, ProfessionData> professions = new ();
			for (int i=1; i<lines.Count; i++) {
				ProfessionData professionData =
					ProfessionData.FromString(lines[i].Trim());
				professions.TryAdd(professionData.Profession, professionData);
			}

			return new (
				userId,
				character,
				@class
			) { _professions = professions };
		}
		// Returns a properly-indented, ready-for-collation serialization
		// of character data. (Does not include owner's user ID.)
		public List<string> Serialize() {
			List<string> lines = new ()
				{ $"{Indent}{Class}{_separator}{Character}" };

			// Sort all data (for output stability).
			List<ProfessionData> professions = new (_professions.Values);
			professions.Sort((p1, p2) => p1.Profession - p2.Profession);

			foreach (ProfessionData profession in professions)
				lines.Add($"{Indent}{Indent}{profession}");

			return lines;
		}

		public record class ProfessionData {
			public readonly Profession Profession;
			public string Summary {
				get => _summary;
				set => _summary = value.Trim();
			}
			private string _summary = "";
			
			// Methods for accessing tier skill values.
			private IReadOnlyDictionary<string, TierSkill> _skills =
				new ConcurrentDictionary<string, TierSkill>();
			public TierSkill GetSkill(string tier) => _skills[tier];
			public void SetSkills(ConcurrentDictionary<string, TierSkill> skill) =>
				_skills = skill;

			public ProfessionData(Profession profession, string summary="") {
				Profession = profession;
				Summary = summary;
			}

			// Serialization/deserialization methods.
			private const string _separator = ": ";
			public static ProfessionData FromString(string input) {
				string[] split = input.Trim().Split(_separator, 2);
				Profession profession = Enum.Parse<Profession>(split[0]);
				string summary = (split.Length > 1) ? split[1] : "";
				return new (profession, summary);
			}
			public override string ToString() =>
				$"{Profession}{_separator}{Summary}";
		}

		public readonly record struct TierSkill(int Skill, int SkillMax) {
			public override string ToString() => $"{Skill}/{SkillMax}";
		}
	}

	public record class ItemData {
		public readonly string Name;
		public readonly Profession Profession;
		public readonly string ProfessionTier;

		// Properties and methods for accessing crafter data.
		// The values in this table are the recipe IDs of the crafters'
		// known recipes, which may have different ranks.
		private ConcurrentDictionary<Character, long> _crafters = new ();
		public IReadOnlySet<Character> Crafters =>
			new HashSet<Character>(_crafters.Keys);
		public long GetCrafterRecipeId(Character crafter) =>
			_crafters[crafter];
		public void SetCrafter(Character crafter, long recipeId) =>
			_crafters[crafter] = recipeId;
		public void RemoveCrafter(Character crafter) =>
			_crafters.TryRemove(crafter, out _);

		public ItemData(string name, Profession profession, string professionTier) {
			Name = name;
			Profession = profession;
			ProfessionTier = professionTier;
		}
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
