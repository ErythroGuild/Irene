namespace Irene.Modules;

class Minigame {
	public enum Game {
		RPS, RPSLS,
		Morra,
		Balloon,
		Duel, Duel2,
	};

	public record struct Record {
		public static Record Empty => new (0, 0);
		private const string _separator = "-";

		public int Wins { get; set; }
		public int Losses { get; set; }
		public int Total =>
			Wins + Losses;
		public double Winrate =>
			(double)Wins / Total;

		public Record(int wins, int losses) {
			Wins = wins;
			Losses = losses;
		}

		// Serialization / deserialization.
		public string Serialize() =>
			string.Join(_separator, Wins, Losses);
		public static Record Deserialize(string data) {
			string[] split = data.Split(_separator, 2);
			int wins = int.Parse(split[0]);
			int losses = int.Parse(split[1]);
			return new (wins, losses);
		}
	}

	private static readonly object _lock = new ();
	private const string
		_pathScores = @"data/minigame-scores.txt",
		_pathTemp = @"data/minigame-scores-temp.txt";
	private const string _indent = "\t";
	private const string _delimiter = ":";

	public static void Init() { }
	static Minigame() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		Util.CreateIfMissing(_pathScores, _lock);

		Log.Information("  Initialized module: Minigame");
		Log.Debug("    Score datafile initialized.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public static string DisplayName(Game game) => game switch {
		Game.RPS     => "Rock-Paper-Scissors",
		Game.RPSLS   => "Rock-Paper-Scissors-Lizard-Spock",
		Game.Morra   => "Morra",
		Game.Balloon => "The Balloon Game",
		Game.Duel    => "DB Duel",
		Game.Duel2   => "DB Duel (Advanced)",
		_ => "",
	};

	public static string GetRules(Game game) => game switch {
		Game.RPS =>
			"""
			:right_facing_fist: beats :v:
			:v: beats :hand_splayed:
			:hand_splayed: beats :left_facing_fist:
			""",
		Game.RPSLS =>
			@"https://imgur.com/P3dxSF5",
		Game.Morra =>
			"""
			Pick a number to show, and then guess the sum of your number + your opponent's number.
			The person who guesses correctly wins.
			""",
		Game.Balloon =>
			"""
			The balloon will pop after a predetermined number of pumps. :balloon:
			During your turn, you may pump as many times as possible, but always at least once.
			The person who blows up the balloon loses.
			""",
		Game.Duel =>
			"""
			Some moves require charges to use. You can hold a max of 5 charges.
			(You forfeit if you don't have enough charges for the selected move.)

			**Blast** beats **Charge**.
			**Block** neutralizes **Blast**.
			**Mega-blast** beats **Blast**, **Block**, and **Charge**.
	
			**Block** costs nothing.
			**Blast** costs **1** charge.
			**Mega-blast** costs **2** charges.
			""",
		Game.Duel2 =>
			"""
			*Same rules as regular DB Duel, but with additional moves.*
			Some moves require charges to use. You can hold a max of 7 charges.
			(You forfeit if you don't have enough charges for the selected move.)

			**Blast** beats **Charge**.
			**Block** neutralizes **Blast**.
			**Mega-blast** beats **Blast**, **Block**, and **Charge**.
			**Mega-block** neutralizes **Blast** and **Mega-blast**.
			**Explosion** beats everything.

			**Block** costs nothing.
			**Mega-block** costs **1** charge.
			**Blast** costs **1** charge.
			**Mega-blast** costs **2** charges.
			**Explosion** costs **3** charges.
			""",
		_ => throw new ArgumentException("Unknown game.", nameof(game)),
	};

	// Returns the record for a specific game for a specific user.
	public static Record GetRecord(ulong id, Game game) {
		IDictionary<Game, Record> records = GetRecords(id);
		return records.ContainsKey(game)
			? records[game]
			: Record.Empty;
	}

	// Returns a list of records (of games) for a specific user.
	// This method is more efficient than GetRecords(Game).
	public static IDictionary<Game, Record> GetRecords(ulong id) {
		Dictionary<Game, Record> records = new ();

		string? entry = GetEntry(id);
		// Return empty list if no records found.
		if (entry is null)
			return records;

		string[] lines = entry.Split("\n");
		foreach (string line_i in lines) {
			// Skip first line (user ID).
			if (!line_i.StartsWith(_indent))
				continue;
			
			string line = line_i.Replace(_indent, "");
			string[] split = line.Split(_delimiter);
			Game game = Enum.Parse<Game>(split[0]);
			Record record = Record.Deserialize(split[1]);
			records.Add(game, record);
		}
		return records;
	}

	// Collates a list of records (of users) for a specific game.
	// This method is less efficient than GetRecords(ulong).
	public static IDictionary<ulong, Record> GetRecords(Game game) {
		Dictionary<ulong, Record> records = new ();
		string key = $"{_indent}{game}{_delimiter}";

		List<string> entries = GetAllEntries();
		foreach (string entry in entries) {
			ulong? id = null;
			Record? record = null;
			string[] lines = entry.Split("\n");
			foreach (string line in lines) {
				if (!line.StartsWith(_indent)) {
					id = ulong.Parse(line);
				} else if (line.StartsWith(key)) {
					string[] split = line.Split(_delimiter, 2);
					record = Record.Deserialize(split[1]);
				}
			}
			if (id is not null && record is not null)
				records.Add(id.Value, record.Value);
		}

		return records;
	}

	// Resets or updates the records for a specific game for a
	// specific user.
	public static void ResetRecord(ulong id, Game game)
		{ UpdateRecord(id, game, Record.Empty); }
	public static void UpdateRecord(ulong id, Game game, Record record) {
		IDictionary<Game, Record> records = GetRecords(id);
		records[game] = record;

		// Create updated (sorted) entry.
		string entry = id.ToString();
		List<string> game_data = new ();
		foreach (Game game_i in records.Keys) {
			// Only write non-empty records.
			if (records[game_i] == Record.Empty)
				continue;
			string record_game = records[game_i].Serialize();
			string line = $"{_indent}{game_i}{_delimiter}{record_game}";
			game_data.Add(line);
		}
		game_data.Sort((string x, string y) => {
			string[] split_x = x.Split(_delimiter, 2);
			string[] split_y = y.Split(_delimiter, 2);
			return split_x[0].CompareTo(split_y[0]);
		});
		entry += "\n" + game_data.ToLines();

		// Update entry data.
		List<string> entries = GetAllEntries();
		List<string> entries_new = new ();
		bool did_replace = false;
		foreach (string entry_i in entries) {
			if (entry_i.StartsWith(id.ToString())) {
				entries_new.Add(entry);
				did_replace = true;
			} else {
				entries_new.Add(entry_i);
			}
		}
		if (!did_replace)
			entries_new.Add(entry);

		// Remove all empty entries.
		entries_new.RemoveAll(
			(entry_i) => !entry_i.Contains('\n')
		);

		// Write to file.
		entries_new.Sort();
		lock (_lock) {
			File.WriteAllLines(_pathTemp, entries_new);
			File.Delete(_pathScores);
			File.Move(_pathTemp, _pathScores);
		}
	}

	// Group the entire score file into entries for each user.
	private static List<string> GetAllEntries() {
		List<string> entries = new ();
		string? entry = null;

		lock (_lock) {
			using StreamReader file = File.OpenText(_pathScores);
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
	// Fetch the first entry of the given user ID.
	// Returns null if no results were found.
	private static string? GetEntry(ulong id) {
		bool was_found = false;
		string entry = "";
		string id_string = id.ToString();

		lock (_lock) {
			using StreamReader file = File.OpenText(_pathScores);
			while (!file.EndOfStream) {
				string line = file.ReadLine() ?? "";
				if (line == id_string) {
					was_found = true;
					entry = line;
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
}
