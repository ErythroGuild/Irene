using Irene.Interactables;

using Option = Irene.Interactables.Selection.Option;

namespace Irene.Modules;

class Roles {
	public enum PingRole {
		Raid, Mythics, KSM, Gearing,
		Events, Herald,
	}
	public enum GuildRole {
		Erythro,
		Glaive, Sanctum, Angels, Asgard,
	}

	// Selection menus, indexed by the ID of the member accessing them.
	// NOTE: New selection menus should be registered here, and every
	// member should only have one pair active at once.
	private static readonly ConcurrentDictionary<ulong, (Selection Ping, Selection Guild)> _menus = new ();
	
	// Role object conversion tables.
	private static readonly ConstBiMap<PingRole, ulong> _pingRoles = new (
		new Dictionary<PingRole, ulong> {
			[PingRole.Raid   ] = id_r.raid   ,
			[PingRole.Mythics] = id_r.mythics,
			[PingRole.KSM    ] = id_r.ksm    ,
			[PingRole.Gearing] = id_r.gearing,
			[PingRole.Events ] = id_r.events ,
			[PingRole.Herald ] = id_r.herald ,
		}
	);
	private static readonly ConstBiMap<GuildRole, ulong> _guildRoles = new (
		new Dictionary<GuildRole, ulong> {
			[GuildRole.Erythro] = id_r.erythro,
			[GuildRole.Glaive ] = id_r.glaive ,
			[GuildRole.Sanctum] = id_r.sanctum,
			[GuildRole.Angels ] = id_r.angels ,
			[GuildRole.Asgard ] = id_r.asgard ,
		}
	);
	private static readonly ConstBiMap<PingRole, string> _pingOptions = new (
		new Dictionary<PingRole, string> {
			[PingRole.Raid   ] = _optionPingRaid   ,
			[PingRole.Mythics] = _optionPingMythics,
			[PingRole.KSM    ] = _optionPingKSM    ,
			[PingRole.Gearing] = _optionPingGearing,
			[PingRole.Events ] = _optionPingEvents ,
			[PingRole.Herald ] = _optionPingHerald ,
		}
	);
	private static readonly ConstBiMap<GuildRole, string> _guildOptions = new (
		new Dictionary<GuildRole, string> {
			[GuildRole.Erythro] = _optionGuildErythro,
			[GuildRole.Glaive ] = _optionGuildGlaive ,
			[GuildRole.Sanctum] = _optionGuildSanctum,
			[GuildRole.Angels ] = _optionGuildAngels ,
			[GuildRole.Asgard ] = _optionGuildAsgard ,
		}
	);
	// Select menu definitions.
	private static IReadOnlyList<(PingRole, Option)> PingRoleOptions =>
		new List<(PingRole, Option)> {
			new (PingRole.Raid, new Option {
				Label = "Raid",
				Id = _optionPingRaid,
				Emoji = new ("\U0001F409"), // :dragon:
				Description = "Raid announcements.",
			}),
			new (PingRole.Mythics, new Option {
				Label = "M+",
				Id = _optionPingMythics,
				Emoji = new ("\U0001F5FA"), // :map:
				Description = "M+ keys in general.",
			}),
			new (PingRole.KSM, new Option {
				Label = "KSM",
				Id = _optionPingKSM,
				Emoji = new ("\U0001F94B"), // :martial_arts_uniform:
				Description = "Higher keys requiring more focus.",
			}),
			new (PingRole.Gearing, new Option {
				Label = "Gearing",
				Id = _optionPingGearing,
				Emoji = new ("\U0001F392"), // :school_satchel:
				Description = "Lower keys / M0s to help gear people.",
			}),
			new (PingRole.Events, new Option {
				Label = "Events",
				Id = _optionPingEvents,
				Emoji = new ("\U0001F938\u200D\u2640\uFE0F"), // :woman_cartwheeling:
				Description = "Social event announcements.",
			}),
			new (PingRole.Herald, new Option {
				Label = "Herald",
				Id = _optionPingHerald,
				Emoji = new ("\u2604"), // :comet:
				Description = "Herald of the Titans announcements.",
			}),
		};
	private static IReadOnlyList<(GuildRole, Option)> GuildRoleOptions =>
		new List<(GuildRole, Option)> {
			new (GuildRole.Erythro, new Option {
				Label = "<Erythro>",
				Id = _optionGuildErythro,
				Emoji = new (id_e.erythro),
			}),
			new (GuildRole.Glaive, new Option {
				Label = "<Glaive of Mother Moon>",
				Id = _optionGuildGlaive,
				Emoji = new ("\U0001F90D"), // :white_heart:
			}),
			new (GuildRole.Sanctum, new Option {
				Label = "<Sanctum of Secrets>",
				Id = _optionGuildSanctum,
				Emoji = new ("\u2764"),     // :heart:
			}),
			new (GuildRole.Angels, new Option {
				Label = "<Hooved Angels>",
				Id = _optionGuildAngels,
				Emoji = new ("\U0001F49B"), // :yellow_heart:
			}),
			new (GuildRole.Asgard, new Option {
				Label = "<Asgard>",
				Id = _optionGuildAsgard,
				Emoji = new ("\U0001F499"), // :blue_heart:
			}),
		};

	private const string
		_idSelectPingRoles  = "select_pingroles" ,
		_idSelectGuildRoles = "select_guildroles";
	private const string
		_optionPingRaid     = "option_raid"   ,
		_optionPingMythics  = "option_mythics",
		_optionPingKSM      = "option_ksm"    ,
		_optionPingGearing  = "option_gearing",
		_optionPingEvents   = "option_events" ,
		_optionPingHerald   = "option_herald" ,
		_optionGuildErythro = "option_erythro",
		_optionGuildGlaive  = "option_glaive" ,
		_optionGuildSanctum = "option_sanctum",
		_optionGuildAngels  = "option_angels" ,
		_optionGuildAsgard  = "option_asgard" ;

	public static async Task RespondAsync(
		Interaction interaction,
		DiscordMember user
	) {
		// Initialize current roles of user.
		HashSet<PingRole> pingRoles = new ();
		HashSet<GuildRole> guildRoles = new ();
		foreach (DiscordRole role in user.Roles) {
			ulong id = role.Id;
			if (_pingRoles.Contains(id))
				pingRoles.Add(_pingRoles[id]);
			if (_guildRoles.Contains(id))
				guildRoles.Add(_guildRoles[id]);
		}

		// Create Selection interactables.
		MessagePromise messagePromise = new ();
		Selection selectPings = Selection.Create(
			interaction,
			messagePromise.Task,
			AssignPingsAsync,
			_idSelectPingRoles,
			PingRoleOptions,
			pingRoles,
			isMultiple: true,
			placeholder: "Select roles to be pinged for."
		);
		Selection selectGuilds = Selection.Create(
			interaction,
			messagePromise.Task,
			AssignGuildsAsync,
			_idSelectGuildRoles,
			GuildRoleOptions,
			guildRoles,
			isMultiple: true,
			placeholder: "Select the guilds you associate with."
		);

		// Update global Selection tracking table, and disable any menus
		// already in-flight.
		if (_menus.ContainsKey(user.Id)) {
			(Selection menu1, Selection menu2) =
				_menus[user.Id];
			// Discarding is just an extra safety, so no need to await.
			_ = menu1.Discard();
			_ = menu2.Discard();
			_menus.TryRemove(user.Id, out _);
		}
		_menus.TryAdd(user.Id, (selectPings, selectGuilds));

		// Respond to interaction.
		// Note: A response must have either content or an embed to be
		// considered valid.
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithContent(" ")
			.AddComponents(selectPings.Component)
			.AddComponents(selectGuilds.Component);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response,true);
		interaction.SetResponseSummary("Role selection menu sent.");

		// Update message promise.
		DiscordMessage message = await interaction.GetResponseAsync();
		messagePromise.SetResult(message);
	}

	private static async Task AssignPingsAsync(ComponentInteractionCreateEventArgs e) {
		if (Erythro is null)
			throw new InvalidOperationException("Guild not initialized yet.");

		// Fetch member object data.
		Interaction interaction = Interaction.FromComponent(e);
		DiscordMember? user = await interaction.User.ToMember();
		if (user is null) {
			await RespondNoDataAsync(interaction);
			return;
		}

		// Fetch list of desired roles.
		// Added PingRoles are saved separately (to update Selection).
		HashSet<DiscordRole> rolesAdded = new ();
		foreach (string option in e.Values) {
			DiscordRole role =
				Erythro.Role(_pingRoles[_pingOptions[option]]);
			rolesAdded.Add(role);
		}

		// Add non-PingRole roles to desired roles.
		// These roles must be kept the same as before.
		HashSet<DiscordRole> roles = new (rolesAdded);
		foreach(DiscordRole role in user.Roles) {
			if (!_pingRoles.Contains(role.Id))
				roles.Add(role);
		}

		// Update member roles.
		await user.ReplaceRolesAsync(roles);
		Log.Information("Updated member roles for {Username}.", user.DisplayName);

		// Update Selection object.
		(Selection selection, _) = _menus[user.Id];
		HashSet<string> options = new ();
		foreach (DiscordRole role in rolesAdded)
			options.Add(_pingOptions[_pingRoles[role.Id]]);
		await selection.Update(options);
	}

	private static async Task AssignGuildsAsync(ComponentInteractionCreateEventArgs e) {
		if (Erythro is null)
			throw new InvalidOperationException("Guild not initialized yet.");

		// Fetch member object data.
		Interaction interaction = Interaction.FromComponent(e);
		DiscordMember? user = await interaction.User.ToMember();
		if (user is null) {
			await RespondNoDataAsync(interaction);
			return;
		}

		// Fetch list of desired roles.
		// Added GuildRoles are saved separately (to update Selection).
		HashSet<DiscordRole> rolesAdded = new ();
		foreach (string option in e.Values) {
			DiscordRole role =
				Erythro.Role(_guildRoles[_guildOptions[option]]);
			rolesAdded.Add(role);
		}

		// Add non-GuildRole roles to desired roles.
		// These roles must be kept the same as before.
		HashSet<DiscordRole> roles = new (rolesAdded);
		foreach(DiscordRole role in user.Roles) {
			if (!_guildRoles.Contains(role.Id))
				roles.Add(role);
		}

		// Update member roles.
		await user.ReplaceRolesAsync(roles);
		Log.Information("Updated member roles for {Username}.", user.DisplayName);

		// Update Selection object.
		(_, Selection selection) = _menus[user.Id];
		HashSet<string> options = new ();
		foreach (DiscordRole role in rolesAdded)
			options.Add(_guildOptions[_guildRoles[role.Id]]);
		await selection.Update(options);
	}

	// Convenince function for responding when member data is missing.
	private static async Task RespondNoDataAsync(Interaction interaction) {
		DiscordFollowupMessageBuilder error =
			new DiscordFollowupMessageBuilder()
			.WithContent(
				$"""
				Failed to fetch your server data.
				:frowning: Try again in a moment?
				"""
			)
			.AsEphemeral(true);
		await interaction.FollowupAsync(error);
	}
}
