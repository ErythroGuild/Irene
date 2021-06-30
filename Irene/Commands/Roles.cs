using System.Collections.Generic;
using System.IO;

using DSharpPlus.Entities;

using static Irene.Program;

namespace Irene.Commands {
	using id_r = RoleIDs;
	using id_e = EmojiIDs;

	class Roles : ICommands {
		public enum PingRole {
			Raid,
			Mythics, KSM, Gearing,
			Events, Herald,
		}

		static readonly Dictionary<PingRole, DiscordRole> pingRole_to_discordRole = new () {
			{ PingRole.Raid   , roles[id_r.raid   ] },
			{ PingRole.Mythics, roles[id_r.mythics] },
			{ PingRole.KSM    , roles[id_r.ksm    ] },
			{ PingRole.Gearing, roles[id_r.gearing] },
			{ PingRole.Events , roles[id_r.events ] },
			{ PingRole.Herald , roles[id_r.herald ] },
		};
		static readonly Dictionary<DiscordRole, PingRole> discordRole_to_pingRole;
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
		static readonly Dictionary<PingRole, string> dict_summaries = new () {
			{ PingRole.Raid   , "Raid announcements." },
			{ PingRole.Mythics, "M+ keys in general." },
			{ PingRole.KSM    , "Higher keys requiring more focus." },
			{ PingRole.Gearing, "Lower keys / M0s to help gear people." },
			{ PingRole.Events , "Social event announcements." },
			{ PingRole.Herald , "Herald of the Titans announcements." },
		};
		static readonly Dictionary<string, string> escape_codes = new () {
			{ @"\n"    , "\n"     },
			{ @"\u2022", "\u2022" },
			{ @"\u25E6", "\u25E6" },
			{ @":emsp:", "\u2003" },
			{ @":ensp:", "\u2022" },
			{ @":+-:"  , "\u00B1" },
		};

		const string path_intros = @"data/roles_intros.txt";
		const string delim = "=";

		static Roles() {
			discordRole_to_pingRole = new Dictionary<DiscordRole, PingRole>();
			foreach (PingRole role in pingRole_to_discordRole.Keys) {
				discordRole_to_pingRole.Add(pingRole_to_discordRole[role], role);
			}
		}

		public static string help() {
			StringWriter text = new ();

			text.WriteLine("`@Irene -roles` Lists the available roles to add/remove.");
			text.WriteLine("`@Irene -roles-add <role(s)>` Gives you the named role(s);");
			text.WriteLine("`@Irene -roles-remove <role(s)>` Removes the named role(s) from you.");
			text.WriteLine(":warning: Only type the name! Don't mention the role (no `@`).");
			text.WriteLine("To add/remove multiple roles at once, separate the role names with spaces.");
			text.WriteLine("You can assign yourself roles at any time, provided you are at least a Guest.");

			text.Flush();
			return text.ToString();
		}

		public static void list(Command cmd) {
			log.info("  Listing available roles.");
			StringWriter text = new ();

			text.WriteLine("*Available roles:*");
			foreach (PingRole role in pingRole_to_discordRole.Keys) {
				string name = pingRole_to_discordRole[role].Name;
				string summary = dict_summaries[role];
				text.WriteLine($"**{name}:** {summary}");
			}
			text.WriteLine("*Use `@Irene -roles-add <role(s)>` to assign yourself roles.*");

			text.Flush();
			_ = cmd.msg.RespondAsync(text.ToString());
		}

		public static void add(Command cmd) {
			// Must be a valid DiscordMember to modify roles.
			if (cmd.user is null) {
				log.warning("  User must be convertible to DiscordMember to modify roles.");
				_ = cmd.msg.RespondAsync("Could not find associated guild to modify roles for.");
				return;
			}
			List<DiscordRole> roles_current = new (cmd.user.Roles);

			// Parse command args.
			string arg = cmd.args.Trim().ToLower();
			List<PingRole> roles_arg = parse_roles(arg);

			// Iterate through the requested roles and keep the ones
			// that need to be added.
			List<DiscordRole> roles_add = new ();
			foreach (PingRole role_key in roles_arg) {
				DiscordRole role = pingRole_to_discordRole[role_key];
				if (!roles_current.Contains(role)) {
					roles_add.Add(role);
				}
			}

			// Notify if no roles were added.
			if (roles_add.Count == 0) {
				log.info("  No roles needed to be added.");

				StringWriter text = new ();
				text.WriteLine("Didn't find any roles that needed to be added.");
				text.WriteLine("See: `@Irene -help roles` for help.");
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());

				return;
			}

			// Assign roles and send welcome messages.
			log.info("  Assigning roles & sending welcome messages.");
			StringWriter text_respond = new ();
			text_respond.WriteLine($"Adding role(s):");
			foreach(DiscordRole role in roles_add) {
				_ = cmd.user.GrantRoleAsync(role);
				string welcome = get_welcome(role);
				_ = cmd.user.SendMessageAsync(welcome);
				text_respond.WriteLine($"\u2003\u2022 **{role.Name}**");
			}
			text_respond.Flush();
			_ = cmd.msg.RespondAsync(text_respond.ToString());
		}

		public static void remove(Command cmd) {
			// Must be a valid DiscordMember to modify roles.
			if (cmd.user is null) {
				log.warning("  User must be convertible to DiscordMember to modify roles.");
				_ = cmd.msg.RespondAsync("Could not find associated guild to modify roles for.");
				return;
			}
			List<DiscordRole> roles_current = new (cmd.user.Roles);

			// Parse command args.
			string arg = cmd.args.Trim().ToLower();
			List<PingRole> roles_arg = parse_roles(arg);

			// Iterate through the requested roles and keep the ones
			// that need to be removed.
			List<DiscordRole> roles_remove = new ();
			foreach (PingRole role_key in roles_arg) {
				DiscordRole role = pingRole_to_discordRole[role_key];
				if (roles_current.Contains(role)) {
					roles_remove.Add(role);
				}
			}

			// Notify if no roles were removed.
			if (roles_remove.Count == 0) {
				log.info("  No roles needed to be removed.");

				StringWriter text = new ();
				text.WriteLine("Didn't find any roles that needed to be removed.");
				text.WriteLine("See: `@Irene -help roles` for help.");
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());

				return;
			}

			// Assign roles and send welcome messages.
			log.info("  Removing roles.");
			StringWriter text_respond = new ();
			text_respond.WriteLine($"Removing role(s):");
			foreach (DiscordRole role in roles_remove) {
				_ = cmd.user.RevokeRoleAsync(role);
				text_respond.WriteLine($"\u2003\u2022 **{role.Name}**");
			}
			text_respond.Flush();
			_ = cmd.msg.RespondAsync(text_respond.ToString());
		}

		// Parse and separate a space-delimited list of role names.
		static List<PingRole> parse_roles(string arg) {
			List<PingRole> roles = new ();

			arg = arg.Trim().ToLower();
			string[] split = arg.Split(' ', pingRole_to_discordRole.Count);
			foreach (string role in split) {
				string role_key = role.Trim().ToLower();
				if (dict_pingRoles.ContainsKey(role_key)) {
					roles.Add(dict_pingRoles[role_key]);
				}
			}

			return roles;
		}

		// Read through data file to find matching welcome message.
		static string get_welcome(DiscordRole role) {
			return get_welcome(discordRole_to_pingRole[role]);
		}
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

			content = unescape(content);
			content = $"{emojis[id_e.erythro]} {content}";
			return content;
		}

		// Replace all recognized escape codes with their codepoints.
		static string unescape(string str) {
			foreach (string escape_code in escape_codes.Keys) {
				string codepoint = escape_codes[escape_code];
				str = str.Replace(escape_code, codepoint);
			}
			return str;
		}
	}
}
