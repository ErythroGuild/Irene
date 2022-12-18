namespace Irene.Modules;

using Option = ISelector.Entry;

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
	private static readonly ConcurrentDictionary<UserRoleTypeSelect, ISelector> _menus = new ();
	
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

	private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(3);

	private const string
		_idSelectPingRoles  = "selector_pingroles" ,
		_idSelectGuildRoles = "selector_guildroles";
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

		// Create `Selector` interactables.
		MessagePromise messagePromise = new ();
		Selector<PingRole> selectorPings = Selector<PingRole>.Create(
			interaction,
			messagePromise,
			s => AssignPingsAsync(s, user),
			_idSelectPingRoles,
			PingRoleOptions,
			pingRoles,
			new SelectorOptions {
				IsMultiple = true,
				Timeout = _timeout,
				Placeholder = "Select roles to be pinged for.",
			}
		);
		Selector<GuildRole> selectorGuilds = Selector<GuildRole>.Create(
			interaction,
			messagePromise,
			s => AssignGuildsAsync(s, user),
			_idSelectGuildRoles,
			GuildRoleOptions,
			guildRoles,
			new SelectorOptions {
				IsMultiple = true,
				Timeout = _timeout,
				Placeholder = "Select any guilds you associate with.",
			}
		);

		// Update global Selector tracking table, and disable any menus
		// already in-flight.
		UserRoleTypeSelect idPingSelect = new (user.Id, typeof(PingRole));
		UserRoleTypeSelect idGuildSelect = new (user.Id, typeof(GuildRole));
		if (_menus.ContainsKey(idPingSelect)) {
			ISelector menu = _menus[idPingSelect];
			// Discarding is just an extra safety, so no need to await.
			_ = menu.Discard();
			_menus.TryRemove(idPingSelect, out _);
		}
		if (_menus.ContainsKey(idGuildSelect)) {
			ISelector menu = _menus[idGuildSelect];
			// Discarding is just an extra safety, so no need to await.
			_ = menu.Discard();
			_menus.TryRemove(idGuildSelect, out _);
		}
		_menus.TryAdd(idPingSelect, selectorPings);
		_menus.TryAdd(idGuildSelect, selectorGuilds);

		// Respond to interaction.
		// Note: A response must have either content or an embed to be
		// considered valid.
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithContent(" ")
			.AddComponents(selectorPings.GetSelect())
			.AddComponents(selectorGuilds.GetSelect());
		string summary = "Role selection menus sent.";
		await interaction.RegisterAndRespondAsync(response, summary, true);

		// Update message promise.
		DiscordMessage message = await interaction.GetResponseAsync();
		messagePromise.SetResult(message);
	}

	private static Task AssignPingsAsync(IReadOnlySet<PingRole> roles, DiscordMember user) =>
		AssignRolesAsync(user, roles, _pingRoles);
	private static Task AssignGuildsAsync(IReadOnlySet<GuildRole> roles, DiscordMember user) =>
		AssignRolesAsync(user, roles, _guildRoles);
	private static Task AssignOfficersAsync(IReadOnlySet<OfficerRole> roles, DiscordMember user) =>
		AssignRolesAsync(user, roles, _officerRoles);

	private static async Task AssignRolesAsync<T>(
		DiscordMember user,
		IReadOnlySet<T> rolesSelected,
		ConstBiMap<T, ulong> tableRoleIds
	) where T : Enum {
		CheckErythroInit();

		// Fetch list of desired roles.
		HashSet<DiscordRole> roles = new ();
		foreach (T roleSelected in rolesSelected) {
			DiscordRole role = Erythro.Role(tableRoleIds[roleSelected]);
			roles.Add(role);
		}

		// Add non-enum role roles to desired roles.
		// These roles must be kept the same as before.
		foreach (DiscordRole role in user.Roles) {
			if (!tableRoleIds.Contains(role.Id))
				roles.Add(role);
		}

		// Update member roles.
		await user.ReplaceRolesAsync(roles);
		Log.Information("Updated member roles for {Username}.", user.DisplayName);
	}
}
