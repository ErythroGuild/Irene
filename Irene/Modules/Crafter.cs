namespace Irene.Modules;

using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Timers;

using ApiNamespace = BlizzardClient.Namespace;
using Class = ClassSpec.Class;
using TierSkillDictionary = ConcurrentDictionary<string, Crafter.TierSkill>;

class Crafter {
	// --------
	// Data structures:
	// --------

	public enum Profession {
		Cooking, Alchemy,
		Jewelcrafting, Enchanting,
		Engineering, Inscription,
		Blacksmithing, Leatherworking, Tailoring,
	}

	// Profession data.
	public readonly record struct TierSkill(int Skill, int SkillMax);
	private record class ProfessionData {
		public readonly Profession Profession;
		public string Summary;
		public IReadOnlyDictionary<string, TierSkill> Tiers =
			new TierSkillDictionary();

		public ProfessionData(Profession profession, string summary = "") {
			Profession = profession;
			Summary = summary;
		}

		public static ProfessionData FromString(string input) {
			string[] split = input.Split(_separator, 2);
			if (split.Length < 2)
				throw new FormatException("Invalid profession data format: no separator found.");

			try {
				Profession profession = Enum.Parse<Profession>(split[0]);
				return new ProfessionData(profession, split[1]);
			} catch (ArgumentException e) {
				throw new FormatException("Invalid profession data format: invalid profession type.", e);
			}
		}
		public override string ToString() =>
			string.Join(_separator, Profession, Summary);
	}
	// Character data.
	public readonly record struct Character {
		public readonly string Name;
		public readonly string Server;

		public Character(string name, string server) {
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
	}
	private record class CharacterData {
		public readonly ulong UserId;
		public readonly Character Character;
		public readonly Class Class;
		public IReadOnlyDictionary<Profession, ProfessionData> Professions =
			new ConcurrentDictionary<Profession, ProfessionData>();

		public CharacterData(ulong userId, Character character, Class @class) {
			UserId = userId;
			Character = character;
			Class = @class;
		}

		// Expects the data to be properly indented (once for the per-
		// character data, and twice for the profession data).
		public static CharacterData Deserialize(ulong userId, List<string> lines) {
			// Parse character data.
			string[] split = lines[0].Trim().Split(_separator, 3);
			Class @class = Enum.Parse<Class>(split[0]);
			string name = split[1];
			string server = split[2];

			// Parse all profession data.
			ConcurrentDictionary<Profession, ProfessionData> professions = new ();
			for (int i=1; i<lines.Count; i++) {
				ProfessionData professionData =
					ProfessionData.FromString(lines[i].Trim());
				professions.TryAdd(professionData.Profession, professionData);
			}

			// Construct return object.
			return new (
				userId,
				new (name, server),
				@class
			) { Professions = professions };
		}
		// Returns a properly-indented, ready-for-collation serialization
		// of character data.
		public List<string> Serialize() {
			List<string> lines = new ()
				{ $"{_indent}{CharacterDataString()}" };

			foreach (ProfessionData profession in Professions.Values)
				lines.Add($"{_indent}{_indent}{profession}");

			return lines;
		}
		// The first line of the serialized character data (no indents).
		private string CharacterDataString() =>
			string.Join(_separator, Class, Character.Name, Character.Server);
	}
	// Item data.
	private record class ItemData {
		public readonly string Name;
		public readonly Profession Profession;
		public readonly string ProfessionTier;
		// The value of this table maps to the recipe ID.
		public ConcurrentDictionary<Character, long> CrafterRecipes = new ();

		public ItemData(string name, Profession profession, string professionTier) {
			Name = name;
			Profession = profession;
			ProfessionTier = professionTier;
		}
	}


	// --------
	// Properties, fields, and constants:
	// --------

	// Static status indicator for data updates.
	// Not thread-safe; should only be modified behind a `TaskQueue`.
	public static DateTimeOffset LastUpdated { get; private set; }

	public const string ServerDefault = "Moon Guard";

	// Default autocomplete options.
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

	// Timer to automatically refresh database.
	private static readonly Timer
		_timerItemData,
		_timerRoster;
	private static readonly TimeSpan
		_intervalItemDataRefresh = TimeSpan.FromMinutes(90), // 1.5 hours
		_intervalRosterRefresh = TimeSpan.FromMinutes(45);

	// Master list of valid servers, populated from API query.
	private static readonly IReadOnlyList<string> _servers;
	// Master list of all guild members. Stored as strings since this
	// is only used to populate a `StringCompleter`.
	private static IReadOnlyList<string> _roster = new List<string>();

	// Master table of crafters, indexed by the name of the item.
	// (`ItemData` includes crafter lists.)
	private static ConcurrentDictionary<string, ItemData> _itemCrafters = new ();
	// Master table associating each crafter to their profession data.
	private static ConcurrentDictionary<Character, CharacterData> _crafterData = new ();
	// Master table of crafters, indexed by the user ID of the player
	// who owns the crafters in that list.
	private static ConcurrentDictionary<ulong, IReadOnlyList<Character>> _playerCrafters = new ();
	// Cache of which ranks each recipe maps to. This only needs to be
	// populated as the recipes are actually fetched.
	private static readonly ConcurrentDictionary<long, int?> _recipeRanks = new ();

	// Client for handling low-level details of Blizzard API calls.
	private static readonly BlizzardClient _client;
	// Queues to control access to data.
	private static readonly TaskQueue
		_queueFile = new (),
		_queueItemData = new ();

	// Configuration constants.
	private const string
		_pathToken = @"config/blizzard.txt",
		_pathCharacters = @"data/crafters.txt";
	private const string
		_keyClientId = "id: ",
		_keyClientSecret = "secret: ";
	private const string
		_separator = ">>>",
		_indent = "\t";
	// Paths for Blizzard API.
	private const string
		_urlServers = @"/data/wow/realm/index", // dynamic
		_urlRoster = @"/data/wow/guild/moon-guard/erythro/roster", // profile
		_urlProfile = @"/profile/wow/character/{1}/{0}", // profile
		_urlProfessions = @"/profile/wow/character/{1}/{0}/professions", // profile
		_urlRecipe = @"/data/wow/recipe/{0}"; // static;
	// Keys for JSON deserialization.
	private const string
		_keyServers    = "realms"          ,
		_keyName       = "name"            ,
		_keyMembers    = "members"         ,
		_keyCharacter  = "character"       ,
		_keyClass      = "character_class" ,
		_keyPrimary    = "primaries"       ,
		_keySecondary  = "secondaries"     ,
		_keyProfession = "profession"      ,
		_keyTiers      = "tiers"           ,
		_keyTier       = "tier"            ,
		_keySkill      = "skill_points"    ,
		_keySkillMax   = "max_skill_points",
		_keyRecipes    = "known_recipes"   ,
		_keyId         = "id"              ,
		_keyRank       = "rank"            ;


	// --------
	// Initialization:
	// --------

	static Crafter() {
		// Initialize client with authentication info.
		StreamReader file = File.OpenText(_pathToken);
		string? id = file.ReadLine();
		string? secret = file.ReadLine();
		file.Close();

		if (id is null || secret is null)
			throw new InvalidOperationException("Blizzard API auth info missing.");

		id = id[_keyClientId.Length..];
		secret = secret[_keyClientSecret.Length..];
		_client = new(id, secret);

		// Initialize server list from API query.
		string serversJson = RequestServersAsync().Result;
		List<string> servers = new (ParseServersJson(serversJson));
		servers.Sort();
		_servers = servers;

		// Initialize guild roster from API query.
		_ = UpdateRosterAsync();

		// Initialize database, first fetching all saved character data.
		// This is threadsafe and can happen in the background.
		Util.CreateIfMissing(_pathCharacters);
		_ = ReadCrafterDataFromFile()
			.ContinueWith(t => RebuildItemDatabaseAsync());

		// Initialize guild roster auto-update timer.
		Timer timerRoster = Util.CreateTimer(_intervalRosterRefresh, true);
		timerRoster.Elapsed += (_, _) => _ = UpdateRosterAsync();
		_timerRoster = timerRoster;
		_timerRoster.Start();

		// Initialize database auto-update timer.
		Timer timerItemData = Util.CreateTimer(_intervalItemDataRefresh, true);
		timerItemData.Elapsed += (_, _) => _ = RebuildItemDatabaseAsync();
		_timerItemData = timerItemData;
		_timerItemData.Start();
	}


	// --------
	// Autocompleters:
	// --------

	public static readonly Completer CompleterItem = new StringCompleter(
		(_, _) => GetItems(),
		(_, _) => GetRandomItems(),
		18
	);
	public static readonly Completer CompleterRoster = new StringCompleter(
		(_, _) => GetRoster(),
		(_, _) => GetRandomCharacters(),
		18
	);
	public static readonly Completer CompleterCrafter = new StringCompleter(
		(args, interaction) => GetCrafters(interaction.User.Id),
		(args, interaction) => GetCrafters(interaction.User.Id),
		12
	);
	public static readonly Completer CompleterServer = new StringCompleter(
		(_, _) => GetServers(),
		(_, _) => GetDefaultServers(),
		12
	);
	private static IReadOnlyList<string> GetItems() =>
		new List<string>(_itemCrafters.Keys);
	private static IReadOnlyList<string> GetRandomItems() {
		// Generate random indices.
		HashSet<int> indices = new ();
		for (int i=0; i<12; i++) {
			int i_items =
				(int)Random.RandomWithFallback(0, _itemCrafters.Count);
			while (indices.Contains(i_items))
				i_items++;
			indices.Add(i_items);
		}

		// Convert indices to actual characters.
		List<string> options = new ();
		List<string> items = new (_itemCrafters.Keys);
		foreach (int i in indices)
			options.Add(items[i]);

		return options;
	}
	private static IReadOnlyList<string> GetRoster() => _roster;
	private static IReadOnlyList<string> GetRandomCharacters() {
		// Generate random indices.
		HashSet<int> indices = new ();
		for (int i = 0; i<4; i++) {
			int i_roster =
				(int)Random.RandomWithFallback(0, _roster.Count);
			while (indices.Contains(i_roster))
				i_roster++;
			indices.Add(i_roster);
		}

		// Convert indices to actual characters.
		List<string> options = new ();
		foreach (int i in indices)
			options.Add(_roster[i]);

		return options;
	}
	private static IReadOnlyList<string> GetCrafters(ulong id) {
		List<string> crafterNames = new ();
		foreach (Character crafter in _playerCrafters[id])
			crafterNames.Add(crafter.Name);
		return crafterNames;
	}
	private static IReadOnlyList<string> GetServers() => _servers;
	private static IReadOnlyList<string> GetDefaultServers() => _defaultServers;


	// --------
	// Interaction response methods:
	// --------

	public static async Task RespondListAsync(Interaction interaction, Profession profession) {
		CheckErythroInit();

		string title = GetProfessionTitle(profession);

		// Compile list of crafters of the requested profession.
		List<CharacterData> crafters = new ();
		foreach (CharacterData character in _crafterData.Values) {
			if (character.Professions.ContainsKey(profession))
				crafters.Add(character);
		}

		// Handle empty case.
		if (crafters.Count == 0) {
			string responseNone =
				$"""
				:desert: Sorry, there aren't any registered {title}s yet.
				If you know one, maybe ask them to register?
				""";
			await interaction.RegisterAndRespondAsync(responseNone, true);
			return;
		}

		// Sort characters.
		crafters.Sort(
			(a, b) => a.Character.Name.CompareTo(b.Character.Name)
		);

		// Convert list to strings.
		DiscordEmoji quality = Erythro.Emoji(id_e.quality5);
		const string
			_emDash = "\u2014",
			_zwSpace = "\u200B",
			_enSpace = "\u2002",
			_emSpace = "\u2003";
		string heading =
			$"{_zwSpace}{_enSpace}{quality}{_enSpace}__**{title}s**__{_enSpace}{quality}\n";
		List<string> lines = new ();
		foreach (CharacterData crafter in crafters) {
			DiscordEmoji @class = crafter.Class.Emoji();
			string name = GetServerLocalName(crafter.Character);
			string mention = crafter.UserId.MentionUserId();
			string entry =
				$"""
				{@class}{_enSpace}**{name}**
				{_emSpace}{_emSpace}{_enSpace}{_emDash} {mention}
				""";
			lines.Add(entry);
		}
		
		// Respond with list of crafters.
		MessagePromise messagePromise = new ();
		StringPages pages = StringPages.Create(
			interaction,
			messagePromise,
			lines,
			new StringPagesOptions {
				PageSize = 6,
				Header = heading,
			}
		);
		DiscordMessageBuilder response = pages
			.GetContentAsBuilder()
			.WithAllowedMentions(Mentions.None);

		string summary = "Crafter list sent.";
		await interaction.RegisterAndRespondAsync(
			response,
			summary
		);

		DiscordMessage message = await interaction.GetResponseAsync();
		messagePromise.SetResult(message);
	}

	public static async Task RespondFindAsync(Interaction interaction, string item) {
		// Send initial loading bar.
		string loading =
			$"""
			Searching database...
			{ProgressBar.Get(2, 6)}
			""";
		await interaction.RegisterAndRespondAsync(loading);

		// Handle empty case.
		if (!_itemCrafters.ContainsKey(item) ||
			_itemCrafters[item].CrafterRecipes.IsEmpty
		) {
			string responseNone =
				$"""
				:desert: Sorry, I couldn't find any registered crafters for **{item}**.
				If you know any crafters, maybe ask them to register?
				""";
			await interaction.EditResponseAsync(responseNone);
			return;
		}

		ItemData data = _itemCrafters[item];

		// Format header.
		string tier = data.ProfessionTier;
		const string
			enDash  = "\u2013",
			enSpace = "\u2002",
			emSpace = "\u2003";
		string heading =
			$"""
			__**{item}**__ {enDash} {tier}
			{emSpace}*refreshed {LastUpdated.Timestamp(Util.TimestampStyle.Relative)}*

			""";

		// Update progress bar.
		loading =
			$"""
			Checking recipes...
			{ProgressBar.Get(4, 6)}
			""";
		await interaction.EditResponseAsync(loading);

		// Check for recipe ranks.
		HashSet<long> recipes = new (data.CrafterRecipes.Values);
		bool hasRanks = false;
		foreach (long recipe in recipes) {
			if (!_recipeRanks.ContainsKey(recipe)) {
				string recipeJson = await RequestRecipeAsync(recipe);
				int? rank = ParseRecipeJson(recipeJson);
				_recipeRanks.TryAdd(recipe, rank);
			}
			hasRanks |= _recipeRanks[recipe] is not null;
		}

		// Update progress bar.
		loading =
			$"""
			Compiling results...
			{ProgressBar.Get(5, 6)}
			""";
		await interaction.EditResponseAsync(loading);

		// Sort results.
		List<Character> crafters = new (data.CrafterRecipes.Keys);
		crafters.Sort((a, b) => CrafterRankComparer(
			a, b, hasRanks, data
		));

		// Compile results.
		List<string> lines = new (); ;
		foreach (Character crafter in crafters) {
			// Character data.
			CharacterData crafterData = _crafterData[crafter];
			DiscordEmoji @class = crafterData.Class.Emoji();
			string name = GetServerLocalName(crafter);
			string mention = Util.MentionUserId(crafterData.UserId);

			// Profession data.
			ProfessionData professionData =
				crafterData.Professions[data.Profession];
			TierSkill skill = professionData.Tiers[tier];
			double skillProgress =
				skill.Skill / (double)skill.SkillMax;
			string bar = ProgressBar.Get(skillProgress, 5);

			// Recipe rank data.
			long recipe = data.CrafterRecipes[crafter];
			string stars = hasRanks
				? $"\n{emSpace}{GetRecipeRankStars(_recipeRanks[recipe])}"
				: "";

			// Summary string.
			string summary = (professionData.Summary == "")
				? ""
				: $"\n> *{professionData.Summary}*";

			// Format output.
			string entry =
				$"""
				{@class} **{name}** {enDash} {mention}{stars}
				{bar}{enSpace}{skill.Skill} / {skill.SkillMax}{summary}

				""";
			lines.Add(entry);
		}

		// Respond with results.
		MessagePromise messagePromise = new ();
		StringPages pages = StringPages.Create(
			interaction,
			messagePromise,
			lines,
			new StringPagesOptions {
				PageSize = 3,
				Header = heading,
			}
		);

		DiscordMessageBuilder response = pages
			.GetContentAsBuilder()
			.WithAllowedMentions(Mentions.None);
		DiscordMessage message = await
			interaction.EditResponseAsync(response);

		messagePromise.SetResult(message);
		Log.Information("  Crafter search results sent.");
	}

	public static async Task RespondSetAsync(Interaction interaction, Character character) {
		ulong id = interaction.User.Id;

		// Check that the character isn't already registered.
		if (_crafterData.ContainsKey(character)) {
			string errorExists =
				$"""
				Sorry, **{GetServerLocalName(character)}** is already registered.
				""";
			await interaction.RegisterAndRespondAsync(errorExists, true);
			return;
		}

		// Check that the character is valid.
		try {
			string profileJson = await RequestProfileAsync(character);
		} catch (HttpRequestException e) {
			if (e.StatusCode == System.Net.HttpStatusCode.NotFound) {
				string errorNoCharacter =
					$"""
					Sorry, I couldn't find info for **{GetServerLocalName(character)}**.
					If the name is correct, Blizzard servers may be out of date.
					Relog, wait a few minutes, and try again? :stopwatch:
					""";
				await interaction.RegisterAndRespondAsync(errorNoCharacter, true);
				return;
			}
		}

		// Notify user that command was received.
		string response = "Registering new character...";
		await interaction.RegisterAndRespondAsync(response);

		await AddCharacterAsync(id, character);
		CharacterData data = _crafterData[character];
		response =
			$"""
			Registered new character:
			{data.Class.Emoji()} **{GetServerLocalName(character)}**
			""";
		await interaction.EditResponseAsync(response);
	}

	//public static async Task RespondRefreshAsync(Interaction interaction, Character character) {
	//	// Ensure user owns the character.
	//	ulong id = interaction.User.Id;
	//	if (!_playerCrafters.ContainsKey(id))
	//		_playerCrafters.TryAdd(id, new List<Character>());
	//	List<Character> characters = new (_playerCrafters[id]);

	//	// Respond if user is not the owner.
	//	if (!characters.Contains(character)) {
	//		string responseNotOwner =
	//			$"""
	//			You don't have a crafter named **{GetServerLocalName(character)}**.
	//			:giraffe: *(Crafters can only be updated by their owners.)*
	//			""";
	//		await interaction.RegisterAndRespondAsync(responseNotOwner, true);
	//	}
	//}

	public static async Task RespondRemoveAsync(Interaction interaction, Character character) {
		// Ensure user owns the character.
		ulong id = interaction.User.Id;
		if (!_playerCrafters.ContainsKey(id))
			_playerCrafters.TryAdd(id, new List<Character>());
		List<Character> characters = new (_playerCrafters[id]);

		// Respond if user is not the owner.
		if (!characters.Contains(character)) {
			string responseNotOwner =
				$"""
				You don't have a crafter named **{GetServerLocalName(character)}**.
				:giraffe: *(Crafters can only be removed by their owners.)*
				""";
			await interaction.RegisterAndRespondAsync(responseNotOwner, true);
		}

		// Preview character data.
		CharacterData data = _crafterData[character];
		string preview =
			$"{data.Class.Emoji()} **{GetServerLocalName(data.Character)}**";
		string response =
			$"""
			*Removing data for:*
			{preview}
			""";
		await interaction.RegisterAndRespondAsync(response, true);

		// Remove data.
		await RemoveCharacterAsync(id, character);
		response =
			$"""
			*Removed data for:*
			{preview}
			""";
		await interaction.EditResponseAsync(response);
	}


	// --------
	// Database access methods:
	// --------
	// These methods are threadsafe. Don't use lower-level access methods.
	// The "database" refers to `_itemCrafters` and `_crafterData`.
	// Note: These methods do not check for ownership of the character!

	// Update guild roster (needed to include new members that join).
	private static async Task UpdateRosterAsync() {
		string rosterJson = await RequestRosterAsync();
		List<string> roster = new (ParseRosterJson(rosterJson));
		roster.Sort();
		_roster = roster;
	}

	// Completely rebuild the database.
	// `_itemCrafters` is replaced and `_crafterData` is updated.
	// Note: Does NOT remove characters from `_crafterData`.
	private static async Task RebuildItemDatabaseAsync() {
		Stopwatch stopwatch = Stopwatch.StartNew();
		Log.Information("  Crafter item database rebuild started.");

		await _queueItemData.Run(new Task<Task>(async () => {
			ConcurrentDictionary<string, ItemData> itemCrafters = new ();

			foreach (IReadOnlyList<Character> characters in _playerCrafters.Values) {
				foreach (Character character in characters) {
					string dataJson =
						await RequestProfessionsAsync(character);
					ParseProfessionsJson(
						dataJson,
						character,
						_crafterData[character],
						itemCrafters
					);
				}
			}

			_itemCrafters = itemCrafters;
			LastUpdated = DateTimeOffset.UtcNow;
		}));

		Log.Information("  Crafter item database rebuild complete.");
		stopwatch.LogMsec(2);
	}
	// Add a character (and their crafting data) to the database.
	private static async Task AddCharacterAsync(ulong userId, Character character) {
		await _queueItemData.Run(new Task<Task>(async () => {
			// Add to `_crafterData` (required for parsing professsions!).
			string profileJson = await RequestProfileAsync(character);
			Class @class = ParseProfileJson(profileJson);
			_crafterData[character] = new (userId, character, @class);

			// Add to `_itemCrafters` and `_crafterData`.
			string dataJson = await RequestProfessionsAsync(character);
			ParseProfessionsJson(
				dataJson,
				character,
				_crafterData[character],
				_itemCrafters
			);

			// Update `_playerCrafters`.
			if (!_playerCrafters.ContainsKey(userId))
				_playerCrafters.TryAdd(userId, new List<Character>());
			List<Character> characters = new (_playerCrafters[userId])
				{ character };
			_playerCrafters[userId] = characters;

			// Save data.
			await WriteCrafterDataToFile();
		}));
	}
	// Remove a character (and their crafting data) from the database.
	private static async Task RemoveCharacterAsync(ulong userId, Character character) {
		await _queueItemData.Run(new Task<Task>(async () => {
			// Remove from `_itemCrafters`.
			foreach (ItemData item in _itemCrafters.Values) {
				foreach (Character character_i in item.CrafterRecipes.Keys) {
					if (character_i == character)
						item.CrafterRecipes.TryRemove(character, out _);
				}
			}

			// Remove from `_crafterData`.
			_crafterData.TryRemove(character, out _);

			// Remove from _playerCrafters`.
			List<Character> characters = new ();
			foreach (Character character_i in _playerCrafters[userId]) {
				if (character_i != character)
					characters.Add(character_i);
			}
			_playerCrafters[userId] = characters;

			// Save data.
			await WriteCrafterDataToFile();
		}));
	}
	// Overwrite the summary text for the specified profession.
	private static async Task SetSummary(
		Character character,
		Profession profession,
		string summary
	) {
		await _queueItemData.Run(new Task<Task>(async () => {
			_crafterData[character].Professions[profession].Summary = summary;
			await WriteCrafterDataToFile();
		}));
	}


	// --------
	// File I/O methods (queued):
	// --------
	// Both these methods only queue the file operations themselves.
	// The actual data manipulation should still be wrapped in a queue
	// as well (`_queueItemData`).

	// Replace the crafter data file with current data, reading from
	// `_playerCrafters` and `_crafterData`.
	private static async Task WriteCrafterDataToFile() {
		await _queueFile.Run(new Task<Task>(async () => {
			List<string> lines = new ();

			foreach (ulong id in _playerCrafters.Keys) {
				lines.Add(id.ToString());
				foreach (Character character in _playerCrafters[id])
					lines.AddRange(_crafterData[character].Serialize());
			}

			await File.WriteAllLinesAsync(_pathCharacters, lines);
		}));
	}

	// Replace the current `_crafterData` and `_playerCrafters` with
	// new ones constructed from file data.
	// Item databases will need to be rebuilt after running this.
	private static async Task ReadCrafterDataFromFile() {
		await _queueFile.Run(
			new Task<Task>(async () => {
				ConcurrentDictionary<ulong, IReadOnlyList<Character>> playerCrafters;
				ConcurrentDictionary<Character, CharacterData> crafterData;

				List<string> lines =
					new (await File.ReadAllLinesAsync(_pathCharacters));
				DeserializeCrafterData(lines, out playerCrafters, out crafterData);

				_playerCrafters = playerCrafters;
				_crafterData = crafterData;
			})
		);
	}
	// Helper method for `ReadCrafterDataFromFile()`.
	// Note: Should only ever get called by that method.
	private static void DeserializeCrafterData(
		List<string> lines,
		out ConcurrentDictionary<ulong, IReadOnlyList<Character>> playerCrafters,
		out ConcurrentDictionary<Character, CharacterData> crafterData
	) {
		playerCrafters = new ();
		crafterData = new ();

		for (int i=0; i<lines.Count; i++) {
			string line = lines[i];
			// Skip blank lines.
			if (line.Trim() == "")
				continue;

			// Parse user (owner) ID.
			ulong id = ulong.Parse(line);

			// Parse in all characters owned by this user.
			List<string> linesUser = new ();
			while (i+1 < lines.Count &&
				lines[i+1].StartsWith(_indent)
			) {
				i++; line = lines[i];
				linesUser.Add(line);
			}

			// Break down the lines for each character.
			List<Character> characters = new ();
			for (int j=0; j<linesUser.Count; j++) {
				List<string> linesCharacter = new ()
					{ linesUser[j] };
				while (j+1 < linesUser.Count &&
					linesUser[j+1].StartsWith($"{_indent}{_indent}")
				) {
					j++;
					linesCharacter.Add(linesUser[j]);
				}

				// Update data for export.
				CharacterData characterData =
					CharacterData.Deserialize(id, linesCharacter);
				Character character = characterData.Character;
				crafterData.TryAdd(character, characterData);
				characters.Add(character);
			}

			// Update data for export.
			playerCrafters.TryAdd(id, characters);
		}
	}


	// --------
	// Simple data conversion methods:
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
	// Convert a JSON key value to the actual `Profession` enum.
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

	// Gets the job title of someone with this profession.
	private static string GetProfessionTitle(Profession profession) => profession switch {
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

	// Only append server names for non-Moon Guard servers.
	private static string GetServerLocalName(Character character) {
		string name = character.Name;
		if (character.Server != ServerDefault)
			name += "-" + ServerNameToNormalized(character.Server);
		return name;
	}

	// Creates a string of 3 full/empty stars, for a recipe's rank.
	private static string GetRecipeRankStars(int? rank) {
		rank ??= 0;
		const string
			starFull  = "\u2605",
			starEmpty = "\u2606";

		StringBuilder output = new ();
		for (int i=0; i<3; i++) {
			string star = (i <= rank-1)
				? starFull
				: starEmpty;
			output.Append(star);
		}

		return output.ToString();
	}

	// Helper methods to convert canonical server name to alternative
	// representations. (Not necessarily reversible.)
	private static string ServerNameToSlug(string name) =>
		name.ToLower()
			.Replace(" ", "-")
			.Replace("'", "");
	private static string ServerNameToNormalized(string name) =>
		name.Replace(" ", "")
			.Replace("-", "");

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

	// Sorts crafters by the following criteria:
	// - recipe rank, descending (if available)
	// - tier skill, descending
	// - default server before other servers
	// - alphabetically, ascending
	private static int CrafterRankComparer(
		Character a, Character b,
		bool hasRanks,
		ItemData item
	) {
		// Sort by rank, descending (if available).
		if (hasRanks) {
			long recipe_a = item.CrafterRecipes[a];
			long recipe_b = item.CrafterRecipes[b];
			int rank_a = _recipeRanks[recipe_a] ?? 0;
			int rank_b = _recipeRanks[recipe_b] ?? 0;
			if (rank_a != rank_b)
				return rank_b - rank_a;
		}

		// Sort by tier skill, descending.
		Profession profession = item.Profession;
		string tier = item.ProfessionTier;
		int skill_a = _crafterData[a]
			.Professions[profession]
			.Tiers[tier]
			.Skill;
		int skill_b = _crafterData[b]
			.Professions[profession]
			.Tiers[tier]
			.Skill;
		if (skill_a != skill_b)
			return skill_b - skill_a;

		// Sort default server before other servers.
		bool isDefaultServer_a = (a.Server == ServerDefault);
		bool isDefaultServer_b = (b.Server == ServerDefault);
		if (isDefaultServer_a != isDefaultServer_b)
			return isDefaultServer_a
				? -1
				:  1;

		// Sort by name, alphabetically.
		return string.Compare(a.Name, b.Name);
	}


	// --------
	// HTTP request methods:
	// --------
	
	// These methods return a string that can directly be parsed into
	// a `JsonNode` for further processing.
	private static Task<string> RequestServersAsync() =>
		_client.RequestAsync(ApiNamespace.Dynamic, _urlServers);
	private static Task<string> RequestRosterAsync() =>
		_client.RequestAsync(ApiNamespace.Profile, _urlRoster);
	private static Task<string> RequestProfileAsync(Character character) {
		string url = GetProfileUrl(character);
		return _client.RequestAsync(ApiNamespace.Profile, url);
	}
	private static Task<string> RequestProfessionsAsync(Character character) {
		string url = GetProfessionsUrl(character);
		return _client.RequestAsync(ApiNamespace.Profile, url);
	}
	private static Task<string> RequestRecipeAsync(long id) {
		string url = GetRecipeUrl(id);
		return _client.RequestAsync(ApiNamespace.Static, url);
	}


	// --------
	// JSON parsing methods:
	// --------

	// Returns a list of all servers, indexed by their IDs.
	private static HashSet<string> ParseServersJson(string json) {
		HashSet<string> servers = new ();

		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		JsonNode node = Util.ParseSubnode(root, _keyServers);
		foreach (JsonNode? nodeServer in node.AsArray()) {
			if (nodeServer is null)
				continue;
			string name = Util.ParseString(nodeServer, _keyName);
			servers.Add(name);
		}

		return servers;
	}
	// Returns a list of all guild members (only names, no servers).
	private static HashSet<string> ParseRosterJson(string json) {
		HashSet<string> roster = new ();

		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		JsonNode node = Util.ParseSubnode(root, _keyMembers);
		foreach (JsonNode? node_i in node.AsArray()) {
			if (node_i is null)
				continue;
			JsonNode nodeMember = Util.ParseSubnode(node_i, _keyCharacter);
			string name = Util.ParseString(nodeMember, _keyName);
			roster.Add(name);
		}

		return roster;
	}
	// Returns the character class from a profile.
	private static Class ParseProfileJson(string json) {
		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		string className =
			Util.ParseSubString(root, _keyClass, _keyName);

		return ParseClass(className)
			?? throw new FormatException();
	}
	// Returns the rank of recipe, if the recipe has ranks.
	// Returns null if it does not.
	private static int? ParseRecipeJson(string json) {
		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		// Check if the recipe has ranks.
		JsonNode? nodeRank = root[_keyRank];
		return nodeRank is null
			? null
			: Util.ParseInt(root, _keyRank);
	}

	// Parses through the professions data and updates both the input
	// `characterData` and `itemCrafters` parameters with extracted data
	// (those two parameters are modified).
	// For `characterData`, removed professions will have their associated
	// data removed.
	// For `itemCrafters`, data will only be added, and not removed.
	private static void ParseProfessionsJson(
		string json,
		Character character,
		CharacterData characterData,
		IDictionary<string, ItemData> itemCrafters
	) {
		JsonNode root = JsonNode.Parse(json)
			?? throw new FormatException();

		// Create replacement `ProfessionData` to reassign later.
		ConcurrentDictionary<Profession, ProfessionData> data = new ();

		// Merge primary/secondary profession arrays into a single list.
		// These will be null if either primary or secondary professions
		// are completely missing, so we need to guard with null checks.
		JsonNode? nodeProfession;
		List<JsonNode?> nodesProfession = new ();
		nodeProfession = root[_keyPrimary];
		if (nodeProfession is not null)
			nodesProfession.AddRange(nodeProfession.AsArray());
		nodeProfession = root[_keySecondary];
		if (nodeProfession is not null)
			nodesProfession.AddRange(nodeProfession.AsArray());

		// Iterate through combined list of nodes.
		foreach (JsonNode? node_i in nodesProfession) {
			if (node_i is null)
				continue;

			// Parse the actual profession JSON node.
			(Profession, TierSkillDictionary)? professionData =
				ParseProfessionJsonNode(
					node_i,
					character,
					itemCrafters
				);
			if (professionData is null)
				continue;

			// Create new `ProfessionData`, copying over original user-
			// entered data if any exists.
			(Profession profession, TierSkillDictionary tierSkill) =
				professionData.Value;
			string summary = "";
			if (characterData.Professions.ContainsKey(profession))
				summary = characterData.Professions[profession].Summary;
			data.TryAdd(profession, new (profession, summary));
			data[profession].Tiers = tierSkill;
		}

		// Overwrite the old professions data with the updated data.
		characterData.Professions = data;
	}

	// Inner helper method for `ParseProfessionsJson()`. Should only
	// ever be called by that method.
	// Note: This method modifies its `itemCrafters` parameter.
	private static (Profession, TierSkillDictionary)? ParseProfessionJsonNode(
		JsonNode node,
		Character character,
		IDictionary<string, ItemData> itemCrafters
	) {
		// Determine which profession we're parsing.
		string professionName =
				Util.ParseSubString(node, _keyProfession, _keyName);
		Profession? profession = ParseProfession(professionName);
		if (profession is null)
			return null;

		// Create replacement TierSkill data to reassign later.
		TierSkillDictionary tierSkills = new ();

		// Parse through each tier of the profession.
		JsonNode nodeTiers = Util.ParseSubnode(node, _keyTiers);
		foreach (JsonNode? nodeTier in nodeTiers.AsArray()) {
			if (nodeTier is null)
				continue;

			// Add TierSkill data entry.
			string tier = Util.ParseSubString(nodeTier, _keyTier, _keyName);
			int skill = Util.ParseInt(nodeTier, _keySkill);
			int skillMax = Util.ParseInt(nodeTier, _keySkillMax);
			tierSkills.TryAdd(tier, new (skill, skillMax));

			// Parse through each item.
			// Sometimes a tier has no known recipes.
			if (nodeTier[_keyRecipes] is null)
				continue;
			JsonNode nodeItems = Util.ParseSubnode(nodeTier, _keyRecipes);
			foreach (JsonNode? nodeItem in nodeItems.AsArray()) {
				if (nodeItem is null)
					continue;

				string item = Util.ParseString(nodeItem, _keyName);
				long id = Util.ParseLong(nodeItem, _keyId);

				// Ensure the item exists in the table before
				// registering the current crafter.
				if (!itemCrafters.ContainsKey(item)) {
					itemCrafters.TryAdd(
						item,
						new (item, profession.Value, tier)
					);
				}
				// Append the current crafter to the list.
				itemCrafters[item].CrafterRecipes.TryAdd(character, id);
			}
		}

		return new (profession.Value, tierSkills);
	}
}
