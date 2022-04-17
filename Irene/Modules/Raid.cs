namespace Irene.Modules;

public enum RaidTier {
	EN, NH, ToV, ToS, ABT,
	Uldir, BoD, CoS, EP, NWC,
	CN, SoD, SFO,
}
public enum RaidDay {
	Fri, Sat,
}
public enum RaidGroup {
	Spaghetti,
	Salad,
}

public readonly record struct RaidDate
	(int Week, RaidDay Day);

class Raid {
	public static TimeOnly Time { get => new (19, 0, 0); } // local (Pacific) time.
	public static RaidGroup DefaultGroup { get => RaidGroup.Spaghetti; }
	public static RaidTier CurrentTier { get => RaidTier.SFO; }
	public static int CurrentWeek { get {
		TimeSpan duration = DateTime.UtcNow - Date_Season3.UtcResetTime();
		int week = (duration.Days / 7) + 1; // int division
		return week;
	} }
	
	private static readonly object _lock = new ();
	private static readonly List<string> _raidEmojis = new () {
		":dolphin:", ":whale:"   , ":fox:"        , ":squid:"   ,
		":rabbit2:", ":bee:"     , ":butterfly:"  , ":owl:"     ,
		":shark:"  , ":swan:"    , ":lady_beetle:", ":sloth:"   ,
		":octopus:", ":bird:"    , ":turkey:"     , ":rooster:" ,
		":otter:"  , ":parrot:"  , ":elephant:"   , ":microbe:" ,
		":peacock:", ":chipmunk:", ":lion_face:"  , ":mouse:"   ,
		":snail:"  , ":giraffe:" , ":duck:"       , ":bat:"     ,
		":crab:"   , ":flamingo:", ":orangutan:"  , ":kangaroo:",
	};

	private const string
		_fragLogs     = @"https://www.warcraftlogs.com/reports/",
		_fragWipefest = @"https://www.wipefest.gg/report/",
		_fragAnalyzer = @"https://wowanalyzer.com/report/";
	private const string
		_pathData   = @"data/raids.txt",
		_pathBuffer = @"data/raids-buf.txt";
	private const string _sep = "-";
	private const string _indent = "\t";
	private const string _delim = "=";
	private const string
		_keyDoCancel  = "canceled",
		_keyTier      = "tier",
		_keyWeek      = "week",
		_keyDay       = "day",
		_keyGroup     = "group",
		_keySummary   = "summary",
		_keyLogId     = "log-id",
		_keyMessageId = "message-id";

	// Force static initializer to run.
	public static void Init() { return; }
	static Raid() {
		// Make sure datafile exists.
		Util.CreateIfMissing(_pathData, _lock);
	}

	// Fetch raid data from saved data.
	// Returns null if a matching entry could not be found.
	public static Raid? Fetch(int week, RaidDay day, RaidGroup group) =>
		Fetch(CurrentTier, week, day, group);
	public static Raid? Fetch(RaidTier tier, int week, RaidDay day, RaidGroup group) {
		string hash = new Raid(tier, week, day, group).Hash;
		string? entry = ReadRaidData(hash);
		return (entry is null)
			? null
			: Deserialize(entry);
	}

	// Overwrite/insert the provided raid data the existing data.
	public static void Update(Raid raid) {
		List<string> entries = ReadAllRaidData();
		SortedList<Raid, string> entries_sorted =
			new (new RaidComparer());

		// Populate sorted list and add in updated data.
		foreach (string entry in entries) {
			Raid? key = Deserialize(entry);
			if (key is null)
				continue;
			if (raid.Hash == key.Hash)
				entries_sorted.Add(key, raid.Serialize());
			else
				entries_sorted.Add(key, entry);
		}

		// Update data file.
		lock (_lock) {
			File.WriteAllLines(_pathBuffer, entries_sorted.Values);
			File.Delete(_pathData);
			File.Move(_pathBuffer, _pathData);
		}
	}

	// Reads the entire datafile and groups them into entries.
	private static List<string> ReadAllRaidData() {
		List<string> entries = new ();
		string? entry = null;

		lock (_lock) {
			using StreamReader file = File.OpenText(_pathData);
			while (!file.EndOfStream) {
				string line = file.ReadLine() ?? "";
				if (!line.StartsWith(_indent)) {
					if (entry is not null)
						entries.Add(entry);
					entry = line;
				} else if (entry is not null) {
					entry += $"\n{line}";
				}
			}
			if (entry is not null)
				entries.Add(entry);
		}

		return entries;
	}
	// Fetch a single serialized entry matching the given hash.
	// This function exits early when possible.
	// Returns null if no valid object found.
	private static string? ReadRaidData(string hash) {
		bool was_found = false;
		string entry = "";

		// Look for matching raid data.
		lock (_lock) {
			using StreamReader file = File.OpenText(_pathData);
			while (!file.EndOfStream) {
				string line = file.ReadLine() ?? "";
				if (line == hash) {
					was_found = true;
					entry = hash;
					line = file.ReadLine() ?? "";
					while (line.StartsWith(_indent)) {
						entry += $"\n{line}";
						line = file.ReadLine() ?? "";
					}
					// Reader is now invalid: we read an extra line.
					// Must break anyway!
					break;
				}
			}
		}

		return was_found ? entry : null;
	}

	// Creates a populated Raid object from the serialized data.
	// Returns null if the object is underspecified.
	private static Raid? Deserialize(string entry) {
		// Skip first line (hash).
		string[] lines = entry.Split("\n")[1..];

		// Buffer variables for deserialization.
		RaidTier? tier = null;
		int? week = null;
		RaidDay? day = null;
		RaidGroup? group = null;
		bool? doCancel = null;
		string? summary = null;
		string? logId = null;
		ulong? messageId = null;

		// Parse lines.
		foreach (string line in lines) {
			string[] split = line.Split(_delim, 2);
			string key = split[0];
			string value = split[1];

			switch (key) {
			case _keyTier:
				tier = Enum.Parse<RaidTier>(value);
				break;
			case _keyWeek:
				week = int.Parse(value);
				break;
			case _keyDay:
				day = Enum.Parse<RaidDay>(value);
				break;
			case _keyGroup:
				group = Enum.Parse<RaidGroup>(value);
				break;
			case _keyDoCancel:
				doCancel = bool.Parse(value);
				break;
			case _keySummary:
				summary = (value != "") ? value : null;
				break;
			case _keyLogId:
				logId = (value != "") ? value : null;
				break;
			case _keyMessageId:
				messageId = (value != "") ? ulong.Parse(value) : null;
				break;
			}
		}

		// Check that all required fields are non-null.
		if (tier is null || week is null || day is null || group is null || doCancel is null)
			return null;

		// Create a raid object to return.
		return new Raid(tier!.Value, week!.Value, day!.Value, group!.Value) {
			DoCancel = doCancel!.Value,
			Summary = summary,
			LogId = logId,
			MessageId = messageId,
		};
	}

	// Returns a uniquely identifiable string per raid + group.
	public string Hash { get =>
		string.Join(_sep, new object[] { Tier, Date.Week, Date.Day, Group });
	}
	// Each week of the tier has a different emoji associated with it.
	// The order is fixed between tiers.
	public string Emoji { get {
		int i = (Date.Week - 1) % _raidEmojis.Count;
		return _raidEmojis[i];
	} }
	// Convenience functions for accessing different log websites.
	public string? UrlLogs { get =>
		LogId is null ? null : $"{_fragLogs}{LogId}";
	}
	public string? UrlWipefest { get =>
		LogId is null ? null : $"{_fragWipefest}{LogId}";
	}
	public string? UrlAnalyzer { get =>
		LogId is null ? null : $"{_fragAnalyzer}{LogId}";
	}

	// Data fields (non-calculated).
	public RaidTier Tier { get; init; }
	public RaidDate Date { get; init; }
	public RaidGroup Group { get; init; }
	public bool DoCancel { get; set; }
	public string? Summary { get; set; }
	public string? LogId { get; set; }
	public ulong? MessageId { get; set; }

	// Constructors.
	public Raid(int week, RaidDay day, RaidGroup group)
		: this(CurrentTier, week, day, group) { }
	public Raid(RaidTier tier, int week, RaidDay day, RaidGroup group) {
		Tier = tier;
		Date = new RaidDate(week, day);
		Group = group;
		DoCancel = false;
		Summary = null;
		LogId = null;
		MessageId = null;
	}

	// Returns a(n ordered) list of the instance's serialization.
	string Serialize() =>
		string.Join("\n", new List<string> {
			Hash,
			$"{_indent}{_keyTier}{_delim}{Tier}",
			$"{_indent}{_keyWeek}{_delim}{Date.Week}",
			$"{_indent}{_keyDay}{_delim}{Date.Day}",
			$"{_indent}{_keyGroup}{_delim}{Group}",
			$"{_indent}{_keyDoCancel}{_delim}{DoCancel}",
			$"{_indent}{_keySummary}{_delim}{Summary ?? ""}",
			$"{_indent}{_keyLogId}{_delim}{LogId ?? ""}",
			$"{_indent}{_keyMessageId}{_delim}{MessageId?.ToString() ?? ""}",
		});

	// Reverse-date comparer (for serialization).
	private class RaidComparer : Comparer<Raid> {
		public override int Compare(Raid? x, Raid? y) {
			if (x is null)
				throw new ArgumentNullException(nameof(x), "Attempted to compare null reference.");
			if (y is null)
				throw new ArgumentNullException(nameof(y), "Attempted to compare null reference.");

			if (x.Tier != y.Tier)
				return y.Tier - x.Tier;
			if (x.Date.Week != y.Date.Week)
				return y.Date.Week - x.Date.Week;
			if (x.Date.Day != y.Date.Day)
				return y.Date.Day - x.Date.Day;
			if (x.Group != y.Group)
				return x.Group - y.Group; // Group is normal sort order

			return 0;
		}
	}
}
