using System.Text.RegularExpressions;

using Irene.Modules;

namespace Irene.Commands;

using RaidObj = Modules.Raid;

class Raid {
	private const string
		_commandRaid     = "raid"       ,
		_commandInfo     = "info"       ,
		_commandEligible = "eligibility",
		_commandViewLogs = "view-logs"  ,
		_commandSetLogs  = "set-logs"   ,
		_commandSetPlan  = "set-plan"   ,
		_commandCancel   = "cancel"     ;

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@" `/raid info` displays the plans for the upcoming raid,",
			@" `/raid eligibility` checks raid requirements (and if you meet them),",
			@":lock: `/raid eligibility <member>` checks raid requirements for a specific member.",
			@" `/raid view-logs <date>` shows the logs for the given date,",
			@":lock: `/raid set-logs <group> <date> <link>` sets the logs for the given date.",
			@":lock: `/raid set-plan <group> <date>` sets the plans for the given date's raid.",
			@":lock: `/raid cancel <date>` marks raid that day as canceled.",
		} ) };
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }

	public static List<AutoCompleteHandler> AutoComplete   { get => new () {
		new (_commandRaid, AutoCompleteAsync),
	}; }

	private static async Task RunAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string command = args[0].Name;

		// Check for permissions.
		switch (command) {
		case _commandSetLogs:
		case _commandSetPlan:
		case _commandCancel:
			bool doContinue = await
				interaction.CheckAccessAsync(AccessLevel.Officer);
			if (!doContinue)
				return;
			break;
		}

		// Dispatch the correct subcommand.
		InteractionHandler subcommand = command switch {
			_commandInfo     => ShowInfoAsync,
			_commandEligible => CheckEligibleAsync,
			_commandViewLogs => ViewLogsAsync,
			_commandSetLogs  => SetLogsAsync,
			_commandSetPlan  => SetPlanAsync,
			_commandCancel   => CancelAsync,
			_ => throw new ArgumentException("Unrecognized subcommand.", nameof(interaction)),
		};
		await subcommand(interaction);
		return;
	}

	private static async Task AutoCompleteAsync(TimedInteraction interaction) {

	}

	private static async Task ShowInfoAsync(TimedInteraction interaction) {

	}

	private static async Task CheckEligibleAsync(TimedInteraction interaction) {
		throw new NotImplementedException("Eligibility checking not implemented yet.");
	}

	private static async Task ViewLogsAsync(TimedInteraction interaction) {

	}

	private static async Task SetLogsAsync(TimedInteraction interaction) {

	}

	private static async Task SetPlanAsync(TimedInteraction interaction) {

	}

	private static async Task CancelAsync(TimedInteraction interaction) {

	}

	public static void set_logs(Command cmd) {
		//string args = cmd.args.Trim();
		//if (args == "") {
		//	Log.Information("  No arguments provided.");
		//	StringWriter text_err = new ();
		//	text_err.WriteLine("No arguments specified; you must at least provide a link.");
		//	text_err.WriteLine("See `@Irene -help logs-set` for more help.");
		//	_ = cmd.msg.RespondAsync(text_err.ToString());
		//	return;
		//}

		//// Format arguments and set reasonable defaults.
		//string[] split = cmd.args.Split(" ", 3);
		//RaidGroup? group = RaidObj.DefaultGroup;
		//RaidDay? day = DateTimeOffset.Now.DayOfWeek switch {
		//	DayOfWeek.Friday   => RaidDay.Fri,
		//	DayOfWeek.Saturday => RaidDay.Sat,
		//	_ => null,
		//};
		//string? log_id = null;
		
		//// Parse arguments.
		//switch (split.Length) {
		//case 1:
		//	log_id = parse_log_id(split[0]);
		//	break;
		//case 2:
		//	log_id = parse_log_id(split[1]);
		//	split[0] = split[0].ToLower();
		//	group = parse_group(split[0]) ?? group;
		//	day = parse_day(split[0]) ?? day;
		//	break;
		//case 3:
		//	log_id = parse_log_id(split[2]);
		//	split[0] = split[0].ToLower();
		//	split[1] = split[1].ToLower();
		//	group = parse_group(split[0]) ?? parse_group(split[1]);
		//	day = parse_day(split[1]) ?? parse_day(split[0]);
		//	break;
		//}

		//// Validate arguments.
		//if (group is null || day is null || log_id is null) {
		//	Log.Information("  Insufficient information provided.");
		//	StringWriter text_err = new ();
		//	text_err.WriteLine("Couldn't (completely) parse the given arguments.");
		//	text_err.WriteLine("See `@Irene -help logs-set` for more help.");
		//	_ = cmd.msg.RespondAsync(text_err.ToString());
		//	return;
		//}

		//// Set data appropriately.
		//RaidObj raid =
		//	RaidObj.get(RaidObj.CurrentWeek, (RaidDay)day, (RaidGroup)group) ??
		//	new (RaidObj.CurrentWeek, (RaidDay)day, (RaidGroup)group);
		//string? log_id_prev = raid.LogId;
		//raid.LogId = log_id;
		//RaidObj.update(raid);

		//// Respond.
		//Log.Information($"  Updating log id: {log_id}");
		//StringWriter text = new ();
		//text.WriteLine($"The logs have been successfully set to `{log_id}`.");
		//if (log_id_prev is not null) {
		//	Log.Information($"    previous logs overwritten: {log_id_prev}");
		//	text.WriteLine($"A previous log already existed: `{log_id_prev}`.");
		//	text.WriteLine("**This has been overwritten.**");
		//}
		//text.WriteLine($"Updating announcement post.");
		//_ = cmd.msg.RespondAsync(text.ToString());

		//// Edit corresponding announcement post.
		//WeeklyEvent.update_raid_logs(raid);
	}

	// Parse arguments to data objects.
	// Returns null if ambiguous / not recognized.
	static RaidDay? parse_day(string str) {
		Dictionary<RaidDay, List<string>> dict = new () {
			{ RaidDay.Fri, new () {
				"f",
				"fri",
				"fri.",
				"friday",
				"1",
				"reclear",
			} },
			{ RaidDay.Sat, new () {
				"s",
				"sat",
				"sat.",
				"saturday",
				"2",
				"prog",
				"progression",
			} },
		};

		str = str.Trim().ToLower();
		foreach (RaidDay day in dict.Keys) {
			if (dict[day].Contains(str)) {
				return day;
			}
		}
		return null;
	}
	static RaidGroup? parse_group(string str) {
		Dictionary<RaidGroup, List<string>> dict = new () {
			{ RaidGroup.Spaghetti, new () {
				":spaghetti:",
				"\U0001F35D",
				"spaghetti",
				"spaghet",
			}},
			{ RaidGroup.Salad, new () {
				":salad:",
				"\U0001F957",
				"salad",
			}},
		};

		str = str.Trim().ToLower();
		foreach (RaidGroup group in dict.Keys) {
			if (dict[group].Contains(str)) {
				return group;
			}
		}
		return null;
	}
	static string? parse_log_id(string str) {
		Regex regex = new (@"(?:https?\:\/\/www\.warcraftlogs\.com\/reports\/)?(?<id>\w+)(?:#.+)?");
		Match match = regex.Match(str);
		if (!match.Success)
			{ return null; }
		string id = match.Groups["id"].Value;
		return id;
	}
}
