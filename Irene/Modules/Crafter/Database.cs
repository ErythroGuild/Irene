namespace Irene.Modules.Crafter;

using System.Diagnostics;
using System.Timers;

using static Types;
using static ApiRequest;

using Class = ClassSpec.Class;
using ProfessionData = Types.CharacterData.ProfessionData;

class Database {
	private readonly record struct ExpiringRank {
		public readonly int? Rank;
		public readonly DateTimeOffset Expiry;

		public bool IsValid => Expiry > DateTimeOffset.UtcNow;

		// Item rank data is at most one day out-of-date.
		// (But most likely only new recipes will be added, and old
		// ranks will never be modified.)
		private static readonly TimeSpan _ttl = TimeSpan.FromDays(1);
		public ExpiringRank(int? rank) {
			Rank = rank;
			Expiry = DateTimeOffset.UtcNow + _ttl;
		}
	};


	// --------
	// Properties and constants:
	// --------

	// Static status indicator for item data updates.
	// Not thread-safe. Should only be modified behind the queue for
	// item data updates.
	public static DateTimeOffset LastUpdated { get; private set; }

	// Timers to automatically refresh database tables.
	private static readonly Timer
		_timerItemData,
		_timerRoster,
		_timerServers;
	private static readonly TimeSpan
		_intervalItemDataRefresh = TimeSpan.FromMinutes(90), // 1.5 hours
		_intervalRosterRefresh   = TimeSpan.FromMinutes(45),
		_intervalServersRefresh  = TimeSpan.FromDays(7);
		// Servers are probably only added on resets.
		
	// Queues to control resource access.
	private static readonly TaskQueue
		_queueFile = new (),
		_queueItemData = new ();

	//private const string _pathCharacters = @"data/crafters.txt";
	private const string _pathCharacters = @"data/crafters-new-format.txt";


	// --------
	// Master tables:
	// --------

	// Master list of valid servers (according to Blizzard's API).
	public static IReadOnlyList<string> Servers { get; private set; } = new List<string>();
	// Master list of all guild members, stored as strings (since this
	// is only used to populate a `StringCompleter`).
	public static IReadOnlyList<string> Roster { get; private set; } = new List<string>();
	
	// Master table of crafters, indexed by the user ID of the player
	// who owns the crafters in that list.
	private static ConcurrentDictionary<ulong, IReadOnlySet<Character>> _playerCrafters = new ();
	// Master table associating each crafter to their profession data.
	private static ConcurrentDictionary<Character, CharacterData> _crafterData = new ();
	// Master table of crafters, indexed by profession.
	private static ConcurrentDictionary<Profession, IReadOnlySet<Character>> _professionCrafters = new ();
	// Master table of crafters, indexed by the name of the item.
	// (`ItemData` includes crafter lists.)
	private static ConcurrentDictionary<string, ItemData> _itemCrafters = new ();

	// Cache of which ranks each recipe maps to, indexed by recipe ID.
	private static readonly ConcurrentDictionary<long, ExpiringRank> _recipeRanks = new ();


	// --------
	// Initialization:
	// --------

	static Database() {
		// Initialize server list and guild roster from API query.
		Task.WaitAll(
			InitServersAsync(),
			InitRosterAsync()
		);

		// Initialize database, first fetching all saved character data.
		// This is threadsafe and can happen in the background.
		Util.CreateIfMissing(_pathCharacters);
		_ = ReadCrafterDataFromFile()
			.ContinueWith(t => InitItemDatabaseAsync());

		// Set up server list auto-update.
		Timer timerServers = Util.CreateTimer(_intervalServersRefresh, true);
		timerServers.Elapsed += (_, _) => _ = InitServersAsync();
		_timerServers = timerServers;
		_timerServers.Start();

		// Set up guild roster auto-update.
		Timer timerRoster = Util.CreateTimer(_intervalRosterRefresh, true);
		timerRoster.Elapsed += (_, _) => _ = InitRosterAsync();
		_timerRoster = timerRoster;
		_timerRoster.Start();

		// Set up item database auto-update.
		Timer timerItemData = Util.CreateTimer(_intervalItemDataRefresh, true);
		timerItemData.Elapsed += (_, _) => _ = InitItemDatabaseAsync();
		_timerItemData = timerItemData;
		_timerItemData.Start();
	}

	// Update server list (in case servers are added/removed somehow).
	private static async Task InitServersAsync() {
		string json = await RequestServersAsync();
		List<string> servers = new (ParseServersJson(json));
		servers.Sort();
		Servers = servers;
	}
	// Update guild roster (needed to include new members that join).
	private static async Task InitRosterAsync() {
		string json = await RequestRosterAsync();
		List<string> roster = new (ParseRosterJson(json));
		roster.Sort();
		Roster = roster;
	}

	// Repopulate the main databases from scratch. These include:
	//   - updated : `_crafterData`
	//   - replaced: `_professionCrafters`
	//   - replaced: `_itemCrafters`
	// Note: This requires `_playerCrafters` to already be populated,
	// and `_crafterData` to already have saved data read in.
	private static async Task InitItemDatabaseAsync() {
		Stopwatch stopwatch = Stopwatch.StartNew();
		Log.Information("  Crafter item database rebuild started.");

		await _queueItemData.Run(new Task<Task>(async () => {
			// Collate all the character profession JSON data.
			Dictionary<Character, string> json = new ();
			foreach (IReadOnlySet<Character> characters in _playerCrafters.Values) {
				foreach (Character character in characters) {
					string json_i = await RequestProfessionsAsync(character);
					json.Add(character, json_i);
				}
			}

			ParsedCharacterData parsedData = ParseProfessionsJson(json);

			// Update `_crafterData`.
			PopulateTierSkills(parsedData.ProfessionData);
			// Replace profession crafter lists.
			_professionCrafters = CollateProfessionCrafters();
			// Replace entire item database.
			_itemCrafters = parsedData.ItemTable;

			LastUpdated = DateTimeOffset.UtcNow;
		}));

		Log.Information("  Crafter item database rebuild complete.");
		stopwatch.LogMsec(2);
	}


	// --------
	// Database table access methods:
	// --------

	public static IReadOnlySet<string> GetItems() =>
		new HashSet<string>(_itemCrafters.Keys);

	public static ItemData GetItemData(string itemName) =>
		_itemCrafters[itemName];
	public static CharacterData GetCrafterData(Character crafter) =>
		_crafterData[crafter];

	public static IReadOnlySet<Character> GetCrafters(ulong userId) {
		_playerCrafters.TryGetValue(
			userId,
			out IReadOnlySet<Character>? crafters
		);
		return crafters ?? new HashSet<Character>();
	}
	public static IReadOnlySet<Character> GetCrafters(Profession profession) {
		_professionCrafters.TryGetValue(
			profession,
			out IReadOnlySet<Character>? crafters
		);
		return crafters ?? new HashSet<Character>();
	}

	public static async Task<int?> GetRecipeRankAsync(long id) =>
		IsRecipeRankCached(id)
			? _recipeRanks[id].Rank
			: await CacheRecipeRankAsync(id);
	// Unlike `GetRecipeRankAsync()`, this method does not `await` and
	// throws an `InvalidOperationException` if no valid value is cached.
	// If `doAllowExpired` is true, then rank data will be returned even
	// if expired (will not throw).
	public static int? GetRecipeRankCached(long id, bool doAllowExpired) {
		if (!_recipeRanks.TryGetValue(id, out ExpiringRank rankCached))
			throw new InvalidOperationException($"Recipe rank not found in cache: {id}");

		if (doAllowExpired || rankCached.IsValid)
			return rankCached.Rank;

		throw new InvalidOperationException($"Cached recipe rank data expired: {id}");
	}
	
	// Whether or not the given recipe ID has a non-expired, cached rank.
	public static bool IsRecipeRankCached(long id) =>
		_recipeRanks.TryGetValue(id, out ExpiringRank rankCached) &&
		rankCached.IsValid;
	// Forces a refresh of the recipe rank data, and also returns the
	// rank that was fetched (this can be ignored).
	public static async Task<int?> CacheRecipeRankAsync(long id) {
		string json = await RequestRecipeAsync(id);
		int? rank = ParseRecipeJson(json);
		_recipeRanks[id] = new (rank);
		return rank;
	}


	// --------
	// Database update methods:
	// --------
	// These methods are thread-safe, and wrap their database accesses
	// in the database task queue.
	// Note: These methods do not check for character ownership!
	// The user ID is only used for indexing the character.

	// Add a character to all databases.
	public static Task AddCharacterAsync(ulong userId, Character character) =>
		_queueItemData.Run(new Task<Task>(async () => {
			// Parse the character's profession data.
			string jsonProfessions = await RequestProfessionsAsync(character);
			ParsedCharacterData parsedData =
				ParseProfessionsJson(character, jsonProfessions);

			// Add to `_crafterData`. This table should be updated first,
			// in case the data is accessed from the other tables.
			string jsonProfile = await RequestProfileAsync(character);
			Class @class = ParseProfileJson(jsonProfile);
			_crafterData[character] = new (userId, character, @class);
			PopulateTierSkills(parsedData.ProfessionData);

			// Add to `_playerCrafters`.
			if (!_playerCrafters.ContainsKey(userId))
				_playerCrafters.TryAdd(userId, new HashSet<Character>());
			HashSet<Character> userCrafters =
				new (_playerCrafters[userId]) { character };
			_playerCrafters[userId] = userCrafters;

			// Add to `_professionCrafters`.
			foreach (Profession profession in _crafterData[character].Professions) {
				if (!_professionCrafters.ContainsKey(profession))
					_professionCrafters.TryAdd(profession, new HashSet<Character>());
				HashSet<Character> professionCrafters =
					new (_professionCrafters[profession]) { character };
				_professionCrafters[profession] = professionCrafters;
			}

			// Add to `_itemCrafters`.
			foreach (ItemData itemData in parsedData.ItemTable.Values) {
				string item = itemData.Name;
				if (!_itemCrafters.ContainsKey(item)) {
					_itemCrafters.TryAdd(item, itemData);
				} else {
					_itemCrafters[item].SetCrafter(
						character,
						itemData.GetCrafterRecipeId(character)
					);
				}
			}

			// Save data.
			await WriteCrafterDataToFile();
		}));

	// Remove a character from all databases.
	public static Task RemoveCharacterAsync(Character character) =>
		// These removals are in reverse order (compared to adding a
		// character), to prevent trying to access already-removed data.
		_queueItemData.Run(new Task<Task>(async () => {
			// Remove from `_itemCrafters`.
			foreach (ItemData item in _itemCrafters.Values)
				// No need to check item profession first, since we're
				// going to iterate through every item anyway.
				item.RemoveCrafter(character);
				// No need to remove the rest of the item data--that's
				// still valid, and can be cached for future accesses.

			// Remove from `_professionCrafters`.
			foreach (Profession profession in _professionCrafters.Keys) {
				if (_professionCrafters[profession].Contains(character)) {
					HashSet<Character> crafters =
						new (_professionCrafters[profession]);
					crafters.Remove(character);
					_professionCrafters[profession] = crafters;
				}
			}

			// Remove from `_playerCrafters`.
			foreach (ulong userId in _playerCrafters.Keys) {
				if (_playerCrafters[userId].Contains(character)) {
					HashSet<Character> crafters =
						new (_playerCrafters[userId]);
					crafters.Remove(character);
					_playerCrafters[userId] = crafters;
				}
			}

			// Remove from `_crafterData`.
			_crafterData.TryRemove(character, out _);

			// Save data.
			await WriteCrafterDataToFile();
		}));

	// Update all profession data for a character.
	// Note: Requires character to already exist in all the databases.
	// This method does NOT update the `_playerCrafters` list.
	public static Task RefreshCharacterAsync(Character character) =>
		_queueItemData.Run(new Task<Task>(async () => {
			// Fetch updated data.
			string jsonProfessions = await RequestProfessionsAsync(character);
			ParsedCharacterData parsedData =
				ParseProfessionsJson(character, jsonProfessions);

			// Update `_crafterData`, copying over previous (unavailable
			// from parsed) data. This table should be updated first,
			// in case the data is accessed from the other tables.
			string jsonProfile = await RequestProfileAsync(character);
			Class @class = ParseProfileJson(jsonProfile);
			CharacterData crafterDataPrevious = _crafterData[character];
			CharacterData crafterData = new (
				crafterDataPrevious.UserId,
				character,
				@class
			);
			foreach (Profession profession in crafterData.Professions) {
				crafterData.SetProfessions(
					new (crafterDataPrevious.GetProfessionData())
				);
			}
			_crafterData[character] = crafterData;
			PopulateTierSkills(parsedData.ProfessionData);

			// Update `_professionCrafters`.
			foreach (Profession profession in Enum.GetValues<Profession>()) {
				if (!_professionCrafters.ContainsKey(profession))
					_professionCrafters.TryAdd(profession, new HashSet<Character>());

				HashSet<Character> professionCrafters =
					new (_professionCrafters[profession]);
				if (crafterData.HasProfession(profession))
					professionCrafters.Add(character);
				else
					professionCrafters.Remove(character);
				_professionCrafters[profession] = professionCrafters;
			}

			// Update `_itemCrafters`.
			HashSet<string> itemsMerged = new (_itemCrafters.Keys);
			itemsMerged.UnionWith(parsedData.ItemTable.Keys);

			foreach (string item in itemsMerged) {
				if (!_itemCrafters.ContainsKey(item))
					_itemCrafters.TryAdd(item, parsedData.ItemTable[item]);

				ItemData itemData = _itemCrafters[item];
				if (!parsedData.ItemTable.TryGetValue(item, out ItemData? parsedItemData)) {
					itemData.RemoveCrafter(character);
				} else {
					itemData.SetCrafter(
						character,
						parsedItemData.GetCrafterRecipeId(character)
					);
				}
			}

			// Save data.
			await WriteCrafterDataToFile();
		}));

	// Set the summary text for the specified character and profession.
	public static Task SetSummary(
		Character character,
		Profession profession,
		string summary
	) =>
		_queueItemData.Run(new Task<Task>(async () => {
			ProfessionData data =
				_crafterData[character]
				.GetProfessionData(profession);
			data.Summary = summary;
			await WriteCrafterDataToFile();
		}));


	// --------
	// Data file I/O methods:
	// --------
	// Both these methods only queue the file operations themselves.
	// The actual data manipulation should still be wrapped in a queue
	// as well (e.g. `_queueItemData`).

	// Replace the crafter data file with current data, taken from the
	// `_playerCrafters` and `_crafterData` tables.
	private static Task WriteCrafterDataToFile() =>
		_queueFile.Run(new Task<Task>(async () => {
			List<string> lines = new ();

			// Sort the user list to ensure the saved data is stable.
			List<ulong> users = new (_playerCrafters.Keys);
			users.Sort();

			foreach (ulong user in users) {
				lines.Add(user.ToString());

				// Sort each user's crafter list as well (only on names).
				List<Character> crafters = new (GetCrafters(user));
				crafters.Sort((c1, c2) => string.Compare(c1.Name, c2.Name));

				foreach (Character character in _playerCrafters[user])
					lines.AddRange(_crafterData[character].Serialize());
			}

			await File.WriteAllLinesAsync(_pathCharacters, lines);
		}));

	// Replace the current `_crafterData` and `_playerCrafters` with
	// new ones constructed from file data.
	// Note: This wipes all `TierSkill` data!
	private static Task ReadCrafterDataFromFile() =>
		_queueFile.Run(
			new Task<Task>(async () => {
				List<string> lines =
					new (await File.ReadAllLinesAsync(_pathCharacters));

				DeserializeCrafterData(
					lines,
					out var playerCrafters,
					out var crafterData
				);
				// `var` is okay here since this helper method is only
				// ever called in this spot.

				_playerCrafters = playerCrafters;
				_crafterData = crafterData;
			})
		);

	// Helper method for `ReadCrafterDataFromFile()`.
	// Note: Should only ever get called by said method.
	private static void DeserializeCrafterData(
		List<string> lines,
		out ConcurrentDictionary<ulong, IReadOnlySet<Character>> playerCrafters,
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
			string _indent = CharacterData.Indent;
			while (i+1 < lines.Count &&
				lines[i+1].StartsWith(_indent)
			) {
				i++; line = lines[i];
				linesUser.Add(line);
			}

			// Break down the lines for each character.
			HashSet<Character> characters = new ();
			for (int j=0; j<linesUser.Count; j++) {
				List<string> linesCharacter = new () { linesUser[j] };
				string _indent2 = $"{_indent}{_indent}";
				while (j+1 < linesUser.Count &&
					linesUser[j+1].StartsWith(_indent2)
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
	// Internal helper methods:
	// --------

	// Updates the existing `_crafterData` table with parsed `TierSkill`
	// data.
	// Requires the `CharacterData` for the character to already exist
	// in the table.
	private static void PopulateTierSkills(
		ConcurrentDictionary<Character, HashSet<ParsedProfessionData>> parsedData
	) {
		foreach (Character character in _crafterData.Keys) {
			HashSet<ParsedProfessionData> professionData = parsedData[character];
			ConcurrentDictionary<Profession, ProfessionData> professions = new ();
			CharacterData data = _crafterData[character];

			foreach (ParsedProfessionData professionData_i in professionData) {
				Profession profession = professionData_i.Profession;

				// Copy over any existing `ProfessionData`.
				ProfessionData dataPopulated =
					data.HasProfession(profession)
						? data.GetProfessionData(profession)
						: new (profession);

				professions.TryAdd(profession, dataPopulated);

				// Update profession data with parsed `TierSkill`s.
				data.GetProfessionData(profession)
					.SetSkills(professionData_i.TierSkills);
			}

			data.SetProfessions(professions);
		}
	}
	
	// Collates a replacement for the `_professionCrafters` table from
	// the current `_crafterData`.
	private static ConcurrentDictionary<Profession, IReadOnlySet<Character>>
		CollateProfessionCrafters()
	{
		// First populate a mutable list using `_crafterData`.
		ConcurrentDictionary<Profession, HashSet<Character>> crafterList = new ();
		foreach (Profession profession in Enum.GetValues<Profession>())
			crafterList.TryAdd(profession, new ());

		foreach (Character character in _crafterData.Keys) {
			CharacterData crafterData = _crafterData[character];
			foreach (Profession profession in crafterData.Professions)
				crafterList[profession].Add(character);
		}

		// Convert each mutable list to a frozen list.
		ConcurrentDictionary<Profession, IReadOnlySet<Character>> output = new ();
		foreach (Profession profession in crafterList.Keys)
			output.TryAdd(profession, crafterList[profession]);

		return output;
	}
}
