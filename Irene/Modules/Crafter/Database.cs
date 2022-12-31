namespace Irene.Modules.Crafter;

using System.Timers;

using static Types;
using static ApiRequest;

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
			.ContinueWith(t => RebuildItemDatabaseAsync());

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
		timerItemData.Elapsed += (_, _) => _ = RebuildItemDatabaseAsync();
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


	// --------
	// Database table access methods:
	// --------

	public static IReadOnlySet<string> GetItems() =>
		new HashSet<string>(_itemCrafters.Keys);

	public static ItemData GetItemData(string itemName) =>
		_itemCrafters[itemName];
	public static CharacterData GetCrafterData(Character crafter) =>
		_crafterData[crafter];

	public static IReadOnlySet<Character> GetCrafters(ulong userId) =>
		_playerCrafters[userId];
	public static IReadOnlySet<Character> GetCrafters(Profession profession) =>
		_professionCrafters[profession];

	public static async Task<int?> GetRecipeRankAsync(long id) {
		// Check cache for an unexpired value.
		if (_recipeRanks.TryGetValue(id, out ExpiringRank rankCached)) {
			if (rankCached.IsValid)
				return rankCached.Rank;
		}

		// Else, fetch value, update cache, and return.
		string json = await RequestRecipeAsync(id);
		int? rank = ParseRecipeJson(json);
		_recipeRanks[id] = new (rank);
		return rank;
	}
}
