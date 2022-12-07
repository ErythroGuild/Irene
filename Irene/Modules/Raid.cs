namespace Irene.Modules;

using System.Linq;
using System.Text.RegularExpressions;

using TierBasisPair = Tuple<Raid.RaidTier?, DateOnly?>;
using GroupDataPair = KeyValuePair<Raid.RaidGroup, Raid.RaidData>;

class Raid {
	public enum RaidTier {
		EN, NH, ToV, ToS, ABT,
		Uldir, BoD, EP, NWC,
		CN, SoD, SFO,
	}
	public enum RaidDay {
		Friday, Saturday,
	}
	public enum RaidGroup {
		Spaghetti,
		Salad,
	}
	public readonly record struct RaidDate {
		public readonly RaidTier Tier;
		public readonly int Week;
		public readonly RaidDay Day;

		// Returns a uniquely identifiable string per date.
		public string Hash =>
			string.Join(_separator, new object[] { Tier, Week, Day });
		// Each week of the tier has a different emoji associated
		// with it; the order is fixed between tiers.
		public string Emoji { get {
			int i = (Week - 1) % _emojis.Count;
			return _emojis[i];
		} }

		// Direct constructor (if tier/week data are known).
		public RaidDate(RaidTier tier, int week, RaidDay day) {
			Tier = tier;
			Week = week;
			Day = day;
		}

		// Convenience factory methods, taking actual date objects.
		public static RaidDate? TryCreate(DateTimeOffset dateTime) {
			DateTimeOffset dateTime_server =
				TimeZoneInfo.ConvertTime(
					dateTime.ToUniversalTime(),
					TimeZone_Server);
			DateOnly date =
				DateOnly.FromDateTime(dateTime_server.DateTime);
			return TryCreate(date);
		}
		public static RaidDate? TryCreate(DateOnly date) {
			// Find / check raid day.
			RaidDay? day = date.DayOfWeek switch {
				DayOfWeek.Friday => RaidDay.Friday,
				DayOfWeek.Saturday => RaidDay.Saturday,
				_ => null,
			};
			if (day is null)
				return null;

			// Look up raid tier.
			DateTimeOffset date_time = date.UtcResetTime();
			(RaidTier? tier, DateOnly? basis) = date_time switch {
				DateTimeOffset d when d <  Date_Season1.UtcResetTime() => new (null, null),
				DateTimeOffset d when d <  Date_Season2.UtcResetTime() => new (RaidTier.CN , Date_Season1),
				DateTimeOffset d when d <  Date_Season3.UtcResetTime() => new (RaidTier.SoD, Date_Season2),
				DateTimeOffset d when d >= Date_Season3.UtcResetTime() => new (RaidTier.SFO, Date_Season3),
				_ => new TierBasisPair(null, null),
			};
			if (tier is null || basis is null)
				return null;

			// Calculate raid week.
			TimeSpan duration = date_time - basis.Value.UtcResetTime();
			int week = (duration.Days / 7) + 1; // int division

			return new RaidDate(tier.Value, week, day.Value);
		}

		// Serialization/deserialization.
		public string Serialize() =>
			string.Join(_separator, new object[] { Tier, Week, Day });
		public static RaidDate Deserialize(string input) {
			string[] split = input.Split(_separator, 3);
			return new RaidDate(
				Enum.Parse<RaidTier>(split[0]),
				int.Parse(split[1]),
				Enum.Parse<RaidDay>(split[2])
			);
		}

		private static readonly ReadOnlyCollection<string> _emojis =
			new (new List<string> {
				":dolphin:", ":whale:"   , ":fox:"        , ":squid:"   ,
				":rabbit2:", ":bee:"     , ":butterfly:"  , ":owl:"     ,
				":shark:"  , ":swan:"    , ":lady_beetle:", ":sloth:"   ,
				":octopus:", ":bird:"    , ":turkey:"     , ":rooster:" ,
				":otter:"  , ":parrot:"  , ":elephant:"   , ":microbe:" ,
				":peacock:", ":chipmunk:", ":lion_face:"  , ":mouse:"   ,
				":snail:"  , ":giraffe:" , ":duck:"       , ":bat:"     ,
				":crab:"   , ":flamingo:", ":orangutan:"  , ":kangaroo:",
			});
		private const string _separator = ",";
	}
	public record class RaidData {
		public string? LogId { get; set; }

		// Convenience functions for accessing different log websites.
		public string? UrlLogs     => LogId is null ? null : $"{_fragLogs}{LogId}";
		public string? UrlWipefest => LogId is null ? null : $"{_fragWipefest}{LogId}";
		public string? UrlAnalyzer => LogId is null ? null : $"{_fragAnalyzer}{LogId}";

		public RaidData(string? logId) {
			LogId = logId;
		}

		private const string
			_fragLogs     = @"https://www.warcraftlogs.com/reports/",
			_fragWipefest = @"https://www.wipefest.gg/report/",
			_fragAnalyzer = @"https://wowanalyzer.com/report/";
	}

	// RaidGroup-related functionality.
	public static RaidGroup DefaultGroup = RaidGroup.Spaghetti;
	public static string GroupEmoji(RaidGroup group) =>
		group switch {
			RaidGroup.Spaghetti => ":spaghetti:",
			RaidGroup.Salad => ":salad:",
			_ => throw new ArgumentException("Unknown RaidGroup type.", nameof(group)),
		};
	
	// Internal static data.
	private static readonly object _lock = new ();
	private const string _pathData = @"data/raids.txt";
	private const string _indent = "\t";
	private const string _delim = "=";
	private const string
		_keyRaidDate  = "raid-date",
		_keyDoCancel  = "canceled",
		_keySummary   = "summary",
		_keyMessageId = "message-id",
		_keyGroup     = "group",
		_keyLogId     = "log-id";

	static Raid() {
		// Make sure datafile exists.
		Util.CreateIfMissing(_pathData);

		Log.Information("  Initialized module: Raid");
		Log.Debug("    Raid data file checked.");
	}

	// Returns null if the link is ill-formed.
	public static string? ParseLogId(string link) {
		Match match = Regex.Match(
			link,
			@"(?:(?:https?\:\/\/)?(?:www\.)?warcraftlogs\.com\/reports\/)?(?<id>\w+)(?:#.+)?"
		);
		if (!match.Success)
			return null;
		string id = match.Groups["id"].Value;
		return id;
	}

	// Fetch raid data from data file.
	// Returns a fresh entry if a matching entry could not be found
	// (default-initialized).
	public static Raid Fetch(RaidDate date) {
		Raid raid_default = new (date);
		string? entry = ReadEntry(raid_default.Hash);
		return (entry is null)
			? raid_default
			: Deserialize(entry) ?? raid_default;
	}

	// Reads the entire data file and groups them into entries.
	private static List<string> ReadAllEntries() {
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
	private static string? ReadEntry(string hash) {
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

	// Properties.
	public RaidDate Date { get; init; }
	public bool DoCancel { get; set; }
	public string? Summary { get; set; }
	public ulong? MessageId { get; set; }
	public Dictionary<RaidGroup, RaidData> Data { get; init; }
	public string Hash => Date.Hash;
	public string Emoji => Date.Emoji;
	public string LogLinks { get {
		CheckErythroInit();

		DiscordEmoji
			emoji_wipefest = Erythro.Emoji(id_e.wipefest),
			emoji_analyzer = Erythro.Emoji(id_e.wowanalyzer),
			emoji_logs = Erythro.Emoji(id_e.warcraftlogs);

		List<string> lines = new ();
		List<GroupDataPair> data = Data
			.ToList()
			.FindAll((pair) => pair.Value.LogId is not null);
		switch (data.Count) {
		case 1:
			foreach (GroupDataPair pair in data) {
				RaidData data_i = pair.Value;
				lines.Add($"{emoji_wipefest} - <{data_i.UrlWipefest}>");
				lines.Add($"{emoji_analyzer} - <{data_i.UrlAnalyzer}>");
				lines.Add($"{emoji_logs} - <{data_i.UrlLogs}>");
			}
			break;
		case >1:
			foreach (GroupDataPair pair in data) {
				(RaidGroup group, RaidData data_i) = pair;
				string emoji = GroupEmoji(group);
				lines.Add($"{emoji} **{group}** {emoji}");
				lines.Add($"{emoji_wipefest} - <{data_i.UrlWipefest}>");
				lines.Add($"{emoji_analyzer} - <{data_i.UrlAnalyzer}>");
				lines.Add($"{emoji_logs} - <{data_i.UrlLogs}>");
				lines.Add("");
			}
			lines.RemoveAt(lines.Count - 1);
			break;
		}

		return lines.ToLines();
	} }
	public string AnnouncementText =>
		$"{Emoji} {Erythro.Role(id_r.raid).Mention} - Forming now!";

	// Constructor / serialization / deserialization.
	private Raid(RaidDate date) {
		Date = date;
		DoCancel = false;
		Summary = null;
		Data = new Dictionary<RaidGroup, RaidData>();
		MessageId = null;
	}
	private string? Serialize() {
		// Return null if entry is all default values.
		if (DoCancel == false &&
			Summary is null &&
			MessageId is null &&
			Data.Count == 0
		) { return null; }

		List<string> lines = new () {
			Hash,
			$"{_indent}{_keyRaidDate}{_delim}{Date.Serialize()}",
			$"{_indent}{_keyDoCancel}{_delim}{DoCancel}",
			$"{_indent}{_keySummary}{_delim}{Summary ?? ""}",
			$"{_indent}{_keyMessageId}{_delim}{MessageId?.ToString() ?? ""}",
		};
		foreach (RaidGroup group in Data.Keys) {
			if (Data[group].LogId is not null) {
				lines.Add($"{_indent}{_keyGroup}{_delim}{group}");
				lines.Add($"{_indent}{_indent}{_keyLogId}{_delim}{Data[group].LogId ?? ""}");
			}
		}

		return lines.ToLines();
	}
	// Returns null if the data is underspecified.
	private static Raid? Deserialize(string entry) {
		string _indent2 = _indent + _indent;
		// Skip first line (hash).
		string[] lines = entry.Split("\n")[1..];

		// Buffer variables for deserialization.
		RaidDate? raidDate = null;
		bool? doCancel = null;
		string? summary = null;
		ulong? messageId = null;
		Dictionary<RaidGroup, RaidData> data = new ();

		// Parse all the lines.
		int line_count = lines.Length;
		for (int i=0; i<line_count; i++) {
			// Remove first indent and split into key/value pair.
			string[] split = lines[i][_indent.Length..]
				.Split(_delim, 2);
			(string key, string value) = (split[0], split[1]);

			// Assign all data.
			switch (key) {
			case _keyRaidDate:
				raidDate = RaidDate.Deserialize(value);
				break;
			case _keyDoCancel:
				doCancel = bool.Parse(value);
				break;
			case _keySummary:
				summary = (value != "") ? value : null;
				break;
			case _keyMessageId:
				messageId = (value != "") ? ulong.Parse(value) : null;
				break;
			case _keyGroup: {
				RaidGroup? group = Enum.Parse<RaidGroup>(value);
				string? logId = null;

				// Parse all group sub-entries.
				while (i+1 < line_count && lines[i+1].StartsWith(_indent2)) {
					i++;
					split = lines[i][_indent2.Length..]
						.Split(_delim, 2);
					(key, value) = (split[0], split[1]);
					switch (key) {
					case _keyLogId:
						logId = (value != "") ? value : null;
						break;
					}
				}

				// Assign data (or break).
				if (group is null || logId is null)
					break;
				data.Add(group!.Value, new (logId));
				break; }
			}
		}

		// Check that all required fields are non-null.
		if (raidDate is null || doCancel is null)
			return null;

		// Create a raid object to return.
		return new Raid(raidDate!.Value) {
			DoCancel = doCancel!.Value,
			Summary = summary,
			MessageId = messageId,
			Data = data,
		};
	}

	// Syntax sugar to call the static methods.
	public async Task UpdateAnnouncement() {
		CheckErythroInit();

		// Exit early if required data isn't available.
		if (MessageId is null) {
			Log.Warning("  Failed to update announcement logs for: {RaidHash}", Hash);
			Log.Information("    No announcement message set.");
			return;
		}

		// Contruct announcement text.
		List<GroupDataPair> data = Data
			.ToList()
			.FindAll((pair) => pair.Value.LogId is not null);
		string announcement = AnnouncementText;
		if (data.Count > 1)
			announcement += "\n";
		if (data.Count > 0)
			announcement += "\n" + LogLinks;

		// Fetch the announcement message and edit it.
		DiscordChannel announcements = Erythro.Channel(id_ch.announce);
		DiscordMessage message = await
			announcements.GetMessageAsync(MessageId.Value);
		await message.ModifyAsync(announcement);
	}
	// Overwrite / add this instance's data to the data file.
	public void UpdateData() {
		List<string> entries = ReadAllEntries();
		SortedList<Raid, string> entries_sorted =
			new (new RaidComparer());

		// Populate sorted list and add in updated data.
		bool didInsert = false;
		foreach (string entry in entries) {
			Raid? key = Deserialize(entry);
			if (key is null)
				continue;
			if (Hash == key.Hash) {
				string? serialized = Serialize();
				if (serialized is not null)
					entries_sorted.Add(key, serialized);
				didInsert = true;
			}
			else {
				entries_sorted.Add(key, entry);
			}
		}
		if (!didInsert) {
			string? serialized = Serialize();
			if (serialized is not null)
				entries_sorted.Add(this, serialized);
		}

		// Update data file.
		lock (_lock) {
			File.WriteAllLines(_pathData.Temp(), entries_sorted.Values);
			File.Delete(_pathData);
			File.Move(_pathData.Temp(), _pathData);
		}
	}

	// Reverse-date comparer (for serialization).
	private class RaidComparer : Comparer<Raid> {
		public override int Compare(Raid? x, Raid? y) {
			if (x is null)
				throw new ArgumentNullException(nameof(x), "Attempted to compare null reference.");
			if (y is null)
				throw new ArgumentNullException(nameof(y), "Attempted to compare null reference.");

			if (x.Date.Tier != y.Date.Tier)
				return y.Date.Tier - x.Date.Tier;
			if (x.Date.Week != y.Date.Week)
				return y.Date.Week - x.Date.Week;
			if (x.Date.Day != y.Date.Day)
				return y.Date.Day - x.Date.Day;

			return 0;
		}
	}
}
