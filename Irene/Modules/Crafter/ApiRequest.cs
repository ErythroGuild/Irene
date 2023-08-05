namespace Irene.Modules.Crafter;

using System.Net.Http;
using System.Text.Json.Nodes;

using static Types;

using ApiType = BlizzardClient.Namespace;
using Class = ClassSpec.Class;
using HttpStatusCode = System.Net.HttpStatusCode;
using TierSkill = Types.CharacterData.TierSkill;

class ApiRequest {
	// --------
	// Configuration and initialization:
	// --------

	// Client for handling low-level details of API calls.
	private static readonly BlizzardClient _client;

	// Paths for the Blizzard API.
	private const string
		_urlServers     = @"/data/wow/realm/index",                      // dynamic
		_urlServer      = @"/data/wow/realm/{0}",                        // dynamic
		_urlRoster      = @"/data/wow/guild/moon-guard/erythro/roster",  // profile
		_urlProfile     = @"/profile/wow/character/{1}/{0}",             // profile
		_urlProfessions = @"/profile/wow/character/{1}/{0}/professions", // profile
		_urlRecipe      = @"/data/wow/recipe/{0}";                       // static

	private const HttpStatusCode _statusNotFound = HttpStatusCode.NotFound;

	// Client authentication info.
	private const string _pathToken = @"secrets/blizzard.txt";
	private const string
		_keyClientId = "id: ",
		_keyClientSecret = "secret: ";

	static ApiRequest() {
		// Initialize client with authentication info.
		StreamReader file = File.OpenText(_pathToken);
		string? id = file.ReadLine();
		string? secret = file.ReadLine();
		file.Close();

		if (id is null || secret is null)
			throw new InvalidOperationException("Blizzard API auth info missing.");

		id = id[_keyClientId.Length..];
		secret = secret[_keyClientSecret.Length..];
		_client = new (id, secret);
	}


	// --------
	// Validity check methods:
	// --------
	// These methods check if the requested argument has a corresponding
	// valid source, according to the Blizzard API.

	// Takes the canonical name of the server as input (the method will
	// transform it into the appropriate server slug).
	public static async Task<bool> CheckIsValidServer(string server) {
		try {
			await RequestServerAsync(server);
			return true;
		} catch (HttpRequestException e) {
			if (e.StatusCode == _statusNotFound)
				return false;
			throw;
		}
	}
	public static async Task<bool> CheckIsValidCharacter(Character character) {
		try {
			await RequestProfileAsync(character);
			return true;
		} catch (HttpRequestException e) {
			if (e.StatusCode == _statusNotFound)
				return false;
			throw;
		}
	}


	// --------
	// API request methods:
	// --------
	// These methods return JSON strings that can be parsed directly
	// into `JsonNode`s for further processing.

	public static Task<string> RequestServersAsync() =>
		_client.RequestAsync(ApiType.Dynamic, _urlServers);
	// Uses the canonical server name (not the slug).
	public static Task<string> RequestServerAsync(string server) {
		string url = GetServerUrl(server);
		return _client.RequestAsync(ApiType.Dynamic, url);
	}
	public static Task<string> RequestRosterAsync() =>
		_client.RequestAsync(ApiType.Profile, _urlRoster);
	public static Task<string> RequestProfileAsync(Character character) {
		string url = GetProfileUrl(character);
		return _client.RequestAsync(ApiType.Profile, url);
	}
	public static Task<string> RequestProfessionsAsync(Character character) {
		string url = GetProfessionsUrl(character);
		return _client.RequestAsync(ApiType.Profile, url);
	}
	public static Task<string> RequestRecipeAsync(long id) {
		string url = GetRecipeUrl(id);
		return _client.RequestAsync(ApiType.Static, url);
	}


	// --------
	// JSON parsing methods:
	// --------

	// Returns a list of all servers, indexed by their IDs.
	public static ISet<string> ParseServersJson(string json) {
		HashSet<string> servers = new ();
		const string
			_keyServers = "realms",
			_keyName    = "name"  ;

		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		JsonNode node = Util.ParseSubnode(root, _keyServers);
		foreach (JsonNode? nodeServer in node.AsArray()) {
			if (nodeServer is null)
				continue;
			try {
				string name = Util.ParseString(nodeServer, _keyName);
				servers.Add(name);
			} catch (FormatException) { }
		}

		return servers;
	}
	// Returns a list of all guild members (only names, no servers).
	public static ISet<string> ParseRosterJson(string json) {
		HashSet<string> roster = new ();
		const string
			_keyMembers   = "members"  ,
			_keyCharacter = "character",
			_keyName      = "name"     ;

		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		JsonNode node = Util.ParseSubnode(root, _keyMembers);
		foreach (JsonNode? node_i in node.AsArray()) {
			if (node_i is null)
				continue;
			try {
				JsonNode nodeMember = Util.ParseSubnode(node_i, _keyCharacter);
				string name = Util.ParseString(nodeMember, _keyName);
				roster.Add(name);
			} catch (FormatException) { }
		}

		return roster;
	}
	// Returns the character class from a profile.
	public static Class ParseProfileJson(string json) {
		const string
			_keyClass = "character_class",
			_keyName  = "name"           ;

		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		string className = Util.ParseSubString(root, _keyClass, _keyName);

		return ParseClass(className)
			?? throw new FormatException();
	}
	// Returns the rank of recipe, if the recipe has ranks.
	// Returns null if it does not.
	public static int? ParseRecipeJson(string json) {
		const string _keyRank = "rank";

		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		// Check if the recipe has ranks.
		JsonNode? nodeRank = root[_keyRank];
		return nodeRank is null
			? null
			: Util.ParseInt(root, _keyRank);
	}

	public record class ParsedCharacterData(
		ConcurrentDictionary<Character, HashSet<ParsedProfessionData>> ProfessionData,
		ConcurrentDictionary<string, ItemData> ItemTable
	);
	public record class ParsedProfessionData(
		Profession Profession,
		ConcurrentDictionary<string, TierSkill> TierSkills
	);
	// Returns parsed data (a list of `CharacterData` + a compiled table
	// of `ItemData`) for a set of characters.
	// This can either be merged into the main tables or replaced.
	public static ParsedCharacterData ParseProfessionsJson(
		Character character,
		string json
	) {
		Dictionary<Character, string> jsonTable =
			new () { [character] = json };
		return ParseProfessionsJson(jsonTable);
	}
	public static ParsedCharacterData ParseProfessionsJson(
		IReadOnlyDictionary<Character, string> json
	) {
		ParsedCharacterData output = new (new (), new ());

		foreach (Character character in json.Keys) {
			ParseCharacterProfessionsJson(
				json[character],
				character,
				out HashSet<ParsedProfessionData> parsedProfessionData,
				output.ItemTable
			);
			output.ProfessionData.TryAdd(
				character,
				parsedProfessionData
			);
		}

		return output;
	}

	// Inner helper method: this should only ever be invoked by
	// `ParseProfessionsJson()`.
	// Note: The `itemData` parameter is modified with added data.
	private static void ParseCharacterProfessionsJson(
		string json,
		Character character,
		out HashSet<ParsedProfessionData> parsedProfessionData,
		ConcurrentDictionary<string, ItemData> itemData
	) {
		parsedProfessionData = new ();
		const string
			_keyPrimary   = "primaries"  ,
			_keySecondary = "secondaries";

		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		// Merge primary/secondary profession arrays into a single list.
		// These will be null if either primary or secondary professions
		// are completely missing, so null checks are necessary.
		JsonNode? nodeProfession;
		List<JsonNode?> nodesProfession = new ();
		nodeProfession = root[_keyPrimary];
		if (nodeProfession is not null)
			nodesProfession.AddRange(nodeProfession.AsArray());
		nodeProfession = root[_keySecondary];
		if (nodeProfession is not null)
			nodesProfession.AddRange(nodeProfession.AsArray());

		// Iterate through the combined list of nodes.
		foreach (JsonNode? node_i in nodesProfession) {
			if (node_i is null)
				continue;

			// Parse the actual profession JSON node; update fields.
			ParsedProfessionData? professionData =
				ParseProfessionJsonNode(
					node_i,
					character,
					itemData
				);

			// Skip any nodes that failed to parse.
			if (professionData is not null)
				parsedProfessionData.Add(professionData);
		}
	}
	// Inner helper method: this should only ever be invoked by
	// `ParseCharacterProfessionsJson()`.
	// Note: The `itemData` parameter is modified with added data.
	private static ParsedProfessionData? ParseProfessionJsonNode(
		JsonNode node,
		Character character,
		ConcurrentDictionary<string, ItemData> itemData
	) {
		const string
			_keyProfession = "profession"      ,
			_keyName       = "name"            ,
			_keyTiers      = "tiers"           ,
			_keyTier       = "tier"            ,
			_keySkill      = "skill_points"    ,
			_keySkillMax   = "max_skill_points",
			_keyRecipes    = "known_recipes"   ,
			_keyId         = "id"              ;

		// Determine which profession we're parsing.
		string professionName =
				Util.ParseSubString(node, _keyProfession, _keyName);
		Profession? profession = ParseProfession(professionName);
		if (profession is null)
			return null;

		// Create replacement `TierSkill` data to reassign later.
		ConcurrentDictionary<string, TierSkill> tierSkills = new ();

		// Parse through each tier of the profession.
		JsonNode nodeTiers = Util.ParseSubnode(node, _keyTiers);
		foreach (JsonNode? nodeTier in nodeTiers.AsArray()) {
			if (nodeTier is null)
				continue;

			// Add `TierSkill` entry for the current tier.
			string tier = Util.ParseSubString(nodeTier, _keyTier, _keyName);
			int skill = Util.ParseInt(nodeTier, _keySkill);
			int skillMax = Util.ParseInt(nodeTier, _keySkillMax);
			tierSkills.TryAdd(tier, new (skill, skillMax));

			// Parse through each item.
			// (Sometimes a tier has no known recipes.)
			if (nodeTier[_keyRecipes] is null)
				continue;
			JsonNode nodeItems = Util.ParseSubnode(nodeTier, _keyRecipes);
			foreach (JsonNode? nodeItem in nodeItems.AsArray()) {
				if (nodeItem is null)
					continue;

				string item = Util.ParseString(nodeItem, _keyName);
				long id = Util.ParseLong(nodeItem, _keyId);

				// Ensure an entry for the item exists in the data table
				// before registering the current crafter.
				if (!itemData.ContainsKey(item)) {
					itemData.TryAdd(
						item,
						new (item, profession.Value, tier)
					);
				}
				// Update the current crafter in the `itemData` table.
				itemData[item].SetCrafter(character, id);
			}
		}

		return new (profession.Value, tierSkills);
	}
	

	// --------
	// Miscellaneous (small) helper methods:
	// --------

	// Convert a JSON key value to the actual `ClassSpec.Class` enum.
	// Returns null if not a recognized class.
	private static Class? ParseClass(string jsonKey) => jsonKey switch {
		"Death Knight" => Class.DK,
		"Demon Hunter" => Class.DH,
		"Druid"   => Class.Druid  ,
		"Evoker"  => Class.Evoker ,
		"Hunter"  => Class.Hunter ,
		"Mage"    => Class.Mage   ,
		"Monk"    => Class.Monk   ,
		"Paladin" => Class.Paladin,
		"Priest"  => Class.Priest ,
		"Rogue"   => Class.Rogue  ,
		"Shaman"  => Class.Shaman ,
		"Warlock" => Class.Warlock,
		"Warrior" => Class.Warrior,
		_ => null,
	};
	// Convert a JSON key value to the actual `Crafter.Profession` enum.
	// Returns null if not one of the supported crafting professions.
	private static Profession? ParseProfession(string jsonKey) => jsonKey switch {
		"Cooking"        => Profession.Cooking       ,
		"Alchemy"        => Profession.Alchemy       ,
		"Jewelcrafting"  => Profession.Jewelcrafting ,
		"Enchanting"     => Profession.Enchanting    ,
		"Engineering"    => Profession.Engineering   ,
		"Inscription"    => Profession.Inscription   ,
		"Blacksmithing"  => Profession.Blacksmithing ,
		"Leatherworking" => Profession.Leatherworking,
		"Tailoring"      => Profession.Tailoring     ,
		_ => null,
	};

	// Populate the server API URL with the given server.
	// This uses the canonical server name, not the slug.
	private static string GetServerUrl(string server) =>
		string.Format(_urlServer, ServerNameToSlug(server));
	// Populate the profile API URL with the character's name/server.
	private static string GetProfileUrl(Character name) =>
		string.Format(
			_urlProfile,
			name.Name.ToLower(),
			ServerNameToSlug(name.Server)
		);
	// Populate the professions API URL with the character's name/server.
	private static string GetProfessionsUrl(Character name) =>
		string.Format(
			_urlProfessions,
			name.Name.ToLower(),
			ServerNameToSlug(name.Server)
		);
	// Populate the recipe API URL with the given recipe ID.
	private static string GetRecipeUrl(long id) =>
		string.Format(_urlRecipe, id);
}
