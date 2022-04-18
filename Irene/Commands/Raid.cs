using System.Text.RegularExpressions;

using Irene.Components;
using Irene.Modules;

using RaidObj = Irene.Modules.Raid;

namespace Irene.Commands;

class Raid : ICommand {
	private static readonly List<string> _dateExamples = new () {
		"today",
		"yesterday",
		"tomorrow",
		"this friday",
		"this saturday",
		"last friday",
		"last saturday",
		"next friday",
		"next saturday",
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
	private const string _idTagPlans = "tag_plans";

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
								_argDate,
								"The date to set logs for.",
								ApplicationCommandOptionType.String,
								required: true,
								autocomplete: true
							),
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
								_argDate,
								"The date to set the raid plans for.",
								ApplicationCommandOptionType.String,
								required: true,
								autocomplete: true
							),
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
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();
		bool doShare = (args.Count > 0)
			? (bool)args[0].Value
			: false;

		// Convert raid dates to RaidDates.
		DateOnly today = DateOnly.FromDateTime(DateTime.Now);
		DateOnly day_fri = today.NextDayOfWeek(DayOfWeek.Friday, true);
		DateOnly day_sat = today.NextDayOfWeek(DayOfWeek.Saturday, true);
		RaidDate? raidDate_fri = RaidObj.CalculateRaidDate(day_fri);
		RaidDate? raidDate_sat = RaidObj.CalculateRaidDate(day_sat);
		if (today.DayOfWeek is DayOfWeek.Saturday)
			raidDate_fri = null; // only show Saturday's plans

		// Fetch plans.
		RaidGroup[] raidGroups = Enum.GetValues<RaidGroup>();
		Dictionary<RaidGroup, string> plans_fri = new ();
		bool isCanceled_fri = false;
		if (raidDate_fri is not null) {
			foreach (RaidGroup group in raidGroups) {
				RaidObj raid =
					RaidObj.Fetch(raidDate_fri.Value, group);
				if (raid.DoCancel) {
					isCanceled_fri = true;
					break;
				}
				string? plans = raid.Summary;
				if (plans != null)
					plans_fri.Add(group, plans);
			}
		}
		Dictionary<RaidGroup, string> plans_sat = new ();
		bool isCanceled_sat = false;
		if (raidDate_sat is not null) {
			foreach (RaidGroup group in raidGroups) {
				RaidObj raid =
					RaidObj.Fetch(raidDate_sat.Value, group);
				if (raid.DoCancel) {
					isCanceled_sat = true;
					break;
				}
				string? plans = raid.Summary;
				if (plans != null)
					plans_sat.Add(group, plans);
			}
		}

		// Generate formatted timestamps.
		DateTimeOffset time_start =
			DateTime.Today + RaidObj.Time.ToTimeSpan();
		DateTimeOffset time_end =
			time_start + TimeSpan.FromHours(2);
		string time_start_str =
			time_start.Timestamp(Util.TimestampStyle.TimeShort);
		string time_end_str =
			time_end.Timestamp(Util.TimestampStyle.TimeShort);

		// Compose response.
		List<string> response = new () {
			$"Raids are on **Friday** + **Saturday**, from {time_start_str}~{time_end_str} server (CT).",
			//"Use `/raid eligibility` to check raid requirements.",
			"(if you don't meet raid requirements, talk to an officer! we'll figure something out)",
			"",
		};
		if (raidDate_fri is not null) {
			response.Add("**Friday**");
			if (isCanceled_fri) {
				response.Add("Raid is canceled. :desert:");
			} else {
				switch (plans_fri.Count) {
				case 0:
					response.Add("No specific plans set. :leaves:");
					break;
				case 1:
					response.Add(new List<string>(plans_fri.Values)[0]);
					break;
				case 2:
					foreach (RaidGroup group in plans_fri.Keys)
						response.Add($"{RaidObj.GroupEmoji(group)} - {plans_fri[group]}");
					break;
				}
			}
		}
		response.Add("**Saturday**");
		if (isCanceled_sat) {
			response.Add("Raid is canceled. :desert:");
		} else {
			switch (plans_sat.Count) {
			case 0:
				response.Add("No specific plans set. :leaves:");
				break;
			case 1:
				response.Add(new List<string>(plans_sat.Values)[0]);
				break;
			case 2:
				foreach (RaidGroup group in plans_sat.Keys)
					response.Add($"{RaidObj.GroupEmoji(group)} - {plans_sat[group]}");
				break;
			}
		}

		// Respond to interaction.
		await Command.RespondAsync(
			interaction,
			string.Join("\n", response), !doShare,
			"Sending raid info.",
			LogLevel.Debug,
			"Raid info sent."
		);
	}

	private static async Task CheckEligibleAsync(TimedInteraction interaction) {
		throw new NotImplementedException("Eligibility checking not implemented yet.");
	}

	private static async Task ViewLogsAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();

		RaidDate? date = ParseRaidDate((string)args[0].Value);
		// Filter out invalid dates.
		if (date is null) {
			await RespondInvalidDateAsync(
				interaction,
				(string)args[0].Value
			);
			return;
		}

		// Make sure Guild is initialized.
		if (Guild is null) {
			await Command.RespondAsync(
				interaction,
				":sweat_smile: Sorry, still starting up. Try again in just a moment.", true,
				"Guild not initialized before fetching log data.",
				LogLevel.Information,
				"Response sent."
			);
			return;
		}

		// Compose response by editing the date's announcement text.
		// The first line needs to be removed.
		string response = RaidObj.AnnouncementText(date.Value);
		response = response.Split("\n", 2)[1].Trim();
		RaidObj raid = new (date.Value, RaidObj.DefaultGroup); // only temporary
		response = $"Logs for {raid.Tier}" +
			$" **week {raid.Date.Week}**," +
			$" {raid.Date.Day}.:\n\n" + response;

		// Respond.
		await Command.RespondAsync(
			interaction,
			response, false,
			"Sending log data.",
			LogLevel.Debug,
			"Logs sent."
		);
	}

	private static async Task SetLogsAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();

		RaidDate? date = ParseRaidDate((string)args[0].Value);
		RaidGroup group = Enum.Parse<RaidGroup>((string)args[1].Value);
		string? logId = RaidObj.ParseLogId((string)args[2].Value);

		// Filter out invalid dates.
		if (date is null) {
			await RespondInvalidDateAsync(
				interaction,
				(string)args[0].Value
			);
			return;
		}

		// Filter out invalid log links.
		if (logId is null) {
			await Command.RespondAsync(
				interaction,
				"Could not extract log data from the provided link. :magnet:", true,
				"Unrecognized log link format.",
				LogLevel.Information,
				"Link provided: {Argument}",
				(string)args[2].Value
			);
			return;
		}

		// Save old log ID (if available), and set new one.
		RaidObj raid = RaidObj.Fetch(date.Value, group);
		string? logId_prev = raid.LogId;
		raid.LogId = logId;

		// Respond.
		string response =
			$"Setting the logs for {raid.Tier}" +
			$" **week {date.Value.Week}** ({date.Value.Day}.):" +
			$" `{logId}`.";
		if (logId_prev is not null)
			response += $"\n:pencil: Previous data overwritten: `{logId_prev}`";
		if (raid.MessageId is not null)
			response += "\n:scroll: Updating announcement post.";
		await Command.RespondAsync(
			interaction,
			response, false,
			"Setting logs.",
			LogLevel.Debug,
			"New log ID: {LogId}",
			logId
		);

		// Update existing data.
		raid.UpdateData();
		Log.Debug("  Data saved.");
		if (raid.MessageId is not null) {
			await raid.UpdateAnnouncement();
			Log.Debug("  Announcement updated.");
		}
	}

	private static async Task SetPlanAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();

		RaidDate? date = ParseRaidDate((string)args[0].Value);
		RaidGroup group = Enum.Parse<RaidGroup>((string)args[1].Value);

		// Filter out invalid dates.
		if (date is null) {
			await RespondInvalidDateAsync(
				interaction,
				(string)args[0].Value
			);
			return;
		}

		// Read in existing plans (if they exist).
		RaidObj raid = RaidObj.Fetch(date.Value, group);
		string plans = raid.Summary ?? "";
		plans = plans.Unescape();

		// Initialize modal components.
		string title = $"{raid.Tier} week {raid.Date.Week}, {raid.Date.Day}.";
		List<TextInputComponent> components = new () {
			new TextInputComponent("Raid Plans", _idTagPlans, value: plans, style: TextInputStyle.Paragraph),
		};

		async Task set_plans(ModalSubmitEventArgs e) {
			Stopwatch stopwatch = Stopwatch.StartNew();
			Log.Information("Modal submission received (id: {Id}).", e.Interaction.Data.CustomId);

			// Update raid object instance.
			Dictionary<string, TextInputComponent> fields =
				e.Interaction.GetModalComponents();
			string data_plans = fields[_idTagPlans].Value.Escape();
			raid.Summary = data_plans;
			raid.UpdateData();

			// Handle interaction.
			await Command.RespondAsync(
				new TimedInteraction(e.Interaction, stopwatch),
				$"Raid plans updated successfully.", false,
				"Updated raid plans successfully.",
				LogLevel.Debug,
				new Lazy<string>(() =>
					$"Plans updated: {raid.Tier} Week {raid.Date.Week} {raid.Date.Day}"
				)
			);
		}

		// Submit modal.
		await Command.RespondAsync(
			interaction,
			new Task(async () => await
				Modal.RespondAsync(interaction.Interaction, title, components, set_plans)
			),
			"Creating modal to set plans.",
			LogLevel.Debug,
			"Modal created. ({Tier} week {WeekNum})",
			raid.Tier, raid.Date.Week
		);
	}

	private static async Task CancelAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();

		RaidDate? date = ParseRaidDate((string)args[0].Value);
		bool doCancel = (args.Count > 1)
			? (bool)args[1].Value
			: true;

		// Filter out invalid dates.
		if (date is null) {
			await RespondInvalidDateAsync(
				interaction,
				(string)args[0].Value
			);
			return;
		}

		// Set raids as canceled.
		RaidGroup[] raidGroups = Enum.GetValues<RaidGroup>();
		foreach (RaidGroup raidGroup in raidGroups) {
			RaidObj raid = RaidObj.Fetch(date.Value, raidGroup);
			if (raid.DoCancel != doCancel) {
				raid.DoCancel = doCancel;
				raid.UpdateData();
			}
		}

		// Respond.
		string response =
			"Raid cancel status has been successfully set. :ok_hand:" +
			$"\n(week {date.Value.Week}, {date.Value.Day}.)";
		await Command.RespondAsync(
			interaction,
			response, false,
			"Raid cancel status set.",
			LogLevel.Debug,
			new Lazy<string>(() => {
				string s = doCancel
					? "Raid canceled"
					: "Raid un-canceled";
				s += $": week {date.Value.Week}, {date.Value.Day}.";
				return s;
			})
		);
	}

	private static async Task RespondInvalidDateAsync(TimedInteraction interaction, string arg) {
		string response = "The date you specified was not a valid raid day. :calendar:";
		await Command.RespondAsync(
			interaction,
			response, true,
			@"Invalid/unrecognized raid date specified.",
			LogLevel.Information,
			"Date specified: {Argument}",
			arg
		);
	}

	private static RaidDate? ParseRaidDate(string arg) {
		DateOnly? date_raw = null;
		arg = arg.Trim().ToLower();

		DayOfWeek day_reset = DayOfWeek.Tuesday;
		Regex regex_closestWeekday = new (@"(?<position>this|last|next)\s+(?<day>friday|fri|f|saturday|sat|s)\.?");
		Regex regex_numberedWeekday = new (@"(?:(?<day1>friday|fri|f|saturday|sat|s)\.?\s+)?(?<i>\d+)\s+weeks?\s+(?<position>ago|from\s+now)(?:\s+on\s+(?<day2>friday|fri|f|saturday|sat|s)\.?)?");

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
			date_raw = date_raw.Value.AddDays(delta);
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
			date_raw = date_raw.Value.AddDays(delta);
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
			RaidObj.CalculateRaidDate((DateOnly)date_raw);
		return date;
	}
}
