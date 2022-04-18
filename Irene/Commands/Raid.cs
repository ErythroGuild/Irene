using System.Text.RegularExpressions;

using Irene.Modules;

namespace Irene.Commands;

class Raid : ICommand {
	private static readonly List<string> _dateExamples = new () {
		"today",
		"yesterday",
		"this friday",
		"this saturday",
		"last friday",
		"last saturday",
		"tomorrow",
	};

	private const string
		_commandRaid     = "raid"       ,
		_commandInfo     = "info"       ,
		_commandEligible = "eligibility",
		_commandViewLogs = "view-logs"  ,
		_commandSetLogs  = "set-logs"   ,
		_commandSetPlan  = "set-plan"   ,
		_commandCancel   = "cancel"     ;
	private const string _argDate = "date";

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@" `/raid info [share]` displays the plans for the upcoming raid,",
			//@" `/raid eligibility` checks raid requirements (and if you meet them),",
			//@":lock: `/raid eligibility <member>` checks raid requirements for a specific member.",
			@" `/raid view-logs <date>` shows the logs for the given date,",
			@":lock: `/raid set-logs <group> <date> <link>` sets the logs for the given date.",
			@":lock: `/raid set-plan <group> <date>` sets the plans for the given date's raid.",
			@":lock: `/raid cancel <date> [do-cancel]` marks raid that day as canceled.",
		} ) };
	}

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				_commandRaid,
				"Raid-related information.",
				options: new List<CommandOption> {
					new (
						_commandInfo,
						"View upcoming raid plans.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"share",
							"Whether to make visible to everyone.",
							ApplicationCommandOptionType.Boolean,
							required: false
						), }
					),
					//new (
					//	_commandEligible,
					//	"Check eligibility for raid.",
					//	ApplicationCommandOptionType.SubCommand,
					//	options: new List<CommandOption> { new (
					//		"member",
					//		"The member to check eligibility for.",
					//		ApplicationCommandOptionType.User,
					//		required: false,
					//		autocomplete: true
					//	), }
					//),
					new (
						_commandViewLogs,
						"View logs for a given date.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							_argDate,
							"The date to retrieve logs for.",
							ApplicationCommandOptionType.String,
							required: true,
							autocomplete: true
						), }
					),
					new (
						_commandSetLogs,
						"Set logs for a given date.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								"group",
								"The group to set logs for.",
								ApplicationCommandOptionType.String,
								required: true,
								new List<CommandOptionEnum> {
									new ("Spaghetti", RaidGroup.Spaghetti.ToString()),
									new ("Salad", RaidGroup.Salad.ToString()),
								}
							),
							new (
								_argDate,
								"The date to set logs for.",
								ApplicationCommandOptionType.String,
								required: true,
								autocomplete: true
							),
							new (
								"link",
								"The WarcraftLogs link.",
								ApplicationCommandOptionType.String,
								required: true
							),
						}
					),
					new (
						_commandSetPlan,
						"Set the raid plans for a raid.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								"group",
								"The group to set the raid plans for.",
								ApplicationCommandOptionType.String,
								required: true,
								new List<CommandOptionEnum> {
									new ("Spaghetti", RaidGroup.Spaghetti.ToString()),
									new ("Salad", RaidGroup.Salad.ToString()),
								}
							),
							new (
								_argDate,
								"The date to set the raid plans for.",
								ApplicationCommandOptionType.String,
								required: true,
								autocomplete: true
							),
						}
					),
					new (
						_commandCancel,
						"Cancel raid on a certain date.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								_argDate,
								"The date to cancel raid for.",
								ApplicationCommandOptionType.String,
								required: true,
								autocomplete: true
							),
							new (
								"do-cancel",
								"Whether or not to cancel raid.",
								ApplicationCommandOptionType.Boolean,
								required: false
							),
						}
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), RunAsync )
		};
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
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();
		string arg = (string)(args.GetArg(_argDate) ?? "");
		arg = arg.Trim().ToLower();

		if (arg == "") {
			await interaction.AutoCompleteResultsAsync(_dateExamples);
			return;
		}

		// Search through cached results.
		List<string> results = new ();
		foreach (string tag in _dateExamples) {
			if (tag.Contains(arg))
				results.Add(tag);
		}

		// Only return first 25 results.
		if (results.Count > 25)
			results = results.GetRange(0, 25);

		await interaction.AutoCompleteResultsAsync(results);
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

	private static RaidDate? ParseRaidDate(string arg) {
		DateOnly? date_raw = null;
		arg = arg.Trim().ToLower();

		DayOfWeek day_reset = DayOfWeek.Tuesday;
		Regex regex_closestWeekday = new (@"(?<position>this|last|next)\s+(?<day>friday|fri|f|saturday|sat|s)]");
		Regex regex_numberedWeekday = new (@"(?:(?<day1>friday|fri|f|saturday|sat|s)\s+)?(?<i>\d+)\s+weeks?\s+(?<position>ago|from\s+now)(?:\s+on\s+(?<day2>friday|fri|f|saturday|sat|s))?");

		static int DaysToWeekday(DayOfWeek basis, DateOnly input, DayOfWeek day) {
			int delta_input = (input.DayOfWeek - basis + 7) % 7;
			int delta_day = (day - basis + 7) % 7;
			return delta_day - delta_input;
		}
		static DayOfWeek ParseDay(string input) => input switch {
			"f" or "fri" or "friday" => DayOfWeek.Friday,
			"s" or "sat" or "saturday" => DayOfWeek.Saturday,
			_ => throw new ArgumentException("Invalid day of week.", nameof(input)),
		};

		switch (arg) {
		case "today":
			date_raw = DateOnly.FromDateTime(DateTime.Today);
			break;
		case "yesterday":
			date_raw = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
			break;
		case "tomorrow":
			date_raw = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
			break;
		case string s when regex_closestWeekday.IsMatch(s): {
			Match match = regex_closestWeekday.Match(s);
			DayOfWeek day = ParseDay(match.Groups["day"].Value);
			date_raw = DateOnly.FromDateTime(DateTime.Today);
			int delta = DaysToWeekday(day_reset, date_raw.Value, day);
			delta += match.Groups["position"].Value switch {
				"this" => 0,
				"last" => -7,
				"next" => 7,
				_ => throw new ArgumentException("Invalid positional relation.", nameof(arg)),
			};
			date_raw.Value.AddDays(delta);
			break; }
		case string s when regex_numberedWeekday.IsMatch(s): {
			Match match = regex_closestWeekday.Match(s);
			date_raw = DateOnly.FromDateTime(DateTime.Today);
			DayOfWeek day = match.Groups switch {
				GroupCollection g when g.ContainsKey("day1") => ParseDay(g["day1"].Value),
				GroupCollection g when g.ContainsKey("day2") => ParseDay(g["day2"].Value),
				_ => date_raw.Value.DayOfWeek,
			};
			int delta = DaysToWeekday(day_reset, date_raw.Value, day);
			int sign = match.Groups["position"].Value switch {
				"ago" => -1,
				string p when p.StartsWith("from") => 1,
				_ => throw new ArgumentException("Invalid positional relation.", nameof(arg)),
			};
			delta += sign * 7 * int.Parse(match.Groups["i"].Value);
			date_raw.Value.AddDays(delta);
			break; }
		default: {
			bool isDate = DateOnly.TryParse(arg, out DateOnly date_parsed);
			date_raw = isDate ? date_parsed : null;
			break; }
		}

		// Filter out invalid (non-raid) days.
		if (date_raw is null)
			return null;
		if (date_raw.Value.DayOfWeek is not
			(DayOfWeek.Friday or DayOfWeek.Saturday)
		) { return null; }

		RaidDate? date =
			Modules.Raid.CalculateRaidDate((DateOnly)date_raw);
		return date;
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
}
