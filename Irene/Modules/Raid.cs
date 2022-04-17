namespace Irene.Modules;

using FileEntry = List<string>;

class Raid {
	public enum Tier {
		EN, NH, ToV, ToS, ABT,
		Uldir, BoD, CoS, EP, NWC,
		CN, SoD, SFO,
	}
	public enum Day {
		Fri, Sat,
	}
	public enum Group {
		Spaghetti,
		Salad,
	}

	public readonly record struct Date (int Week, Day Day);

	public static TimeOnly Time { get => new (19, 0, 0); } // local (Pacific) time.
	public static Group DefaultGroup { get => Group.Spaghetti; }
	public static Tier CurrentTier { get => Tier.SFO; }
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
		_keyDoCancel = "canceled",
		_keyTier     = "tier",
		_keyWeek     = "week",
		_keyDay      = "day",
		_keyGroup    = "group",
		_keySummary  = "summary",
		_keyLogId    = "log-id";

	// Replace the previous raid entry with the same hash as
	// the new one if one exists; otherwise prepends the raid
	// entry.
	public static void update(Raid raid) {
		Util.CreateIfMissing(_pathData, _lock);

		// Replace in-place the entry if it's an update to an
		// existing entry.
		List<FileEntry> entries = get_file_entries();
		string hash = raid.hash();
		bool is_update = false;
		for (int i=0; i<entries.Count; i++) {
			FileEntry entry = entries[i];
			if (entry[0] == hash) {
				is_update = true;
				entries[i] = raid.file_entry();
				break;
			}
		}

		// If the entry is a new entry, add it at the start.
		if (!is_update) {
			entries.Insert(0, raid.file_entry());
		}

		// Flatten list of entries.
		List<string> output = new ();
		foreach (FileEntry entry in entries) {
			foreach (string line in entry) {
				output.Add(line);
			}
		}

		// Update text file.
		lock (_lock) {
			File.WriteAllLines(_pathBuffer, output);
			File.Delete(_pathData);
			File.Move(_pathBuffer, _pathData);
		}
	}

	// Fetch raid data from saved datafile.
	// Returns null if a matching entry could not be found.
	public static Raid? get(int week, Day day) {
		return get(week, day, DefaultGroup);
	}
	public static Raid? get(int week, Day day, Group group) {
		return get(CurrentTier, week, day, group);
	}
	public static Raid? get(Tier tier, int week, Day day, Group group) {
		Util.CreateIfMissing(_pathData, _lock);

		string hash = new Raid(tier, week, day, group).hash();
		FileEntry? entry = get_file_entry(hash);
		if (entry is null) {
			return null;
		}

		Raid? raid = from_file_entry(entry);
		return raid;
	}

	// Reads the entire datafile and groups them into entries.
	static List<FileEntry> get_file_entries() {
		Util.CreateIfMissing(_pathData, _lock);

		List<FileEntry> entries = new ();
		FileEntry? entry = null;

		lock (_lock) {
			StreamReader file = new (_pathData);
			while (!file.EndOfStream) {
				string line = file.ReadLine() ?? "";
				if (!line.StartsWith(_indent)) {
					if (entry is not null) {
						entries.Add(entry);
					}
					entry = new ();
					entry.Add(line);
				} else if (entry is not null) {
					entry.Add(line);
				}
			}
			file.Close();
		}

		return entries;
	}
	// Fetch a single file entry matching the given hash.
	// This function exits early when possible.
	static FileEntry? get_file_entry(string hash) {
		Util.CreateIfMissing(_pathData, _lock);

		bool was_found = false;
		FileEntry entry = new ();

		// Look for matching raid data.
		lock (_lock) {
			StreamReader file = new (_pathData);
			while (!file.EndOfStream) {
				string line = file.ReadLine() ?? "";
				if (line == hash) {
					was_found = true;
					entry.Add(line);
					line = file.ReadLine() ?? "";
					while (line.StartsWith(_indent)) {
						entry.Add(line);
						line = file.ReadLine() ?? "";
					}
					// File stream is invalid now.
					// (next line is missing!)
					break;
				}
			}
			file.Close();
		}

		if (was_found) {
			return entry;
		} else {
			return null;
		}
	}

	static Raid? from_file_entry(FileEntry entry) {
		// Remove hash line.
		if (!entry[0].StartsWith(_indent)) {
			entry.RemoveAt(0);
		}

		// Create buffer variables.
		Tier? tier = null;
		int? week = null;
		Day? day = null;
		Group? group = null;
		string?
			summary = null,
			log_id = null;

		// Parse lines.
		foreach (string line in entry) {
			// Do not remove empty elements.
			string[] split = line.Trim().Split(_delim, 2);
			switch (split[0]) {
			case _keyTier:
				tier = Enum.Parse<Tier>(split[1]);
				break;
			case _keyWeek:
				week = int.Parse(split[1]);
				break;
			case _keyDay:
				day = Enum.Parse<Day>(split[1]);
				break;
			case _keyGroup:
				group = Enum.Parse<Group>(split[1]);
				break;
			case _keySummary:
				summary = split[1];
				if (summary == "") {
					summary = null;
				}
				break;
			case _keyLogId:
				log_id = split[1];
				if (log_id == "") {
					log_id = null;
				}
				break;
			}
		}

		// Check that all required fields are non-null.
		if (tier is null || week is null || day is null || group is null)
			{ return null; }

		// Create a raid object to return.
		// All arguments can be casted as non-null.
		Raid raid = new ((Tier)tier, (int)week, (Day)day, (Group)group) {
			summary = summary,
			log_id = log_id,
		};
		return raid;
	}

	public readonly Tier tier;
	public readonly Date date;
	public readonly Group group;
	public string? summary;
	public string? log_id;

	// Constructors.
	public Raid(int week, Day day) :
		this (week, day, DefaultGroup) { }
	public Raid(int week, Day day, Group group) :
		this (CurrentTier, week, day, group) { }
	public Raid(Tier tier, int week, Day day, Group group) {
		this.tier = tier;
		date = new Date(week, day);
		this.group = group;
		summary = null;
		log_id = null;
	}

	// Returns a uniquely identifiable string per raid+group.
	public string hash() {
		return $"{tier}{_sep}{date.Week}{_sep}{date.Day}{_sep}{group}";
	}

	// Returns a different emoji for each week of the tier.
	// The order is fixed between tiers.
	public string emoji() {
		int i = (date.Week - 1) % _raidEmojis.Count;
		return _raidEmojis[i];
	}

	// Convenience functions that return links to frequently-used
	// websites.
	public string? get_link_logs() {
		return $"{_fragLogs}{log_id}" ?? null;
	}
	public string? get_link_wipefest() {
		return $"{_fragWipefest}{log_id}" ?? null;
	}
	public string? get_link_analyzer() {
		return $"{_fragAnalyzer}{log_id}" ?? null;
	}

	// Returns a(n ordered) list of the instance's serialization.
	FileEntry file_entry() {
		FileEntry output = new ();
		output.Add(hash());
		output.Add($"{_indent}{_keyTier}{_delim}{tier}");
		output.Add($"{_indent}{_keyWeek}{_delim}{date.Week}");
		output.Add($"{_indent}{_keyDay}{_delim}{date.Day}");
		output.Add($"{_indent}{_keyGroup}{_delim}{group}");
		output.Add($"{_indent}{_keySummary}{_delim}{summary ?? ""}");
		output.Add($"{_indent}{_keyLogId}{_delim}{log_id ?? ""}");
		return output;
	}
}
