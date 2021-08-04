using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;

using static Irene.Program;

namespace Irene.Commands {
	using id_r = RoleIDs;
	class Rank : ICommands {
		public enum Type {
			None,
			Guest, Member, Officer,
			// Admin,
		};
		public enum Guild {
			Erythro,
			Glaive, Dragons, Angels, Asgard, Enclave,
		};

		// Conversions / definitions.
		static readonly List<ulong> roles_officer = new () {
			id_r.raidOfficer,
			id_r.mythicOfficer,
			id_r.eventPlanner,
			id_r.recruiter,
			id_r.banker,
		};
		static readonly List<ulong> roles_guild = new () {
			id_r.erythro,
			id_r.glaive,
			id_r.dragons,
			id_r.angels,
			id_r.asgard,
			id_r.enclave,
		};
		static readonly Dictionary<ulong, Type> id_to_rank = new () {
			{ id_r.guest  , Type.Guest   },
			{ id_r.member , Type.Member  },
			{ id_r.officer, Type.Officer },
			// { id_r.admin  , Type.Admin   },
		};
		static readonly Dictionary<Guild, ulong> guild_to_id = new () {
			{ Guild.Erythro, id_r.erythro },
			{ Guild.Glaive , id_r.glaive  },
			{ Guild.Dragons, id_r.dragons },
			{ Guild.Angels , id_r.angels  },
			{ Guild.Asgard , id_r.asgard  },
			{ Guild.Enclave, id_r.enclave },
		};
		static readonly Dictionary<string, Guild> dict_guilds = new () {
			{ "erythro"         , Guild.Erythro },
			{ "ery"             , Guild.Erythro },
			{ "uwupizza"        , Guild.Erythro },
			{ "uwupizzadelivery", Guild.Erythro },

			{ "glaiveofmothermoon", Guild.Glaive },
			{ "glaive"            , Guild.Glaive },

			{ "dragon'sreach", Guild.Dragons },
			{ "dragonsreach" , Guild.Dragons },
			{ "dragon's"     , Guild.Dragons },
			{ "dragons"      , Guild.Dragons },

			{ "hoovedangels", Guild.Angels },
			{ "hooved"      , Guild.Angels },
			{ "angels"      , Guild.Angels },

			{ "asgard", Guild.Asgard },

			{ "kalimdorenclave", Guild.Enclave },
			{ "enclave"        , Guild.Enclave },
		};

		// Formatting tokens.
		const string em = "\u2003";

		public static string help() {
			StringWriter text = new ();

			text.WriteLine("It is recommended to use user IDs to specify members.");
			text.WriteLine("Although nicknames can be used instead of user ID, the nickname often contains special characters.");
			text.WriteLine(":lock: `@Irene -set-erythro` gives the user **Guest** permissions and the **<Erythro>** tag.");
			text.WriteLine(":lock: `@Irene -trials` lists all current trials (**Guest** *and* **<Erythro>**.");
			text.WriteLine(":lock: `@Irene -promote <user-id>` promotes the user from **None** -> **Guest** -> **Member**;");
			text.WriteLine(":lock: `@Irene -demote <user-id>` demotes the user from **Member** -> **Guest** -> **None**.");
			text.WriteLine(":lock: `@Irene -add-guilds <user-id> <guild1> [<guild2> ...]` gives the user the requested guild tags;");
			text.WriteLine(":lock: `@Irene -clear-guilds <user-id>` clears all guild tags from the user.");
			text.WriteLine(":lock: `@Irene -rank-strip-all-roles <user-id>` removes **ALL** roles from the user.");

			return text.output();
		}

		// Grant Guest permissions and tag as <Erythro>.
		public static void set_erythro(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }

			// Grant the applicable roles.
			DiscordMember member = members[0];
			Type rank = sanitize_ranks(member);
			List<DiscordRole> member_roles = new (member.Roles);
			StringWriter text = new ();
			if (rank == Type.None) {
				log.info($"  Promoting {member.DisplayName} to Guest.");
				_ = member.GrantRoleAsync(roles[id_r.guest]);
				text.WriteLine($"Promoted {member.Mention} to **Guest**.");
			} else {
				log.info($"  {member.DisplayName} does not need to be promoted.");
				text.WriteLine($"{member.Mention} already has basic server permissions.");
			}
			if (!member_roles.Contains(roles[id_r.erythro])) {
				log.info($"  Tagging {member.DisplayName} as <Erythro>.");
				_ = member.GrantRoleAsync(roles[id_r.erythro]);
				text.WriteLine($"Tagging {member.Mention} as a member of **<Erythro>**.");
			} else {
				log.info($"  {member.DisplayName} does not need to be tagged.");
				text.WriteLine($"{member.Mention} is already tagged as **<Erythro>**.");
			}
			_ = cmd.msg.RespondAsync(text.output());
		}

		public static void add_guilds(Command cmd) {
			// Parse arguments.
			string[] split = cmd.args.Split(' ', 2);
			string user_str = split[0].Trim();
			string guilds_str = split[1].Trim();

			// Handle ambiguous / unspecified users.
			List<DiscordMember> members = parse_member(user_str);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }

			// Assign requested guild tags.
			DiscordMember member = members[0];
			List<Guild> guilds = parse_guilds(guilds_str);
			log.info($"  Adding guild tags to {member.DisplayName}.");
			foreach (Guild guild in guilds) {
				DiscordRole role = roles[guild_to_id[guild]];
				log.debug($"    Added role @{role.Name}.");
				_ = member.GrantRoleAsync(role);
			}
			_ = cmd.msg.RespondAsync($"Added requested guild tags to {member.Mention}.");
		}
		public static void clear_guilds(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }

			// Clear guild role tags from the member.
			DiscordMember member = members[0];
			List<DiscordRole> member_roles = new (member.Roles);
			log.info($"  Clearing guild tags from {member.DisplayName}.");
			foreach (DiscordRole role in member_roles) {
				if (roles_guild.Contains(role.Id)) {
					log.debug($"    Removed role @{role.Name}.");
					_ = member.RevokeRoleAsync(role);
				}
			}
			_ = cmd.msg.RespondAsync($"Cleared all guild tags from {member.Mention}.");
		}

		public static void promote(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }

			// Promote the unambiguous member.
			DiscordMember member = members[0];
			Type rank = sanitize_ranks(member);
			if (rank == Type.None) {
				log.info($"  Promoting {member.DisplayName} to Guest.");
				_ = member.GrantRoleAsync(roles[id_r.guest]);
				_ = cmd.msg.RespondAsync($"Promoted {member.Mention} to **Guest**.");
				return;
			}
			if (rank == Type.Guest) {
				log.info($"  Promoting {member.DisplayName} from Guest to Member.");
				_ = member.GrantRoleAsync(roles[id_r.member]);
				_ = member.RevokeRoleAsync(roles[id_r.guest]);
				_ = cmd.msg.RespondAsync($"Promoted {member.Mention} from **Guest** to **Member**.");
				return;
			}
			log.info($"  {member.DisplayName} is already {rank} rank.");
			_ = cmd.msg.RespondAsync($"{member.Mention} could not be promoted.");
		}
		public static void demote(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }

			// Demote the unambiguous member.
			DiscordMember member = members[0];
			Type rank = sanitize_ranks(member);
			if (rank == Type.Member) {
				log.info($"  Demoting {member.DisplayName} from Member to Guest.");
				_ = member.GrantRoleAsync(roles[id_r.guest]);
				_ = member.RevokeRoleAsync(roles[id_r.member]);
				_ = cmd.msg.RespondAsync($"Demoted {member.Mention} from **Member** to **Guest**.");
				return;
			}
			if (rank == Type.Guest) {
				log.info($"  Demoting {member.DisplayName} from Guest.");
				_ = member.RevokeRoleAsync(roles[id_r.guest]);
				_ = cmd.msg.RespondAsync($"Demoted {member.Mention} from **Guest**.");
				return;
			}
			log.info($"  {member.DisplayName} could not be demoted.");
			_ = cmd.msg.RespondAsync($"{member.Mention} could not be demoted.");
		}

		public static void promote_officer(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }

			// Promote the unambiguous member.
			DiscordMember member = members[0];
			Type rank = sanitize_ranks(member);
			if (rank == Type.Member) {
				log.info($"  Promoting {member.DisplayName} from Member to Officer.");
				_ = member.GrantRoleAsync(roles[id_r.officer]);
				_ = member.RevokeRoleAsync(roles[id_r.member]);
				_ = cmd.msg.RespondAsync($"Promoted {member.Mention} from **Member** to **Officer**.");
			} else {
				log.info($"  {member.DisplayName} is not a Member.");
				_ = cmd.msg.RespondAsync($"{member.Mention} could not be promoted.");
			}
		}
		public static void demote_officer(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }

			// Demote the unambiguous member.
			DiscordMember member = members[0];
			Type rank = sanitize_ranks(member);
			List<DiscordRole> member_roles = new (member.Roles);
			if (rank == Type.Officer) {
				log.info($"  Demoting {member.DisplayName} from Officer to Member.");
				_ = member.GrantRoleAsync(roles[id_r.member]);
				_ = member.RevokeRoleAsync(roles[id_r.officer]);
				_ = cmd.msg.RespondAsync($"Demoted {member.Mention} from **Officer** to **Member**.");
				
				// Remove officer roles.
				foreach (DiscordRole role in member_roles) {
					if (roles_officer.Contains(role.Id)) {
						log.info($"    Removing @{role.Name}.");
						_ = member.RevokeRoleAsync(role);
					}
				}
			} else {
				log.info($"  {member.DisplayName} is not an Officer.");
				_ = cmd.msg.RespondAsync($"{member.Mention} could not be demoted.");
			}
		}
		
		public static void strip(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }

			// Demote the unambiguous member.
			DiscordMember member = members[0];
			List<DiscordRole> member_roles = new (member.Roles);
			if (member_roles.Count > 0) {
				log.info($"  Stripping {member.DisplayName} of ALL roles.");
				_ = member.ReplaceRolesAsync(new List<DiscordRole>());
				_ = cmd.msg.RespondAsync($"Stripped {member.Mention} of **ALL** roles.");
			} else {
				log.info($"  {member.DisplayName} has no roles to strip.");
				_ = cmd.msg.RespondAsync($"{member.Mention} has no roles to strip.");
			}
		}

		public static void list_trials(Command cmd) {
			// Can't search for member if guilds aren't even loaded.
			if (!is_guild_loaded) {
				log.warning("  Guild data not loaded yet, cannot list members.");
				_ = cmd.msg.RespondAsync("Guild data not loaded yet; please retry in a moment.");
				return;
			}

			// Fetch non-cached members.
			// If guild is loaded, `Program.guild` has been initialized.
			DiscordGuild erythro = guild!;
			List<DiscordMember> members = new (
				erythro.GetAllMembersAsync()
				.Result );

			// Fetch reference roles.
			DiscordRole role_guest = roles[id_r.guest];
			DiscordRole role_guild = roles[id_r.erythro];

			// Construct list of members who are "trials".
			// (Guest + <Erythro>)
			List<DiscordMember> trials = new ();
			foreach (DiscordMember member in members) {
				List<DiscordRole> roles_i = new (member.Roles);
				if (roles_i.Contains(role_guest) && roles_i.Contains(role_guild)) {
					trials.Add(member);
				}
			}

			// Handle case where no trial members exist.
			if (trials.Count == 0) {
				log.info("  No trial members found.");
				_ = cmd.msg.RespondAsync("No Trial members found for **<Erythro>**.");
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
			StringWriter text = new ();
			foreach (DiscordMember trial in trials) {
				TimeSpan time = DateTimeOffset.Now - trial.JoinedAt;
				log.debug($"    {trial.tag()} - {time.Days} days old");
				text.WriteLine($"{trial.Mention} - {time.Days} days old");
			}
			_ = cmd.msg.RespondAsync(text.output());
		}

		// Returns a list of all Guilds with recognized strings.
		// The list can be empty.
		static List<Guild> parse_guilds(string arg) {
			List<Guild> guilds = new ();

			// Parse through all guild strings.
			Regex regex_guild = new (@"<(?<name>[\w'\s]+)>");
			MatchCollection matches = regex_guild.Matches(arg);
			foreach (Match match in matches) {
				string guild = match.Groups["name"].Value;
				guild = guild.Trim().ToLower();
				guild = guild.Replace(" ", "");
				if (dict_guilds.ContainsKey(guild)) {
					guilds.Add(dict_guilds[guild]);
				}
			}

			return guilds;
		}

		// Ensures that a member only has a single (their highest)
		// "rank" role.
		// Returns that highest rank.
		static Type sanitize_ranks(DiscordMember member) {
			List<DiscordRole> member_roles = new (member.Roles);
			Type rank = highest_rank(member);

			// Clear all ranks lower than the highest.
			foreach (DiscordRole role in member_roles) {
				ulong id = role.Id;
				if (id_to_rank.ContainsKey(id)) {
					if (id_to_rank[id] < rank) {
						log.info($"    Rank sanitizer: removing role {role.Name}");
						member.RevokeRoleAsync(role);
					}
				}
			}

			return rank;
		}

		// Returns the highest rank of the member, or returns
		// Type.None if the member has no rank roles.
		static Type highest_rank(DiscordMember member) {
			List<DiscordRole> member_roles = new (member.Roles);
			Type rank_highest = Type.None;

			// Search through all roles.
			foreach (DiscordRole role in member_roles) {
				ulong id = role.Id;
				if (id_to_rank.ContainsKey(id)) {
					Type rank = id_to_rank[id];
					if (rank > rank_highest) {
						rank_highest = rank;
					}
				}
			}

			return rank_highest;
		}

		// Properly log/respond if the member list isn't singular.
		// Returns true if parent function should exit early, and
		// returns false otherwise.
		static bool check_member_unique(List<DiscordMember> members, Command cmd) {
			if (members.Count == 0) {
				log.info($"  Could not find any members matching {cmd.args}.");
				StringWriter text = new ();
				text.WriteLine($"Could not find any members matching `{cmd.args}`.");
				text.WriteLine("If their display name doesn't work, try their user ID instead.");
				_ = cmd.msg.RespondAsync(text.output());
				return true;
			}
			if (members.Count > 1) {
				log.info($"  Found multiple members matching {cmd.args}.");
				StringWriter text = new ();
				text.WriteLine($"Found multiple members matching `{cmd.args}`.");
				foreach (DiscordMember member_i in members) {
					text.WriteLine($"{em}{member_i.Mention}: `{member_i.Id}`");
				}
				text.WriteLine("Try specifying a user ID instead.");
				_ = cmd.msg.RespondAsync(text.output());
				return true;
			}
			return false;
		}

		// Returns a (possibly empty) list of matching members.
		static List<DiscordMember> parse_member(string arg) {
			// Can't search for member if guilds aren't even loaded.
			if (!is_guild_loaded) {
				log.warning("    Attempted to parse DiscordMember before guild data loaded.");
				return new List<DiscordMember>();
			}

			// Fetch non-cached members.
			// If guild is loaded, `Program.guild` has been initialized.
			DiscordGuild erythro = guild!;
			List<DiscordMember> members = new (
				erythro.GetAllMembersAsync()
				.Result );

			// Set up variables.
			arg = arg.Trim();

			// Check against ID / @mention.
			Regex regex_id = new (@"(?<id><@!?\d+>|\d+)");
			if (regex_id.IsMatch(arg)) {
				string id_str = regex_id.Match(arg).Groups["id"].Value;
				bool can_parse = ulong.TryParse(id_str, out ulong id);
				if (can_parse) {
					foreach (DiscordMember member in members) {
						if (member.Id == id) {
							return new List<DiscordMember>() { member };
						}
					}
				}
			}

			// Check against names.
			arg = arg.ToLower();
			List<DiscordMember> results = new ();
			foreach (DiscordMember member in members) {
				if (member.DisplayName.ToLower().StartsWith(arg)) {
					results.Add(member);
				}
			}
			return results;
		}
	}
}
