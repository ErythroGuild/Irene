using static Irene.RecurringEvent;

namespace Irene.Modules;

static partial class RecurringEvents {
	// Used in module initialization.
	// For timezone conversion, see:
	// https://www.timeanddate.com/worldclock/converter.html?p1=234
	private static async Task<List<Event>> GetEvents_Maintenance() {
		TimeSpan t0 = TimeSpan.Zero;
		List<Task<Event?>> event_tasks = new () {
			Event.Create(
				"Irene maintenance: backup raid log data",
				RecurPattern.FromNthDayOfWeek(
					new (new (10, 0), TimeZone_PT),
					n: 1, DayOfWeek.Tuesday,
					months: 2
				),
				new (
					new (2022, 4, 5, 18, 0, 0, t0),
					new (2022, 4, 5)
				),
				Event_IreneBackupRaidLogData,
				TimeSpan.FromDays(21) // 3 weeks
			),
			Event.Create(
				"Irene maintenance: backup events data",
				RecurPattern.FromNthDayOfWeek(
					new (new (10, 0), TimeZone_PT),
					n: 1, DayOfWeek.Tuesday,
					months: 6
				),
				new (
					new (2022, 4, 5, 18, 0, 0, t0),
					new (2022, 4, 5)
				),
				Event_IreneBackupEventsData,
				TimeSpan.FromDays(21) // 3 weeks
			),
		};

		return await InitEventListAsync(event_tasks);
	}

	private static async Task Event_IreneBackupRaidLogData(DateTimeOffset _) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		const string t = "\u2003";
		const string a = "\u21D2";

		// Read in path data.
		string? dir_data = null;
		string? dir_backup = null;
		string? id_owner_str = null;
		lock (_lockDataDir) {
			using StreamReader file = File.OpenText(_pathDataDir);
			dir_data = file.ReadLine();
			dir_backup = file.ReadLine();
			id_owner_str = file.ReadLine();
		}

		// Exit early if bot owner not found.
		if (id_owner_str is null) {
			Log.Error("  No user ID for bot owner found.");
			Log.Debug("    File: {Path}", _pathDataDir);
			return;
		}

		// Construct message.
		List<string> text = new ()
			{ $":file_cabinet: Back up raid log data! Make a copy of `raids.txt`." };
		if (dir_data is not null && dir_backup is not null) {
			text.Add($"{t}{a} from:   `{dir_data}`");
			text.Add($"{t}{a} to:        `{dir_backup}`");
		}

		// Send message.
		ulong id_owner = ulong.Parse(id_owner_str);
		DiscordMember member_owner =
			await Guild.GetMemberAsync(id_owner);
		await member_owner.SendMessageAsync(string.Join("\n", text));
	}

	private static async Task Event_IreneBackupEventsData(DateTimeOffset _) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		const string t = "\u2003";
		const string a = "\u21D2";

		// Read in path data.
		string? dir_data = null;
		string? dir_backup = null;
		string? id_owner_str = null;
		lock (_lockDataDir) {
			using StreamReader file = File.OpenText(_pathDataDir);
			dir_data = file.ReadLine();
			dir_backup = file.ReadLine();
			id_owner_str = file.ReadLine();
		}

		// Exit early if bot owner not found.
		if (id_owner_str is null) {
			Log.Error("  No user ID for bot owner found.");
			Log.Debug("    File: {Path}", _pathDataDir);
			return;
		}

		// Construct message.
		List<string> text = new()
			{ $":file_cabinet: Back up event last-executed data! Make a copy of `events.txt`." };
		if (dir_data is not null && dir_backup is not null) {
			text.Add($"{t}{a} from:   `{dir_data}`");
			text.Add($"{t}{a} to:        `{dir_backup}`");
		}

		// Send message.
		ulong id_owner = ulong.Parse(id_owner_str);
		DiscordMember member_owner =
			await Guild.GetMemberAsync(id_owner);
		await member_owner.SendMessageAsync(string.Join("\n", text));
	}
}