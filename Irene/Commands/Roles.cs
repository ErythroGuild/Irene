using System.Collections.Generic;
using System.IO;

using DSharpPlus.Entities;

using static Irene.Program;
using static Irene.Commands.Roles;

namespace Irene.Commands {
	using Entry = Selection<PingRole>.Entry;
	using id_r = RoleIDs;
	using id_e = EmojiIDs;

	class Roles : ICommands {
		public enum PingRole {
			Raid,
			Mythics, KSM, Gearing,
			Events, Herald,
		}

		static readonly Dictionary<PingRole, Entry> options = new () {
			{ PingRole.Raid, new Entry {
				label = "Raid",
				id = "option_raid",
				emoji = new ("\U0001F409"), // :dragon:
				description = "Raid announcements.",
			} },
			{ PingRole.Mythics, new Entry {
				label = "M+",
				id = "option_mythics",
				emoji = new ("\U0001F5FA"), // :map:
				description = "M+ keys in general.",
			} },
			{ PingRole.KSM, new Entry {
				label = "KSM",
				id = "option_ksm",
				emoji = new ("\U0001F94B"), // :martial_arts_uniform:
				description = "Higher keys requiring more focus.",
			} },
			{ PingRole.Gearing, new Entry {
				label = "Gearing",
				id = "option_gearing",
				emoji = new ("\U0001F392"), // :school_satchel:
				description = "Lower keys / M0s to help gear people.",
			} },
			{ PingRole.Events, new Entry {
				label = "Events",
				id = "option_events",
				emoji = new ("\U0001F938\u200D\u2640\uFE0F"), // :woman_cartwheeling:
				//emoji = new ("\U0001FA97"), // :accordion:
				description = "Social event announcements.",
			} },
			{ PingRole.Herald, new Entry {
				label = "Herald",
				id = "option_herald",
				emoji = new ("\u2604"), // :comet:
				description = "Herald of the Titans announcements.",
			} },
		};
		
		static readonly Dictionary<string, PingRole> dict_pingRoles = new () {
			{ "raid"   , PingRole.Raid },
			{ "raids"  , PingRole.Raid },
			{ "raiding", PingRole.Raid },

			{ "m+"     , PingRole.Mythics },
			{ "mythic+", PingRole.Mythics },
			{ "mythics", PingRole.Mythics },
			{ "keys"   , PingRole.Mythics },
			{ "ksm"    , PingRole.KSM     },
			{ "gearing", PingRole.Gearing },
			{ "gear"   , PingRole.Gearing },

			{ "events", PingRole.Events },
			{ "event" , PingRole.Events },
			{ "herald", PingRole.Herald },
		};
		static readonly Dictionary<PingRole, ulong> pingRole_to_discordRole = new () {
			{ PingRole.Raid   , id_r.raid    },
			{ PingRole.Mythics, id_r.mythics },
			{ PingRole.KSM    , id_r.ksm     },
			{ PingRole.Gearing, id_r.gearing },
			{ PingRole.Events , id_r.events  },
			{ PingRole.Herald , id_r.herald  },
		};
		static readonly Dictionary<ulong, PingRole> discordRole_to_pingRole;

		const string path_intros = @"data/roles_intros.txt";
		const string delim = "=";

		// Force static initializer to run.
		public static void init() { return; }
		static Roles() {
			discordRole_to_pingRole = new Dictionary<ulong, PingRole>();
			foreach (PingRole role in pingRole_to_discordRole.Keys) {
				discordRole_to_pingRole.Add(pingRole_to_discordRole[role], role);
			}
		}

		public static string help() {
			StringWriter text = new ();

			text.WriteLine("`@Irene -roles` Shows you your current roles, and lets you modify them.");
			text.WriteLine("`@Irene -roles-info` Lists available roles and also shows a brief description.");
			text.WriteLine("Any member can view available roles, but you must be at least a Guest to update them.");

			return text.output();
		}

		public static void set(Command cmd) {
			// Make sure user is in the guild (can have roles).
			if (cmd.user is null) {
				log.info("  Cannot set roles for non-guild member.");
				_ = cmd.msg.RespondAsync("Cannot set roles for people who aren't members of the **<Erythro>** server.");
				return;
			}

			// Fetch current roles of the member.
			DiscordMember member = cmd.user;
			List<PingRole> roles_current = new ();
			foreach(DiscordRole role in member.Roles) {
				ulong role_id = role.Id;
				if (discordRole_to_pingRole.ContainsKey(role_id)) {
					roles_current.Add(discordRole_to_pingRole[role_id]);
				}
			}

			// Send message with selection menu.
			log.info("  Sending role selection menu.");
			Selection<PingRole> dropdown = new (
				options,
				assign,
				member,
				"No roles selected",
				true
			);
			DiscordMessageBuilder response =
				new DiscordMessageBuilder()
				.WithContent(print_roles(roles_current))
				.AddComponents(dropdown.get(roles_current));
			dropdown.msg =
				cmd.msg.RespondAsync(response).Result;
		}

		public static void list(Command cmd) {
			log.info("  Listing available roles.");
			StringWriter text = new ();

			text.WriteLine("*Available roles:*");
			foreach (PingRole role in pingRole_to_discordRole.Keys) {
				ulong role_id = pingRole_to_discordRole[role];
				string name = options[role].label;
				string summary = options[role].description ?? "";
				text.WriteLine($"**{name}:** {summary}");
			}
			text.WriteLine("*Use `@Irene -roles` to assign yourself roles.*");

			_ = cmd.msg.RespondAsync(text.output());
		}

		public static void royce(Command cmd) {
			const string rolls_royce = @"https://i.imgur.com/mTEdYN6.jpeg";
			log.info("  Sending Rolls Royce.");
			_ = cmd.msg.RespondAsync(rolls_royce);
		}

		// Assigns the list of roles to the member, and removes any
		// that aren't on the list.
		// Also sends welcome messages for relevant roles.
		static async void assign(List<PingRole> roles, DiscordUser user) {
			// Convert DiscordUser to DiscordMember.
			DiscordMember? member = await user.member();
			if (member is null) {
				return;
			}

			// Update member so its associated roles are current.
			member = await member.Guild.GetMemberAsync(member.Id);

			// Initialize comparison sets.
			HashSet<PingRole> roles_prev = new ();
			foreach (DiscordRole role in member.Roles) {
				ulong role_id = role.Id;
				if (discordRole_to_pingRole.ContainsKey(role_id)) {
					roles_prev.Add(discordRole_to_pingRole[role_id]);
				}
			}
			HashSet<PingRole> roles_new = new (roles);

			// Find removed/added roles.
			HashSet<PingRole> roles_removed = new (roles_prev);
			roles_removed.ExceptWith(roles_new);
			HashSet<PingRole> roles_added = new (roles_new);
			roles_added.ExceptWith(roles_prev);

			// Remove/add roles.
			log.info($"  Removing {roles_removed.Count} role(s).");
			foreach (PingRole role in roles_removed) {
				ulong role_id = pingRole_to_discordRole[role];
				log.debug($"    Removing {role}.");
				_ = member.RevokeRoleAsync(Program.roles[role_id]);
			}
			log.info($"  Adding {roles_added.Count} role(s).");
			foreach (PingRole role in roles_added) {
				ulong role_id = pingRole_to_discordRole[role];
				log.debug($"    Adding {role}.");
				_ = member.GrantRoleAsync(Program.roles[role_id]);
				string welcome = get_welcome(role);
				_ = member.SendMessageAsync(welcome);
			}
			log.endl();
		}

		// Formats the given list of roles into a string.
		static string print_roles(List<PingRole> roles) {
			// Special cases for none/singular.
			if (roles.Count == 0) {
				return "No roles previously set.";
			}
			if (roles.Count == 1) {
				return $"Role previously set:\n**{options[roles[0]].label}**";
			}

			// Construct list of role names.
			StringWriter text = new ();
			text.WriteLine("Roles previously set:");
			foreach (PingRole role in roles) {
				text.Write($"**{options[role].label}**  ");
			}
			return text.output()[..^2];
		}

		// Read through data file to find matching welcome message.
		static string get_welcome(PingRole role) {
			string content = "";
			StreamReader data = File.OpenText(path_intros);

			while(!data.EndOfStream) {
				string line = data.ReadLine() ?? "";
				if (line.Contains(delim)) {
					string[] split = line.Split(delim, 2);
					if (dict_pingRoles[split[0]] == role) {
						content = split[1];
						break;
					}
				}
			}
			data.Close();

			content = content.unescape();
			content = $"{emojis[id_e.erythro]} {content}";
			return content;
		}
	}
}
