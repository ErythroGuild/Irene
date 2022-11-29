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
	public enum OfficerRole {
		Raid,
		Events, Recruiter, Banker,
	}

	// An indexer for the master table of selection menus. Each selection
	// menu should be uniquely identified by member and the type of menu.
	// This means each user should only have one menu of each type open
	// at once.
	private record class UserRoleTypeSelect(ulong UserId, Type RoleTypeEnum);
	// Master table of selection menus.
	// NOTE: New selection menus should be registered here.
	private static readonly ConcurrentDictionary<UserRoleTypeSelect, Selection> _menus = new ();
	
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
	private static readonly ConstBiMap<OfficerRole, ulong> _officerRoles = new (
		new Dictionary<OfficerRole, ulong> {
			[OfficerRole.Raid     ] = id_r.raidOfficer ,
			[OfficerRole.Events   ] = id_r.eventPlanner,
			[OfficerRole.Recruiter] = id_r.recruiter   ,
			[OfficerRole.Banker   ] = id_r.banker      ,
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
	private static readonly ConstBiMap<OfficerRole, string> _officerOptions = new (
		new Dictionary<OfficerRole, string> {
			[OfficerRole.Raid     ] = _optionOfficerRaid     ,
			[OfficerRole.Events   ] = _optionOfficerEvents   ,
			[OfficerRole.Recruiter] = _optionOfficerRecruiter,
			[OfficerRole.Banker   ] = _optionOfficerBanker   ,
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
	private static IReadOnlyList<(OfficerRole, Option)> OfficerRoleOptions =>
		new List<(OfficerRole, Option)> {
			new (OfficerRole.Raid, new Option {
				Label = "Raid Officer",
				Id = _optionOfficerRaid,
				Emoji = new ("\u2694"),     // :crossed_swords:
			}),
			new (OfficerRole.Events, new Option {
				Label = "Event Planner",
				Id = _optionOfficerEvents,
				Emoji = new ("\U0001F3B3"), // :bowling:
			}),
			new (OfficerRole.Recruiter, new Option {
				Label = "Recruiter",
				Id = _optionOfficerRecruiter,
				Emoji = new ("\U0001F5C3"), // :card_box:
			}),
			new (OfficerRole.Banker, new Option {
				Label = "Banker",
				Id = _optionOfficerBanker,
				Emoji = new ("\U0001F4B0"), // :moneybag:
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
		_optionGuildAsgard  = "option_asgard" ,
		_optionOfficerRaid      = "option_raid"     ,
		_optionOfficerEvents    = "option_events"   ,
		_optionOfficerRecruiter = "option_recruiter",
		_optionOfficerBanker    = "option_banker"   ;

	// Public utility methods for checking the roles on a member object.
	public static IReadOnlySet<PingRole> GetPingRoles(DiscordMember user) {
		HashSet<PingRole> roles = new ();
		foreach (DiscordRole role in user.Roles) {
			ulong id = role.Id;
			if (_pingRoles.Contains(id))
				roles.Add(_pingRoles[id]);
		}
		return roles;
	}
	public static IReadOnlySet<GuildRole> GetGuildRoles(DiscordMember user) {
		HashSet<GuildRole> roles = new ();
		foreach (DiscordRole role in user.Roles) {
			ulong id = role.Id;
			if (_guildRoles.Contains(id))
				roles.Add(_guildRoles[id]);
		}
		return roles;
	}
	public static IReadOnlySet<OfficerRole> GetOfficerRoles(DiscordMember user) {
		HashSet<OfficerRole> roles = new ();
		foreach (DiscordRole role in user.Roles) {
			ulong id = role.Id;
			if (_officerRoles.Contains(id))
				roles.Add(_officerRoles[id]);
		}
		return roles;
	}

	// Respond to a slash command interaction.
	public static async Task RespondAsync(
		Interaction interaction,
		DiscordMember user
	) {
		// Initialize current roles of user.
		// Writing this out instead of using the utility methods saves
		// a foreach loop.
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
		UserRoleTypeSelect idPingSelect = new (user.Id, typeof(PingRole));
		UserRoleTypeSelect idGuildSelect = new (user.Id, typeof(GuildRole));
		if (_menus.ContainsKey(idPingSelect)) {
			Selection menu = _menus[idPingSelect];
			// Discarding is just an extra safety, so no need to await.
			_ = menu.Discard();
			_menus.TryRemove(idPingSelect, out _);
		}
		if (_menus.ContainsKey(idGuildSelect)) {
			Selection menu = _menus[idGuildSelect];
			// Discarding is just an extra safety, so no need to await.
			_ = menu.Discard();
			_menus.TryRemove(idGuildSelect, out _);
		}
		_menus.TryAdd(idPingSelect, selectPings);
		_menus.TryAdd(idGuildSelect, selectGuilds);

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

	private static Task AssignPingsAsync(ComponentInteractionCreateEventArgs e) =>
		AssignRolesAsync(e, _pingRoles, _pingOptions);
	private static Task AssignGuildsAsync(ComponentInteractionCreateEventArgs e) =>
		AssignRolesAsync(e, _guildRoles, _guildOptions);
	private static Task AssignOfficersAsync(ComponentInteractionCreateEventArgs e) =>
		AssignRolesAsync(e, _officerRoles, _officerOptions);

	private static async Task AssignRolesAsync<T>(
		ComponentInteractionCreateEventArgs e,
		ConstBiMap<T, ulong> tableRoleIds,
		ConstBiMap<T, string> tableRoleOptions
	) where T : Enum {
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
		// Added enum roles are saved separately (to update Selection).
		HashSet<DiscordRole> rolesAdded = new ();
		foreach (string option in e.Values) {
			DiscordRole role =
				Erythro.Role(tableRoleIds[tableRoleOptions[option]]);
			rolesAdded.Add(role);
		}

		// Add non-enum role roles to desired roles.
		// These roles must be kept the same as before.
		HashSet<DiscordRole> roles = new (rolesAdded);
		foreach (DiscordRole role in user.Roles) {
			if (!tableRoleIds.Contains(role.Id))
				roles.Add(role);
		}

		// Update member roles.
		await user.ReplaceRolesAsync(roles);
		Log.Information("Updated member roles for {Username}.", user.DisplayName);

		// Update Selection object.
		Selection selection = _menus[new (user.Id, typeof(T))];
		HashSet<string> options = new ();
		foreach (DiscordRole role in rolesAdded)
			options.Add(tableRoleOptions[tableRoleIds[role.Id]]);
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
