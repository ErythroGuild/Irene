namespace Irene.Commands;

using Irene.Interactables;

using Module = Modules.Rank;

class Rank : CommandHandler {
	public const string
		Command_Rank       = "rank"       ,
		Command_ListTrials = "list-trials",
		Command_Set        = "set"        ,
		Command_SetErythro = "set-erythro",
		Arg_User = "user";
	private const int _trialListPageSize = 14;

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Officer)}{Mention(Command_Rank)} `{Command_ListTrials}` lists all users who are trials.
		{_t}Trials are members with both "<Erythro>" and "Guest" roles.
		{RankIcon(AccessLevel.Officer)}{Mention(Command_Rank)} `{Command_Set} <{Arg_User}>` sets the user's rank.
		{RankIcon(AccessLevel.Officer)}{Mention(Command_Rank)} `{Command_SetErythro} <{Arg_User}>` sets the user to a trial.
		{_t}This won't demote a user if they have a higher rank already.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Rank,
			"Manage user ranks."
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				AccessLevel.Officer,
				new (
					Command_ListTrials,
					"List all users who are trials.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption>()
				),
				new (ListTrialsAsync)
			),
			new (
				AccessLevel.Officer,
				new (
					Command_Set,
					"Set a user's rank.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> { new (
						Arg_User,
						"The user to manage.",
						ArgType.User,
						required: true
					) }
				),
				new (SetAsync)
			),
			new (
				AccessLevel.Officer,
				new (
					Command_SetErythro,
					"Set a user to a trial.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> { new (
						Arg_User,
						"The user to set.",
						ArgType.User,
						required: true
					) }
				),
				new (SetErythroAsync)
			),
		}
	);

	public async Task ListTrialsAsync(Interaction interaction, ParsedArgs args) {
		IReadOnlyList<DiscordMember> trials = Module.GetTrials();

		// Special case for empty list (no trials).
		if (trials.Count == 0) {
			string responseEmpty =
				":herb: All done! No trial members found for **<Erythro>**.";
			await interaction.RegisterAndRespondAsync(responseEmpty);
			return;
		}

		// Format member list into data list for StringPages.
		List<string> lines = new ();
		foreach (DiscordMember trial in trials) {
			TimeSpan time = DateTimeOffset.Now - trial.JoinedAt;
			string line = $"{trial.Mention} - {time.Days} days";
			lines.Add(line);
		}

		// Construct and respond with member list.
		MessagePromise messagePromise = new ();
		StringPages response = StringPages.Create(
			interaction,
			messagePromise,
			lines,
			new StringPagesOptions { PageSize = _trialListPageSize }
		);

		string summary = "Member list sent.";
		await interaction.RegisterAndRespondAsync(
			response.GetContentAsBuilder(),
			summary
		);

		DiscordMessage message = await interaction.GetResponseAsync();
		messagePromise.SetResult(message);
	}

	public async Task SetAsync(Interaction interaction, ParsedArgs args) {
	}

	public async Task SetErythroAsync(Interaction interaction, ParsedArgs args) {
		//List<string> response = new ();

		//// Fetch the first resolved user.
		//DiscordMember member =
		//	interaction.Interaction.GetTargetMember();
		//Log.Information("  Setting {User} as <Erythro> member.", member.DisplayName);

		//// Set rank role (Guest).
		//if (Command.GetAccessLevel(member) < AccessLevel.Guest) {
		//	await member.GrantRoleAsync(Program.Roles[id_r.guest]);
		//	Log.Debug("    User granted guest privileges.");
		//	interaction.Timer.LogMsecDebug("    Granted in {Time} msec.", false);
		//	response.Add("Guest role granted.");
		//} else {
		//	Log.Debug("    User already had guest privileges.");
		//}

		//// Set guild role (<Erythro>).
		//List<DiscordRole> roles = new (member.Roles);
		//if (!roles.Contains(Program.Roles[id_r.erythro])) {
		//	await member.GrantRoleAsync(Program.Roles[id_r.erythro]);
		//	Log.Debug("    User granted <Erythro> role.");
		//	interaction.Timer.LogMsecDebug("    Granted in {Time} msec.", false);
		//	response.Add("Guild role granted.");
		//} else {
		//	Log.Debug("    User already had <Erythro> role.");
		//}

		//// Add response for no changes needed.
		//if (response.Count == 0)
		//	response.Add("No changes necessary; user has required roles already.");

		//// Report.
		//await interaction.Interaction.UpdateMessageAsync(response.ToLines());
		//Log.Debug("  User {Username}#{Discriminator}  set as <Erythro> member.", member.Username, member.Discriminator);
		//interaction.Timer.LogMsecDebug("    Response completed in {Time} msec.");
	}



	//private record class SelectionTarget
	//	(Selection Selection, DiscordMember Member);

	//// Selection menus, indexed by the user who requested the menu.
	//// Although it may seem less "consistent" to have the possibility of
	//// multiple users attempting to modify the same user (and causing
	//// race conditions), it makes more intuitive sense when conflicts
	//// occur. (And conflicts would still be rare.)
	//private static readonly ConcurrentDictionary<ulong, SelectionTarget> _selectsRank = new ();


	//private static async Task SetRankAsync(DeferrerHandler handler) {
	//	// Ensure all needed params can be converted to DiscordMembers.
	//	DiscordMember? member_caller = await
	//		handler.Interaction.Interaction.User.ToMember();
	//	DiscordMember? member_target =
	//		handler.Interaction.Interaction.GetTargetMember();
	//	if (member_caller is null || member_target is null) {
	//		string response_error = "Could not fetch membership information.";
	//		response_error += (handler.Interaction.Interaction.ChannelId != id_ch.bots)
	//			? $"\nTry running the command again, in {Channels[id_ch.bots].Mention}?"
	//			: "\nIf this still doesn't work in a moment, message Ernie and he'll take care of it!";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response_error,
	//			"Could not convert user to DiscordMember.",
	//			LogLevel.Warning,
	//			"Could not fetch member data. Response sent.".AsLazy()
	//		);
	//		return;
	//	}

	//	// Check that the target isn't the caller.
	//	if (member_caller == member_target) {
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			"You cannot change your own rank.",
	//			"Attempted to set rank for self.",
	//			LogLevel.Information,
	//			"Could not set own rank. Response sent.".AsLazy()
	//		);
	//		return;
	//	}

	//	// Check that the target has a lower rank.
	//	Level level_caller = Command.GetAccessLevel(member_caller);
	//	Level level_target = Command.GetAccessLevel(member_target);
	//	if (level_caller < Level.Admin && level_target >= level_caller) {
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			"You cannot change the rank of someone higher than you.",
	//			"Attempted to set rank for higher-ranked target.",
	//			LogLevel.Information,
	//			"Could not set higher-ranked target rank. Response sent.".AsLazy()
	//		);
	//		return;
	//	}

	//	//// Construct select component by picking only usable options.
	//	//// Admin has special permission to set others to Admin.
	//	//List<KeyValuePair<Level, Entry>> options = new ();
	//	//foreach (KeyValuePair<Level, Entry> option in _optionsRankList) {
	//	//	if (level_caller > option.Key) {
	//	//		options.Add(option);
	//	//	 } else if (option.Key == Level.Admin && level_caller == Level.Admin) {
	//	//		options.Add(option);
	//	//	}
	//	//}
	//	//MessagePromise message_promise = new ();
	//	//Selection select = Selection.Create(
	//	//	handler.Interaction.Interaction,
	//	//	AssignRank,
	//	//	message_promise.Task,
	//	//	options,
	//	//	new HashSet<Level> { level_target },
	//	//	"No rank selected",
	//	//	isMultiple: false
	//	//);

	//	//// Disable any selections already in-flight.
	//	//if (_selectsRank.ContainsKey(member_caller.Id)) {
	//	//	await _selectsRank[member_caller.Id].Selection.Discard();
	//	//	_selectsRank.TryRemove(member_caller.Id, out _);
	//	//}
	//	//_selectsRank.TryAdd(
	//	//	member_caller.Id,
	//	//	new (select, member_target)
	//	//);

	//	//// Send response with selection menu.
	//	//string response_str =
	//	//	$"Previous rank: **{_optionsRank[level_target].Label}**";
	//	//DiscordWebhookBuilder response =
	//	//	new DiscordWebhookBuilder()
	//	//	.WithContent(response_str)
	//	//	.AddComponents(select.Component);
	//	//DiscordMessage message = await Command.SubmitResponseAsync(
	//	//	handler.Interaction,
	//	//	response,
	//	//	"Sending rank selection menu.",
	//	//	LogLevel.Debug,
	//	//	"Rank selection menu sent.".AsLazy()
	//	//);
	//	//message_promise.SetResult(message);
	//}
	//private static async Task AssignRank(ComponentInteractionCreateEventArgs e) {
	//	await AwaitGuildInitAsync();

	//	await e.Interaction.AcknowledgeComponentAsync();

	//	// Fetch target member from cache.
	//	ulong member_id = e.User.Id;
	//	DiscordMember member = _selectsRank[member_id].Member;

	//	// Find the target rank.
	//	// If promoting to Admin, also tag on Officer rank.
	//	Level rank_old = Command.GetAccessLevel(member);
	//	Level rank_new = rank_old;
	//	List<DiscordRole> roles_new = new ();
	//	string selected = e.Values[0];
	//	foreach (Level rank in _optionsRank.Keys) {
	//		if (selected == _optionsRank[rank].Id) {
	//			rank_new = rank;
	//			roles_new.Add(Program.Guild.GetRole(ToDiscordId(rank)));
	//			break;
	//		}
	//	}
	//	if (rank_new == Level.Admin)
	//		roles_new.Add(Program.Guild.Roles[id_r.officer]);

	//	// Fetch list of current roles.
	//	List<DiscordRole> roles_current = new (member.Roles);

	//	// Add non-rank roles to list of roles to be assigned.
	//	foreach (DiscordRole role in roles_current) {
	//		if (!IsRankRole(role.Id))
	//			roles_new.Add(role);
	//	}

	//	// Remove officer roles if the rank assigned is below Officer.
	//	if (rank_new < Level.Officer) {
	//		foreach (Officer officer in _optionsOfficer.Keys) {
	//			ulong id_officer = ToDiscordId(officer);
	//			bool match(DiscordRole i) =>
	//				i.Id == id_officer;
	//			if (roles_new.Exists(match))
	//				roles_new.RemoveAll(match);
	//		}
	//	}

	//	// Update member roles.
	//	Log.Debug("Updating member rank for {Member}.", member.DisplayName);
	//	await member.ReplaceRolesAsync(roles_new);
	//	Log.Information("  Updated rank successfully.");

	//	//// Update select component.
	//	//HashSet<Entry> options_updated = new ();
	//	//options_updated.Add(_optionsRank[rank_new]);
	//	//await _selectsRank[member_id].Selection.Update(options_updated);
	//	//Log.Debug("  Updated select component successfully.");

	//	// Send congrats message if promoted (from Guest or above).
	//	if (rank_new > rank_old && rank_new >= Level.Member) {
	//		Log.Information("  Sending promotion congrats message.");
	//		StringWriter text = new ();
	//		text.WriteLine("Congrats! :tada:");
	//		text.WriteLine($"You've been promoted to **{_optionsRank[rank_new].Label}**.");
	//		if (rank_new < Level.Officer)
	//			text.WriteLine("If your in-game ranks haven't been updated, just ask an Officer to update them.");
	//		await member.SendMessageAsync(text.ToString());
	//		Log.Information("  Promotion message sent.");
	//	}
	//}

	//private static async Task SetOfficerAsync(DeferrerHandler handler) {
	//	// Ensure all needed params can be converted to DiscordMembers.
	//	DiscordMember? member_caller = await
	//		handler.Interaction.Interaction.User.ToMember();
	//	DiscordMember? member_target =
	//		handler.Interaction.Interaction.GetTargetMember();
	//	if (member_caller is null || member_target is null) {
	//		string response_error = "Could not fetch membership information.";
	//		response_error += (handler.Interaction.Interaction.ChannelId != id_ch.bots)
	//			? $"\nTry running the command again, in {Channels[id_ch.bots].Mention}?"
	//			: "\nIf this still doesn't work in a moment, message Ernie and he will take care of it.";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response_error,
	//			"Could not convert user to DiscordMember.",
	//			LogLevel.Warning,
	//			"Could not fetch member data. Response sent.".AsLazy()
	//		);
	//		return;
	//	}

	//	// Fetch current roles of the user.
	//	List<Officer> roles = new ();
	//	foreach (DiscordRole role in member_target.Roles) {
	//		ulong role_id = role.Id;
	//		if (IsOfficerRole(role_id))
	//			roles.Add(ToOfficerRole(role_id));
	//	}
	//	roles.Sort();

	//	//// Construct select component.
	//	//MessagePromise message_promise = new ();
	//	//Selection select = Selection.Create(
	//	//	handler.Interaction.Interaction,
	//	//	AssignOfficer,
	//	//	message_promise.Task,
	//	//	_optionsOfficerList,
	//	//	new HashSet<Officer>(roles),
	//	//	"No roles selected",
	//	//	isMultiple: true
	//	//);

	//	//// Disable any selections already in-flight.
	//	//if (_selectsOfficer.ContainsKey(member_caller.Id)) {
	//	//	await _selectsOfficer[member_caller.Id].Selection.Discard();
	//	//	_selectsOfficer.TryRemove(member_caller.Id, out _);
	//	//}
	//	//_selectsOfficer.TryAdd(
	//	//	member_caller.Id,
	//	//	new (select, member_target)
	//	//);

	//	//// Send response with selection menu.
	//	//DiscordWebhookBuilder response =
	//	//	new DiscordWebhookBuilder()
	//	//	.AddComponents(select.Component);
	//	//DiscordMessage message = await Command.SubmitResponseAsync(
	//	//	handler.Interaction,
	//	//	response,
	//	//	"Sending officer role selection menu.",
	//	//	LogLevel.Debug,
	//	//	"Role selection menu sent.".AsLazy()
	//	//);
	//	//message_promise.SetResult(message);
	//}
	//private static async Task AssignOfficer(ComponentInteractionCreateEventArgs e) {
	//	await AwaitGuildInitAsync();

	//	await e.Interaction.AcknowledgeComponentAsync();

	//	// Fetch target member from cache.
	//	ulong member_id = e.User.Id;
	//	DiscordMember member = _selectsOfficer[member_id].Member;

	//	// Fetch list of desired roles.
	//	List<DiscordRole> roles_new = new ();
	//	List<string> selected = new (e.Values);
	//	foreach (Officer officer in _optionsOfficer.Keys) {
	//		if (selected.Contains(_optionsOfficer[officer].Id))
	//			roles_new.Add(Program.Guild.GetRole(ToDiscordId(officer)));
	//	}

	//	// Fetch list of current roles.
	//	List<DiscordRole> roles_current = new (member.Roles);

	//	// Add non-guild roles to list of roles to be assigned.
	//	foreach (DiscordRole role in roles_current) {
	//		if (!IsOfficerRole(role.Id))
	//			roles_new.Add(role);
	//	}

	//	// Update member roles.
	//	Log.Debug("Updating officer roles for {Member}.", member.DisplayName);
	//	await member.ReplaceRolesAsync(roles_new);
	//	Log.Information("  Updated roles successfully.");

	//	//// Update select component.
	//	//HashSet<Entry> options_updated = new ();
	//	//foreach (Officer key in _optionsGuild.Keys) {
	//	//	DiscordRole role = Program.Guild.GetRole(ToDiscordId(key));
	//	//	if (roles_new.Contains(role))
	//	//		options_updated.Add(_optionsOfficer[key]);
	//	//}
	//	//await _selectsOfficer[member_id].Selection.Update(options_updated);
	//	//Log.Debug("  Updated select component successfully.");
	//}
}
