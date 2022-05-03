using Irene.Components;

using Entry = Irene.Components.Selection.Option;

namespace Irene.Commands;

class Rank : ICommand {
	public enum Level {
		None,
		Guest, Member, Officer,
		Admin,
	};
	public enum Guild {
		Erythro,
		Glaive, Sanctum, Angels, Asgard,
	};
	public enum Officer {
		Raids,
		Events,
		Recruiter,
		Banker,
	};

	private record class SelectionTarget
		(Selection Selection, DiscordMember Member);

	// Selection menus, indexed by the user who requested the menu.
	// Although it may seem less "consistent" to have the possibility of
	// multiple users attempting to modify the same user (and causing
	// race conditions), it makes more intuitive sense when conflicts
	// occur. (And conflicts would still be rare.)
	private static readonly ConcurrentDictionary<ulong, SelectionTarget> _selectsRank = new ();
	private static readonly ConcurrentDictionary<ulong, SelectionTarget> _selectsGuild = new ();
	private static readonly ConcurrentDictionary<ulong, SelectionTarget> _selectsOfficer = new ();

	private static readonly List<KeyValuePair<Level, Entry>> _optionsRankList = new () {
		new (Level.Admin, new Entry {
			Label = "Admin",
			Id = "option_admin",
			Emoji = new ("\U0001F99A"), // :peacock:
			Description = "Guild Master.",
		}),
		new (Level.Officer, new Entry {
			Label = "Officer",
			Id = "option_officer",
			Emoji = new ("\U0001F426"), // :bird:
			Description = "Officer / moderator.",
		}),
		new (Level.Member, new Entry {
			Label = "Member",
			Id = "option_member",
			Emoji = new ("\U0001F98B"), // :butterfly:
			Description = "Trusted member (older member).",
		}),
		new (Level.Guest, new Entry {
			Label = "Guest",
			Id = "option_guest",
			Emoji = new ("\U0001F41B"), // :bug:
			Description = "Verified member (newer member).",
		}),
		new (Level.None, new Entry {
			Label = "No Rank",
			Id = "option_none",
			Emoji = new ("\U0001F401"), // :mouse2:
			Description = "No rank assigned.",
		}),
	};
	private static readonly ConcurrentDictionary<Level, Entry> _optionsRank;
	private static readonly List<KeyValuePair<Guild, Entry>> _optionsGuildList = new () {
		new (Guild.Erythro, new Entry {
			Label = "<Erythro>",
			Id = "option_erythro",
			Emoji = new (id_e.erythro),
		}),
		new (Guild.Glaive, new Entry {
			Label = "<Glaive of Mother Moon>",
			Id = "option_glaive",
			Emoji = new ("\U0001F90D"), // :white_heart:
		}),
		new (Guild.Sanctum, new Entry {
			Label = "<Sanctum of Secrets>",
			Id = "option_sanctum",
			Emoji = new ("\u2764"),     // :heart:
		}),
		new (Guild.Angels, new Entry {
			Label = "<Hooved Angels>",
			Id = "option_angels",
			Emoji = new ("\U0001F49B"), // :yellow_heart:
		}),
		new (Guild.Asgard, new Entry {
			Label = "<Asgard>",
			Id = "option_asgard",
			Emoji = new ("\U0001F499"), // :blue_heart:
		}),
	};
	private static readonly ConcurrentDictionary<Guild, Entry> _optionsGuild;
	private static readonly List<KeyValuePair<Officer, Entry>> _optionsOfficerList = new () {
		new (Officer.Raids, new Entry {
			Label = "Raid Officer",
			Id = "option_raids",
			Emoji = new ("\u2694"),     // :crossed_swords:
		}),
		new (Officer.Events, new Entry {
			Label = "Event Planner",
			Id = "option_events",
			Emoji = new ("\U0001F3B3"), // :bowling:
		}),
		new (Officer.Recruiter, new Entry {
			Label = "Recruiter",
			Id = "option_recruiter",
			Emoji = new ("\U0001F5C3"), // :card_box:
		}),
		new (Officer.Banker, new Entry {
			Label = "Banker",
			Id = "option_banker",
			Emoji = new("\U0001F4B0"), // :moneybag:
		}),
	};
	private static readonly ConcurrentDictionary<Officer, Entry> _optionsOfficer;

	// Conversions / definitions.
	private static readonly ConcurrentDictionary<Level, ulong> _table_RankToId = new () {
		[Level.Admin  ] = id_r.admin  ,
		[Level.Officer] = id_r.officer,
		[Level.Member ] = id_r.member ,
		[Level.Guest  ] = id_r.guest  ,
	};
	private static readonly ConcurrentDictionary<ulong, Level> _table_IdToRank;
	private static readonly ConcurrentDictionary<Guild, ulong> _table_GuildToId = new () {
		[Guild.Erythro] = id_r.erythro,
		[Guild.Glaive ] = id_r.glaive ,
		[Guild.Sanctum] = id_r.sanctum,
		[Guild.Angels ] = id_r.angels ,
		[Guild.Asgard ] = id_r.asgard ,
	};
	private static readonly ConcurrentDictionary<ulong, Guild> _table_IdToGuild;
	private static readonly ConcurrentDictionary<Officer, ulong> _table_OfficerToId = new () {
		[Officer.Raids    ] = id_r.raidOfficer ,
		[Officer.Events   ] = id_r.eventPlanner,
		[Officer.Recruiter] = id_r.recruiter   ,
		[Officer.Banker   ] = id_r.banker      ,
	};
	private static readonly ConcurrentDictionary<ulong, Officer> _table_IdToOfficer;

	private const string
		_commandSetRank = "set",
		_commandSetGuild = "set-guild",
		_commandSetOfficer = "set-officer",
		_commandListTrials = "list-trials",
		_commandSetErythro = "Set <Erythro>";

	// Force static initializer to run.
	public static void Init() { return; }
	static Rank() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		_optionsRank = new ();
		foreach (KeyValuePair<Level, Entry> option in _optionsRankList)
			_optionsRank.TryAdd(option.Key, option.Value);
		_optionsGuild = new ();
		foreach (KeyValuePair<Guild, Entry> option in _optionsGuildList)
			_optionsGuild.TryAdd(option.Key, option.Value);
		_optionsOfficer = new ();
		foreach (KeyValuePair<Officer, Entry> option in _optionsOfficerList)
			_optionsOfficer.TryAdd(option.Key, option.Value);

		_table_IdToRank = Util.Invert(_table_RankToId);
		_table_IdToGuild = Util.Invert(_table_GuildToId);
		_table_IdToOfficer = Util.Invert(_table_OfficerToId);

		Log.Information("  Initialized command: /rank");
		Log.Debug("    Selects and conversion caches initialized.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@":lock: `/rank set <member>` shows a menu for promoting/demoting the specified member.",
			@"`/rank set-guild <member>` shows a menu for assigning the guild(s) a member is associated with.",
			@":lock: `/rank set-officer <member>` shows a menu for assigning specific officer roles to an officer.",
			@":lock: `/rank list-trials` lists all members who are currently trials.",
			"Apart from setting your own guild associations, all commands require Officer rank.",
		} ) };
	}

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"rank",
				"Assign rank-based roles.",
				options: new List<CommandOption> {
					new (
						_commandSetRank,
						"Set the rank of the specified member.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"member",
							"The member to promote/demote.",
							ApplicationCommandOptionType.User,
							required: true
						) }
					),
					new (
						_commandSetGuild,
						"Set the guild roles associated with a member.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"member",
							"The member to set guilds for.",
							ApplicationCommandOptionType.User,
							required: false
						) }
					),
					new (
						_commandSetOfficer,
						"Set the officer positions for an officer.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"member",
							"The member to set officer positions for.",
							ApplicationCommandOptionType.User,
							required: true
						) }
					),
					new (
						_commandListTrials,
						"Display a list of trials (@Guest + @<Erythro>).",
						ApplicationCommandOptionType.SubCommand
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), DeferAsync, RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands { get =>
		new () {
			new ( new (
				_commandSetErythro,
				"",	// description field must be "" instead of null
				defaultPermission: true,
				type: ApplicationCommandType.UserContextMenu
			), DeferAsync, SetErythroAsync)
		};
	}

	public static List<InteractionCommand> MessageCommands { get => new (); }
	public static List<AutoCompleteHandler> AutoComplete   { get => new (); }

	public static async Task DeferAsync(TimedInteraction interaction) {
		DeferrerHandler handler = new (interaction, true);
		DeferrerHandlerFunc? function =
			await GetDeferrerHandler(handler);
		if (function is not null)
			await function(handler);
	}
	public static async Task RunAsync(TimedInteraction interaction) {
		DeferrerHandler handler = new (interaction, false);
		DeferrerHandlerFunc? function =
			await GetDeferrerHandler(handler);
		if (function is not null)
			await function(handler);
	}
	private static async Task<DeferrerHandlerFunc?> GetDeferrerHandler(DeferrerHandler handler) {
		List<DiscordInteractionDataOption> args =
			handler.GetArgs();
		string command = args[0].Name;

		// Check for permissions.
		bool doContinue;
		switch (command) {
		case _commandSetRank:
		case _commandListTrials:
			doContinue = await
				handler.CheckAccessAsync(AccessLevel.Officer);
			if (!doContinue)
				return null;
			break;
		case _commandSetOfficer:
			doContinue = await
				handler.CheckAccessAsync(AccessLevel.Admin);
			if (!doContinue)
				return null;
			break;
		}

		// Dispatch the correct subcommand.
		return command switch {
			_commandSetRank    => SetRankAsync,
			_commandSetGuild   => SetGuildAsync,
			_commandSetOfficer => SetOfficerAsync,
			_commandListTrials => ListTrialsAsync,
			_ => throw new ArgumentException("Unrecognized subcommand.", nameof(handler)),
		};
	}

	private static async Task SetErythroAsync(TimedInteraction interaction) {
		// Check for permissions.
		bool doContinue = await
			interaction.CheckAccessAsync(false, AccessLevel.Officer);
		if (!doContinue)
			return;

		await interaction.Interaction.DeferMessageAsync(true);
		List<string> response_lines = new ();

		// Fetch the first resolved user.
		DiscordMember member =
			interaction.Interaction.GetTargetMember();
		Log.Debug("  Setting {User} as <Erythro> member.", member.DisplayName);

		// Set rank role (Guest).
		if (Command.GetAccessLevel(member) < AccessLevel.Guest) {
			await member.GrantRoleAsync(Program.Roles[id_r.guest]);
			Log.Debug("    User granted guest privileges.");
			interaction.Timer.LogMsecDebug("    Granted in {Time} msec.", false);
			response_lines.Add("Guest role granted.");
		} else {
			Log.Debug("    User already had guest privileges.");
		}

		// Set guild role (<Erythro>).
		List<DiscordRole> roles = new (member.Roles);
		if (!roles.Contains(Program.Roles[id_r.erythro])) {
			await member.GrantRoleAsync(Program.Roles[id_r.erythro]);
			Log.Debug("    User granted <Erythro> role.");
			interaction.Timer.LogMsecDebug("    Granted in {Time} msec.", false);
			response_lines.Add("Guild role granted.");
		} else {
			Log.Debug("    User already had <Erythro> role.");
		}

		// Add response for no changes needed.
		if (response_lines.Count == 0)
			response_lines.Add("No changes necessary; user has required roles already.");

		// Report.
		string response = string.Join("\n", response_lines);
		await interaction.Interaction.UpdateMessageAsync(response);
		Log.Debug("  User {Username}#{Discriminator}  set as <Erythro> member.", member.Username, member.Discriminator);
		interaction.Timer.LogMsecDebug("    Response completed in {Time} msec.");
	}

	private static async Task SetRankAsync(DeferrerHandler handler) {
		// Always ephemeral.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, true);
			return;
		}

		// Ensure all needed params can be converted to DiscordMembers.
		DiscordMember? member_caller = await
			handler.Interaction.Interaction.User.ToMember();
		DiscordMember? member_target =
			GetResolvedMember(handler.Interaction.Interaction);
		if (member_caller is null || member_target is null) {
			string response_error = "Could not fetch membership information.";
			response_error += (Program.Guild is not null && handler.Interaction.Interaction.ChannelId != id_ch.bots)
				? $"\nTry running the command again, in {Channels[id_ch.bots].Mention}?"
				: "\nIf this still doesn't work in a moment, message Ernie and he will take care of it.";
			await Command.SubmitResponseAsync(
				handler.Interaction,
				response_error,
				"Could not convert user to DiscordMember.",
				LogLevel.Warning,
				"Could not fetch member data. Response sent.".AsLazy()
			);
			return;
		}

		// Check that the target isn't the caller.
		if (member_caller == member_target) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"You cannot change your own rank.",
				"Attempted to set rank for self.",
				LogLevel.Information,
				"Could not set own rank. Response sent.".AsLazy()
			);
			return;
		}

		Level level_caller = Command.GetAccessLevel(member_caller);
		Level level_target = Command.GetAccessLevel(member_target);
		string response_str =
			$"Previous rank: **{_optionsRank[level_target].Label}**";

		// Construct select component by picking only usable options.
		// Admin has special permission to set others to Admin.
		List<KeyValuePair<Level, Entry>> options = new ();
		foreach (KeyValuePair<Level, Entry> option in _optionsRankList) {
			if (level_caller > option.Key) {
				options.Add(option);
			 } else if (option.Key == Level.Admin && level_caller == Level.Admin) {
				options.Add(option);
			}
		}
		MessagePromise message_promise = new ();
		Selection select = Selection.Create(
			handler.Interaction.Interaction,
			AssignRank,
			message_promise.Task,
			options,
			new HashSet<Level> { level_target },
			"No rank selected",
			isMultiple: false
		);

		// Disable any selections already in-flight.
		if (_selectsRank.ContainsKey(member_caller.Id)) {
			await _selectsRank[member_caller.Id].Selection.Discard();
			_selectsRank.TryRemove(member_caller.Id, out _);
		}
		_selectsRank.TryAdd(
			member_caller.Id,
			new (select, member_target)
		);

		// Send response with selection menu.
		DiscordWebhookBuilder response =
			new DiscordWebhookBuilder()
			.WithContent(response_str)
			.AddComponents(select.Component);
		DiscordMessage message = await Command.SubmitResponseAsync(
			handler.Interaction,
			response,
			"Sending rank selection menu.",
			LogLevel.Debug,
			"Rank selection menu sent.".AsLazy()
		);
		message_promise.SetResult(message);
	}
	private static async Task AssignRank(ComponentInteractionCreateEventArgs e) {
		await e.Interaction.AcknowledgeComponentAsync();

		// Make sure Guild is initialized.
		if (Program.Guild is null) {
			Log.Information("Updating user rank.");
			Log.Warning("  Rank not updated (guild not initialized).");
			return;
		}

		// Fetch target member from cache.
		ulong member_id = e.User.Id;
		DiscordMember member = _selectsRank[member_id].Member;

		// Find the target rank.
		// If promoting to Admin, also tag on Officer rank.
		Level rank_old = Command.GetAccessLevel(member);
		Level rank_new = rank_old;
		List<DiscordRole> roles_new = new ();
		string selected = e.Values[0];
		foreach (Level rank in _optionsRank.Keys) {
			if (selected == _optionsRank[rank].Id) {
				rank_new = rank;
				roles_new.Add(Program.Guild.GetRole(ToDiscordId(rank)));
				break;
			}
		}
		if (rank_new == Level.Admin)
			roles_new.Add(Program.Guild.Roles[id_r.officer]);

		// Fetch list of current roles.
		List<DiscordRole> roles_current = new (member.Roles);

		// Add non-rank roles to list of roles to be assigned.
		foreach (DiscordRole role in roles_current) {
			if (!IsRankRole(role.Id))
				roles_new.Add(role);
		}

		// Remove officer roles if the rank assigned is below Officer.
		if (rank_new < Level.Officer) {
			foreach (Officer officer in _optionsOfficer.Keys) {
				ulong id_officer = ToDiscordId(officer);
				bool match(DiscordRole i) =>
					i.Id == id_officer;
				if (roles_new.Exists(match))
					roles_new.RemoveAll(match);
			}
		}

		// Update member roles.
		Log.Debug("Updating member rank for {Member}.", member.DisplayName);
		await member.ReplaceRolesAsync(roles_new);
		Log.Information("  Updated rank successfully.");

		// Update select component.
		HashSet<Entry> options_updated = new ();
		options_updated.Add(_optionsRank[rank_new]);
		await _selectsRank[member_id].Selection.Update(options_updated);
		Log.Debug("  Updated select component successfully.");

		// Send congrats message if promoted (from Guest or above).
		if (rank_new > rank_old && rank_new >= Level.Member) {
			Log.Information("  Sending promotion congrats message.");
			StringWriter text = new ();
			text.WriteLine("Congrats! :tada:");
			text.WriteLine($"You've been promoted to **{_optionsRank[rank_new].Label}**.");
			if (rank_new < Level.Officer)
				text.WriteLine("If your in-game ranks haven't been updated, just ask an Officer to update them.");
			await member.SendMessageAsync(text.ToString());
			Log.Information("  Promotion message sent.");
		}
	}

	private static async Task SetGuildAsync(DeferrerHandler handler) {
		// Always ephemeral.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, true);
			return;
		}

		// Determine the caller and target of the command.
		// If a target member (not matching the caller) is specified, check
		// for permissions.
		DiscordMember? member_caller = await
			handler.Interaction.Interaction.User.ToMember();
		DiscordMember? member_target = null;
		List<DiscordInteractionDataOption> args =
			new (handler.GetArgs()[0].Options);
		if (args.Count > 0) {
			member_target =
				GetResolvedMember(handler.Interaction.Interaction);
			if (member_caller is not null &&
				member_target is not null &&
				member_target.Id != member_caller.Id
			) {
				// Check for permissions.
				bool doContinue = await
					handler.CheckAccessAsync(AccessLevel.Officer);
				if (!doContinue)
					return;
			}
		} else {
			member_target = member_caller;
		}

		// Ensure all needed params can be converted to DiscordMembers.
		if (member_caller is null || member_target is null) {
			string response_error = "Could not fetch membership information.";
			response_error += (Program.Guild is not null && handler.Interaction.Interaction.ChannelId != id_ch.bots)
				? $"\nTry running the command again, in {Channels[id_ch.bots].Mention}?"
				: "\nIf this still doesn't work in a moment, message Ernie and he will take care of it.";
			await Command.SubmitResponseAsync(
				handler.Interaction,
				response_error,
				"Could not convert user to DiscordMember.",
				LogLevel.Warning,
				"Could not fetch member data. Response sent.".AsLazy()
			);
			return;
		}

		// Fetch current roles of the user.
		List<Guild> guilds = new ();
		foreach (DiscordRole role in member_target.Roles) {
			ulong role_id = role.Id;
			if (IsGuildRole(role_id))
				guilds.Add(ToGuildRole(role_id));
		}
		guilds.Sort();
		string response_str =
			Selection.PrintSelected(guilds, _optionsGuild, "guild", "guilds");

		// Construct select component.
		MessagePromise message_promise = new ();
		Selection select = Selection.Create(
			handler.Interaction.Interaction,
			AssignGuild,
			message_promise.Task,
			_optionsGuildList,
			new HashSet<Guild>(guilds),
			"No guilds selected",
			isMultiple: true
		);

		// Disable any selections already in-flight.
		if (_selectsGuild.ContainsKey(member_caller.Id)) {
			await _selectsGuild[member_caller.Id].Selection.Discard();
			_selectsGuild.TryRemove(member_caller.Id, out _);
		}
		_selectsGuild.TryAdd(
			member_caller.Id,
			new (select, member_target)
		);

		// Send response with selection menu.
		DiscordWebhookBuilder response =
			new DiscordWebhookBuilder()
			.WithContent(response_str)
			.AddComponents(select.Component);
		DiscordMessage message = await Command.SubmitResponseAsync(
			handler.Interaction,
			response,
			"Sending guild selection menu.",
			LogLevel.Debug,
			"Guild selection menu sent.".AsLazy()
		);
		message_promise.SetResult(message);

	}
	private static async Task AssignGuild(ComponentInteractionCreateEventArgs e) {
		await e.Interaction.AcknowledgeComponentAsync();

		// Make sure Guild is initialized.
		if (Program.Guild is null) {
			Log.Information("Updating user guilds.");
			Log.Warning("  Guilds not updated (guild not initialized).");
			return;
		}

		// Fetch target member from cache.
		ulong member_id = e.User.Id;
		DiscordMember member = _selectsGuild[member_id].Member;

		// Fetch list of desired roles.
		List<DiscordRole> roles_new = new ();
		List<string> selected = new (e.Values);
		foreach (Guild guild in _optionsGuild.Keys) {
			if (selected.Contains(_optionsGuild[guild].Id))
				roles_new.Add(Program.Guild.GetRole(ToDiscordId(guild)));
		}

		// Fetch list of current roles.
		List<DiscordRole> roles_current = new (member.Roles);

		// Add non-guild roles to list of roles to be assigned.
		foreach (DiscordRole role in roles_current) {
			if (!IsGuildRole(role.Id))
				roles_new.Add(role);
		}

		// Update member roles.
		Log.Debug("Updating member roles for {Member}.", member.DisplayName);
		await member.ReplaceRolesAsync(roles_new);
		Log.Information("  Updated roles successfully.");

		// Update select component.
		HashSet<Entry> options_updated = new ();
		foreach (Guild key in _optionsGuild.Keys) {
			DiscordRole role = Program.Guild.GetRole(ToDiscordId(key));
			if (roles_new.Contains(role))
				options_updated.Add(_optionsGuild[key]);
		}
		await _selectsGuild[member_id].Selection.Update(options_updated);
		Log.Debug("  Updated select component successfully.");
	}

	private static async Task SetOfficerAsync(DeferrerHandler handler) {
		// Always ephemeral.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, true);
			return;
		}

		// Ensure all needed params can be converted to DiscordMembers.
		DiscordMember? member_caller = await
			handler.Interaction.Interaction.User.ToMember();
		DiscordMember? member_target =
			GetResolvedMember(handler.Interaction.Interaction);
		if (member_caller is null || member_target is null) {
			string response_error = "Could not fetch membership information.";
			response_error += (Program.Guild is not null && handler.Interaction.Interaction.ChannelId != id_ch.bots)
				? $"\nTry running the command again, in {Channels[id_ch.bots].Mention}?"
				: "\nIf this still doesn't work in a moment, message Ernie and he will take care of it.";
			await Command.SubmitResponseAsync(
				handler.Interaction,
				response_error,
				"Could not convert user to DiscordMember.",
				LogLevel.Warning,
				"Could not fetch member data. Response sent.".AsLazy()
			);
			return;
		}

		// Fetch current roles of the user.
		List<Officer> roles = new ();
		foreach (DiscordRole role in member_target.Roles) {
			ulong role_id = role.Id;
			if (IsOfficerRole(role_id))
				roles.Add(ToOfficerRole(role_id));
		}
		roles.Sort();
		string response_str =
			Selection.PrintSelected(roles, _optionsOfficer, "officer role", "officer roles");

		// Construct select component.
		MessagePromise message_promise = new ();
		Selection select = Selection.Create(
			handler.Interaction.Interaction,
			AssignOfficer,
			message_promise.Task,
			_optionsOfficerList,
			new HashSet<Officer>(roles),
			"No roles selected",
			isMultiple: true
		);

		// Disable any selections already in-flight.
		if (_selectsOfficer.ContainsKey(member_caller.Id)) {
			await _selectsOfficer[member_caller.Id].Selection.Discard();
			_selectsOfficer.TryRemove(member_caller.Id, out _);
		}
		_selectsOfficer.TryAdd(
			member_caller.Id,
			new (select, member_target)
		);

		// Send response with selection menu.
		DiscordWebhookBuilder response =
			new DiscordWebhookBuilder()
			.WithContent(response_str)
			.AddComponents(select.Component);
		DiscordMessage message = await Command.SubmitResponseAsync(
			handler.Interaction,
			response,
			"Sending officer role selection menu.",
			LogLevel.Debug,
			"Role selection menu sent.".AsLazy()
		);
		message_promise.SetResult(message);
	}
	private static async Task AssignOfficer(ComponentInteractionCreateEventArgs e) {
		await e.Interaction.AcknowledgeComponentAsync();

		// Make sure Guild is initialized.
		if (Program.Guild is null) {
			Log.Debug("Updating officer roles.");
			Log.Warning("  Roles not updated (guild not initialized).");
			return;
		}

		// Fetch target member from cache.
		ulong member_id = e.User.Id;
		DiscordMember member = _selectsOfficer[member_id].Member;

		// Fetch list of desired roles.
		List<DiscordRole> roles_new = new ();
		List<string> selected = new (e.Values);
		foreach (Officer officer in _optionsOfficer.Keys) {
			if (selected.Contains(_optionsOfficer[officer].Id))
				roles_new.Add(Program.Guild.GetRole(ToDiscordId(officer)));
		}

		// Fetch list of current roles.
		List<DiscordRole> roles_current = new (member.Roles);

		// Add non-guild roles to list of roles to be assigned.
		foreach (DiscordRole role in roles_current) {
			if (!IsOfficerRole(role.Id))
				roles_new.Add(role);
		}

		// Update member roles.
		Log.Debug("Updating officer roles for {Member}.", member.DisplayName);
		await member.ReplaceRolesAsync(roles_new);
		Log.Information("  Updated roles successfully.");

		// Update select component.
		HashSet<Entry> options_updated = new ();
		foreach (Officer key in _optionsGuild.Keys) {
			DiscordRole role = Program.Guild.GetRole(ToDiscordId(key));
			if (roles_new.Contains(role))
				options_updated.Add(_optionsOfficer[key]);
		}
		await _selectsOfficer[member_id].Selection.Update(options_updated);
		Log.Debug("  Updated select component successfully.");
	}

	private static async Task ListTrialsAsync(DeferrerHandler handler) {
		if (Program.Guild is null) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Server data not downloaded yet. Try again in a few seconds.",
				"Guild not initialized yet.",
				LogLevel.Warning,
				"No data fetched (guild not initialized). Response sent.".AsLazy()
			);
			return;
		}

		// Deferrer is non-ephemeral for the rest.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, false);
			return;
		}

		// Fetch needed data.
		List<DiscordMember> members =
			new (await Program.Guild.GetAllMembersAsync());
		DiscordRole role_guest = Program.Roles[id_r.guest];
		DiscordRole role_guild = Program.Roles[id_r.erythro];

		// Construct list of members who are "trials".
		// (Guest + <Erythro>)
		List<DiscordMember> trials = new ();
		foreach (DiscordMember member in members) {
			List<DiscordRole> roles_i = new (member.Roles);
			if (roles_i.Contains(role_guest) && roles_i.Contains(role_guild))
				trials.Add(member);
		}

		// Handle case where no trial members exist.
		if (trials.Count == 0) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"All done! No trial members found for **<Erythro>**.",
				"No trial members found.",
				LogLevel.Debug,
				"Response sent.".AsLazy()
			);
			return;
		}

		// Sort list by days elapsed.
		trials.Sort(delegate (DiscordMember x, DiscordMember y) {
			DateTimeOffset now = DateTimeOffset.Now;
			TimeSpan x_time = now - x.JoinedAt;
			TimeSpan y_time = now - y.JoinedAt;
			return - x_time.CompareTo(y_time);
			// negative -> sorts from longest to shortest
		});

		// Display list of trial members.
		StringWriter response = new ();
		foreach (DiscordMember trial in trials) {
			TimeSpan time = DateTimeOffset.Now - trial.JoinedAt;
			response.WriteLine($"{trial.Mention} - {time.Days} days old");
		}
		await Command.SubmitResponseAsync(
			handler.Interaction,
			response.ToString(),
			"Sending list of trial members (@Guest + @<Erythro>).",
			LogLevel.Debug,
			"List sent. ({EntryCount} entries)".AsLazy(),
			trials.Count
		);
	}

	private static DiscordMember? GetResolvedMember(DiscordInteraction interaction) {
		List<DiscordMember> members =
			new (interaction.Data.Resolved.Members.Values);
		return members.Count > 0
			? members[0]
			: null;
	}

	private static bool IsRankRole(ulong id) =>
		_table_IdToRank.ContainsKey(id);
	private static bool IsGuildRole(ulong id) =>
		_table_IdToGuild.ContainsKey(id);
	private static bool IsOfficerRole(ulong id) =>
		_table_IdToOfficer.ContainsKey(id);

	private static Level ToRankRole(ulong id) =>
		_table_IdToRank[id];
	private static ulong ToDiscordId(Level rank) =>
		_table_RankToId[rank];
	private static Guild ToGuildRole(ulong id) =>
		_table_IdToGuild[id];
	private static ulong ToDiscordId(Guild guild) =>
		_table_GuildToId[guild];
	private static Officer ToOfficerRole(ulong id) =>
		_table_IdToOfficer[id];
	private static ulong ToDiscordId(Officer role) =>
		_table_OfficerToId[role];
}
