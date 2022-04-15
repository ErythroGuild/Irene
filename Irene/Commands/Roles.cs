using Irene.Components;

namespace Irene.Commands;

using Entry = Selection.Option;

class Roles : ICommand {
	public enum PingRole {
		Raid,
		Mythics, KSM, Gearing,
		Events, Herald,
	}

	// Selection menus, indexed by the ID of the member accessing them.
	private static readonly ConcurrentDictionary<ulong, Selection> _selects = new ();

	private static readonly ConcurrentDictionary<PingRole, Entry> _options = new () {
		[PingRole.Raid] = new Entry {
			Label = "Raid",
			Id = "option_raid",
			Emoji = new ("\U0001F409"), // :dragon:
			Description = "Raid announcements.",
		},
		[PingRole.Mythics] = new Entry {
			Label = "M+",
			Id = "option_mythics",
			Emoji = new ("\U0001F5FA"), // :map:
			Description = "M+ keys in general.",
		},
		[PingRole.KSM] = new Entry {
			Label = "KSM",
			Id = "option_ksm",
			Emoji = new ("\U0001F94B"), // :martial_arts_uniform:
			Description = "Higher keys requiring more focus.",
		},
		[PingRole.Gearing] = new Entry {
			Label = "Gearing",
			Id = "option_gearing",
			Emoji = new ("\U0001F392"), // :school_satchel:
			Description = "Lower keys / M0s to help gear people.",
		},
		[PingRole.Events] = new Entry {
			Label = "Events",
			Id = "option_events",
			Emoji = new ("\U0001F938\u200D\u2640\uFE0F"), // :woman_cartwheeling:
			Description = "Social event announcements.",
		},
		[PingRole.Herald] = new Entry {
			Label = "Herald",
			Id = "option_herald",
			Emoji = new ("\u2604"), // :comet:
			Description = "Herald of the Titans announcements.",
		},
	};
	private static readonly ConcurrentDictionary<PingRole, ulong> _table_RoleToId = new () {
		[PingRole.Raid   ] = id_r.raid   ,
		[PingRole.Mythics] = id_r.mythics,
		[PingRole.KSM    ] = id_r.ksm    ,
		[PingRole.Gearing] = id_r.gearing,
		[PingRole.Events ] = id_r.events ,
		[PingRole.Herald ] = id_r.herald ,
	};
	private static readonly ConcurrentDictionary<ulong, PingRole> _table_IdToRole;

	private static object _lock = new ();
	const string _pathIntros = @"data/role-intros.txt";
	const string _delim = "=";

	// Force static initializer to run.
	public static void Init() { return; }
	static Roles() {
		_table_IdToRole = Util.Invert(_table_RoleToId);
		Util.CreateIfMissing(_pathIntros, ref _lock);
	}

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@"`/roles` shows all available roles, and lets you assign them to yourself.",
			"You can reassign them at any time.",
			"You will receive notification pings for the roles you select.",
			"*(Sometimes Discord will fail to grant you the roles you selected; if that happens, just try again.)*",
		} ) };
	}

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"roles",
				"Check and assign roles to receive notifications for.",
				defaultPermission: true,
				type: ApplicationCommandType.SlashCommand
			), RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }
	public static List<AutoCompleteHandler> AutoComplete   { get => new (); }

	public static async Task RunAsync(TimedInteraction interaction) {
		// Ensure user is a member. (This should always succeed.)
		// Roles can only be set for users in the same guild.
		DiscordUser user = interaction.Interaction.User;
		DiscordMember? member = await user.ToMember();
		if (member is null) {
			string response_error = "Could not determine who you are.";
			response_error += (Guild is not null && interaction.Interaction.ChannelId != id_ch.bots)
				? $"\nTry running the command again, in {Channels[id_ch.bots].Mention}?"
				: "\nIf this still doesn't work in a moment, message Ernie and he will take care of it.";
			await Command.RespondAsync(
				interaction,
				response_error, true,
				"Could not convert DiscordUser to DiscordMember.",
				LogLevel.Warning,
				"User: {Username}#{Discriminator}.",
				user.Username, user.Discriminator
			);
			return;
		}

		// Fetch current (ping) roles of the user.
		List<PingRole> roles = new ();
		foreach (DiscordRole role in member.Roles) {
			ulong role_id = role.Id;
			if (IsPingRole(role_id))
				roles.Add(ToPingRole(role_id));
		}
		roles.Sort();

		// Create a registered Selection object.
		MessagePromise message_promise = new ();
		Selection select = Selection.Create(
			interaction.Interaction,
			AssignRoles,
			message_promise.Task,
			_options,
			roles,
			"No roles selected",
			isMultiple: true
		);

		// Disable any selections already in-flight.
		if (_selects.ContainsKey(member.Id)) {
			await _selects[member.Id].Discard();
			_selects.TryRemove(member.Id, out _);
		}
		_selects.TryAdd(member.Id, select);

		// Send response with selection menu.
		string roles_str =
			Selection.PrintRoles(roles, _options, "role", "roles");
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithContent(roles_str)
			.AddComponents(select.Component);
		await Command.RespondAsync(
			interaction,
			response, true,
			"Sending role selection menu.",
			LogLevel.Debug,
			"Selection menu sent."
		);

		// Update DiscordMessage object for Selection.
		DiscordMessage message = await
			interaction.Interaction.GetOriginalResponseAsync();
		message_promise.SetResult(message);
	}

	private static async Task AssignRoles(ComponentInteractionCreateEventArgs e) {
		// Make sure Guild is initialized.
		if (Guild is null) {
			Log.Information("Updating user roles.");
			Log.Error("  Guild not initialized; could not update roles.");
			await e.Interaction.AcknowledgeComponentAsync();
			Log.Information("  Failed to update roles. No changes made.");
			return;
		}

		// Convert DiscordUser to DiscordMember.
		DiscordUser user = e.Interaction.User;
		DiscordMember? member = await user.ToMember();
		if (member is null) {
			Log.Information("Updating user roles.");
			Log.Error("  Could not convert {User} to DiscordMember.", user);
			await e.Interaction.AcknowledgeComponentAsync();
			Log.Information("  Failed to update roles. No changes made.");
			return;
		}

		// Signal that interaction was received.
		await e.Interaction.AcknowledgeComponentAsync();

		// Fetch list of desired roles.
		List<DiscordRole> roles_new = new ();
		List<string> selected = new (e.Interaction.Data.Values);
		foreach (PingRole role in _options.Keys) {
			if (selected.Contains(_options[role].Id))
				roles_new.Add(Guild.GetRole(ToDiscordId(role)));
		}

		// Fetch list of current roles.
		List<DiscordRole> roles_current = new (member.Roles);

		// Add non-PingRole roles list of roles to be assigned, and
		// compile list of added roles.
		List<DiscordRole> roles_added = new ();
		foreach (DiscordRole role in roles_current) {
			if (!IsPingRole(role.Id)) {
				roles_new.Add(role);
			} else if (!roles_current.Contains(role)) {
				roles_added.Add(role);
			}
		}

		// Update member roles.
		Log.Information("Updating member roles for {Member}.", member.DisplayName);
		await member.ReplaceRolesAsync(roles_new);
		Log.Information("  Updated roles successfully.");

		// Send welcome messages (if Guest or lower).
		await Welcome(member, roles_added);

		// Update select component.
		List<Entry> options_updated = new ();
		foreach (PingRole key in _options.Keys) {
			DiscordRole role = Guild.GetRole(ToDiscordId(key));
			if (roles_new.Contains(role))
				options_updated.Add(_options[key]);
		}
		await _selects[user.Id].Update(options_updated);
		Log.Debug("  Updated select component successfully.");
	}

	// Syntax sugar for checking if a DiscordRole is a PingRole.
	private static bool IsPingRole(ulong id) =>
		_table_IdToRole.ContainsKey(id);

	// Syntax sugar for type conversions.
	private static PingRole ToPingRole(ulong id) =>
		_table_IdToRole[id];
	private static ulong ToDiscordId(PingRole role) =>
		_table_RoleToId[role];

	// Format and send welcome message to member.
	// If member has access level above Guest, then skip.
	private static async Task Welcome(DiscordMember member, List<DiscordRole> roles_added) {
		// Skip sending message if conditions aren't met.
		if (Guild is null)
			return;
		if (member.HasAccess(AccessLevel.Member))
			return;
		if (roles_added.Count == 0)
			return;

		// Convert list of DiscordRoles to their option IDs.
		List<string> ids_added = new ();
		foreach (PingRole key in _options.Keys) {
			DiscordRole role = Guild.GetRole(ToDiscordId(key));
			if (roles_added.Contains(role))
				ids_added.Add(_options[key].Id);
		}

		// Read in selected role intros.
		List<string> welcomes = new ();
		lock (_lock) {
			using StreamReader data = File.OpenText(_pathIntros);
			while (!data.EndOfStream) {
				string line = data.ReadLine() ?? "";
				if (line.Contains(_delim)) {
					string[] split = line.Split(_delim, 2);
					if (ids_added.Contains(split[0]))
						welcomes.Add(split[1].Unescape());
				}
			}
		}

		// Format welcome message.
		string response = $"Thank you for subscribing to pings; welcome aboard! :tada:{Emojis[id_e.eryLove]}";
		foreach (string welcome in welcomes)
			response += $"\n\u2022 {welcome}";
		response += "\n*You can unsubscribe at anytime, or temporarily mute a server / channel.*";
		await member.SendMessageAsync(response);
	}
}
