namespace Irene.Modules;

using FileEntry = List<string>;

class Raid {
	public enum Tier {
		EN, NH, ToV, ToS, ABT,
		Uldir, BoD, CoS, EP, NWC,
		CN, SoD,
	}
	public enum Day {
		Fri, Sat,
	}
	public enum Group {
		Spaghetti,
		Salad,
	}
	public record Date {
		public int week { get; init; }
		public Day day { get; init; }
	}

	public static readonly Group default_group = Group.Spaghetti;
	public static readonly Tier current_tier = Tier.SoD;
	public static readonly TimeSpan time = new (19, 0, 0);

	static readonly List<string> raid_emojis = new () {
	private static readonly object _lock = new ();
		":dolphin:", ":whale:"   , ":fox:"        , ":squid:"   ,
		":rabbit2:", ":bee:"     , ":butterfly:"  , ":owl:"     ,
		":shark:"  , ":swan:"    , ":lady_beetle:", ":sloth:"   ,
		":octopus:", ":bird:"    , ":turkey:"     , ":rooster:" ,
		":otter:"  , ":parrot:"  , ":elephant:"   , ":microbe:" ,
		":peacock:", ":chipmunk:", ":lion_face:"  , ":mouse:"   ,
		":snail:"  , ":giraffe:" , ":duck:"       , ":bat:"     ,
		":crab:"   , ":flamingo:", ":orangutan:"  , ":kangaroo:",
	};

	const string
		frag_logs = @"https://www.warcraftlogs.com/reports/",
		frag_wipefest = @"https://www.wipefest.gg/report/",
		frag_analyzer = @"https://wowanalyzer.com/report/";
	const string
		path_data = @"data/raids.txt",
		path_buffer = @"data/raids-buf.txt";
	const string sep = "-";
	const string indent = "\t";
	const string delim = "=";
	const string
		key_tier    = "tier",
		key_week    = "week",
		key_day     = "day",
		key_group   = "group",
		key_summary = "summary",
		key_log_id  = "log-id";

	// Returns the current week, calculated based on local time.
	public static int current_week() {
		TimeSpan duration = DateTime.UtcNow - Date_Season3.UtcResetTime();
		int week = (duration.Days / 7) + 1;   // int division
		return week;
	}

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
		lock (lock_data) {
			File.WriteAllLines(path_buffer, output);
			File.Delete(path_data);
			File.Move(path_buffer, path_data);
		}
	}

	// Fetch raid data from saved datafile.
	// Returns null if a matching entry could not be found.
	public static Raid? get(int week, Day day) {
		return get(week, day, default_group);
	}
	public static Raid? get(int week, Day day, Group group) {
		return get(current_tier, week, day, group);
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

		lock (lock_data) {
			StreamReader file = new (path_data);
			while (!file.EndOfStream) {
				string line = file.ReadLine() ?? "";
				if (!line.StartsWith(indent)) {
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
		lock (lock_data) {
			StreamReader file = new (path_data);
			while (!file.EndOfStream) {
				string line = file.ReadLine() ?? "";
				if (line == hash) {
					was_found = true;
					entry.Add(line);
					line = file.ReadLine() ?? "";
					while (line.StartsWith(indent)) {
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
		if (!entry[0].StartsWith(indent)) {
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
			string[] split = line.Trim().Split(delim, 2);
			switch (split[0]) {
			case key_tier:
				tier = Enum.Parse<Tier>(split[1]);
				break;
			case key_week:
				week = int.Parse(split[1]);
				break;
			case key_day:
				day = Enum.Parse<Day>(split[1]);
				break;
			case key_group:
				group = Enum.Parse<Group>(split[1]);
				break;
			case key_summary:
				summary = split[1];
				if (summary == "") {
					summary = null;
				}
				break;
			case key_log_id:
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
		this (week, day, default_group) { }
	public Raid(int week, Day day, Group group) :
		this (current_tier, week, day, group) { }
	public Raid(Tier tier, int week, Day day, Group group) {
		this.tier = tier;
		date = new Date() { week = week, day = day };
		this.group = group;
		summary = null;
		log_id = null;
	}

	// Returns a uniquely identifiable string per raid+group.
	public string hash() {
		return $"{tier}{sep}{date.week}{sep}{date.day}{sep}{group}";
	}

	// Returns a different emoji for each week of the tier.
	// The order is fixed between tiers.
	public string emoji() {
		int i = (date.week - 1) % raid_emojis.Count;
		return raid_emojis[i];
	}

	// Convenience functions that return links to frequently-used
	// websites.
	public string? get_link_logs() {
		return $"{frag_logs}{log_id}" ?? null;
	}
	public string? get_link_wipefest() {
		return $"{frag_wipefest}{log_id}" ?? null;
	}
	public string? get_link_analyzer() {
		return $"{frag_analyzer}{log_id}" ?? null;
	}

	// Returns a(n ordered) list of the instance's serialization.
	FileEntry file_entry() {
		FileEntry output = new ();
		output.Add(hash());
		output.Add($"{indent}{key_tier}{delim}{tier}");
		output.Add($"{indent}{key_week}{delim}{date.week}");
		output.Add($"{indent}{key_day}{delim}{date.day}");
		output.Add($"{indent}{key_group}{delim}{group}");
		output.Add($"{indent}{key_summary}{delim}{summary ?? ""}");
		output.Add($"{indent}{key_log_id}{delim}{log_id ?? ""}");
		return output;
	}
}
