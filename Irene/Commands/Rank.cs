using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;

using static Irene.Program;

namespace Irene.Commands {
	using RankEntry = Selection<Rank.Type>.Entry;
	using GuildEntry = Selection<Rank.Guild>.Entry;
	using id_r = RoleIDs;

	class Rank : ICommands {
		public enum Type {
			None,
			Guest, Member, Officer,
			Admin,
		};
		public enum Guild {
			Erythro,
			Glaive, Dragons, Angels, Asgard, Enclave,
		};

		static readonly Dictionary<Type, RankEntry> options_rank = new () {
			{ Type.None, new RankEntry {
				label = "No Rank",
				id = "option_none",
				emoji = new ("\U0001F401"), // :mouse2:
				description = "No rank assigned.",
			} },
			{ Type.Guest, new RankEntry {
				label = "Guest",
				id = "option_guest",
				emoji = new ("\U0001F41B"), // :bug:
				description = "Verified member (newer member).",
			} },
			{ Type.Member, new RankEntry {
				label = "Member",
				id = "option_member",
				emoji = new ("\U0001F98B"), // :butterfly:
				description = "Trusted member (older member).",
			} },
			{ Type.Officer, new RankEntry {
				label = "Officer",
				id = "option_officer",
				emoji = new ("\U0001F426"), // :bird:
				description = "Officer / moderator.",
			} },
		};
		static readonly Dictionary<Guild, GuildEntry> options_guild = new () {
			{ Guild.Erythro, new GuildEntry {
				label = "<Erythro>",
				id = "option_erythro",
			} },
			{ Guild.Glaive, new GuildEntry {
				label = "<Glaive of Mother Moon>",
				id = "option_glaive",
			} },
			{ Guild.Dragons, new GuildEntry {
				label = "<Dragon's Reach>",
				id = "option_dragons",
			} },
			{ Guild.Angels, new GuildEntry {
				label = "<Hooved Angels>",
				id = "option_angels",
			} },
			{ Guild.Asgard, new GuildEntry {
				label = "<Asgard>",
				id = "option_asgard",
			} },
			{ Guild.Enclave, new GuildEntry {
				label = "<Kalimdor Enclave>",
				id = "option_enclave",
			} },
		};

		// Conversions / definitions.
		static readonly Dictionary<Type, ulong> rank_to_id = new () {
			{ Type.Guest  , id_r.guest   },
			{ Type.Member , id_r.member  },
			{ Type.Officer, id_r.officer },
			{ Type.Admin  , id_r.admin   },
		};
		static readonly Dictionary<ulong, Type> id_to_rank;
		static readonly Dictionary<Guild, ulong> guild_to_id = new () {
			{ Guild.Erythro, id_r.erythro },
			{ Guild.Glaive , id_r.glaive  },
			{ Guild.Dragons, id_r.dragons },
			{ Guild.Angels , id_r.angels  },
			{ Guild.Asgard , id_r.asgard  },
			{ Guild.Enclave, id_r.enclave },
		};
		static readonly Dictionary<ulong, Guild> id_to_guild;
		static readonly HashSet<ulong> roles_officer = new () {
			id_r.raidOfficer,
			id_r.mythicOfficer,
			id_r.eventPlanner,
			id_r.recruiter,
			id_r.banker,
		};

		// Formatting tokens.
		const string em = "\u2003";

		// Force static initializer to run.
		public static void init() { return; }
		static Rank() {
			id_to_rank = rank_to_id.inverse();
			id_to_guild = guild_to_id.inverse();
		}

		public static string help() {
			StringWriter text = new ();

			text.WriteLine("It is recommended to use user IDs to specify members.");
			text.WriteLine("Although nicknames can be used instead of user ID, the nickname often contains special characters.");
			text.WriteLine(":lock: `@Irene -rank` Displays the user's rank, and lets you update them.");
			text.WriteLine(":lock: `@Irene -guild` Displays the user's guilds, and lets you modify them.");
			text.WriteLine(":lock: `@Irene -set-erythro` gives the user **Guest** permissions and the **<Erythro>** tag.");
			text.WriteLine(":lock: `@Irene -trials` lists all current trials (**Guest** *and* **<Erythro>**.");

			return text.output();
		}

		// Grant specific permissions to a user, with some restrictions.
		// Officers can only set ranks lower than Officer.
		// Only Admin can modify the Officer rank.
		public static void set_rank(Command cmd) {
			// Handle ambiguous / unspecified / illegal cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }
			do_exit_early = check_caller_membership(cmd);
			if (do_exit_early)
				{ return; }
			do_exit_early = check_arg_exists(cmd);
			if (do_exit_early)
				{ return; }

			// Calculate the allowed modifiable ranks.
			DiscordMember member = members[0];
			Type rank = sanitize_ranks(member);
			string rank_str = $"Previous rank: **{options_rank[rank].label}**";
			Dictionary<Type, RankEntry> options = new () {
				{ Type.None  , options_rank[Type.None  ] },
				{ Type.Guest , options_rank[Type.Guest ] },
				{ Type.Member, options_rank[Type.Member] },
			};
			if ( highest_rank(cmd.user!) == Type.Admin ||
				cmd.user!.Id == member.Id ) {
				options.Add(Type.Officer, options_rank[Type.Officer]);
			}

			// Send message with selection menu.
			log.info("  Sending rank selection menu.");
			Selection<Type> dropdown = new (
				options,
				set_rank,
				member,
				cmd.user!,
				"Select a rank to set",
				false
			);
			DiscordMessageBuilder response =
				new DiscordMessageBuilder()
				.WithContent(rank_str)
				.AddComponents(dropdown.get(rank));
			dropdown.msg =
				cmd.msg.RespondAsync(response).Result;
		}

		// Set the guild tags for a specific user.
		// Cannot modify guild tags of another user if their highest
		// rank is not lower than the user's own highest rank.
		public static void set_guilds(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }
			do_exit_early = check_caller_membership(cmd);
			if (do_exit_early)
				{ return; }
			do_exit_early = check_arg_exists(cmd);
			if (do_exit_early)
				{ return; }

			// Fetch the current guilds of the member.
			DiscordMember member = members[0];
			List<Guild> guilds_current = new ();
			foreach (DiscordRole role in member.Roles) {
				ulong role_id = role.Id;
				if (id_to_guild.ContainsKey(role_id)) {
					guilds_current.Add(id_to_guild[role_id]);
				}
			}

			// Send message with selection menu.
			log.info("  Sending guild selection menu.");
			Selection<Guild> dropdown = new (
				options_guild,
				set_guilds,
				member,
				cmd.user!,
				"No guilds selected",
				true
			);
			DiscordMessageBuilder response =
				new DiscordMessageBuilder()
				.WithContent(print_guilds(guilds_current))
				.AddComponents(dropdown.get(guilds_current));
			dropdown.msg =
				cmd.msg.RespondAsync(response).Result;
		}

		// Shortcut to grant Guest rank and tag as <Erythro>.
		public static void set_erythro(Command cmd) {
			// Handle ambiguous / unspecified cases.
			List<DiscordMember> members = parse_member(cmd.args);
			bool do_exit_early = check_member_unique(members, cmd);
			if (do_exit_early)
				{ return; }
			do_exit_early = check_caller_membership(cmd);
			if (do_exit_early)
				{ return; }
			do_exit_early = check_arg_exists(cmd);
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

		// List all server Guest members and tagged as <Erythro>.
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

		// Callback from the Selection to set member's rank.
		static async void set_rank(List<Type> rank, DiscordUser user) {
			DiscordMember? member = await user.member();
			if (member is null) {
				log.error("Could not find DiscordMember to assign roles.");
				log.endl();
				return;
			}

			// Update member so its associated roles are current.
			member = await member.Guild.GetMemberAsync(member.Id);

			// Assign updated rank.
			log.info($"  Assigning new rank to {member.DisplayName}.");
			Type rank_prev = sanitize_ranks(member);
			Type rank_new = rank[0];
			if (rank_to_id.ContainsKey(rank_new)) {
				_ = member.GrantRoleAsync(roles[rank_to_id[rank_new]]);
			}
			if (rank_to_id.ContainsKey(rank_prev)) {
				_ = member.RevokeRoleAsync(roles[rank_to_id[rank_prev]]);
			}
			sanitize_ranks(member);	// remove potential "officer" roles

			// Send congrats message.
			if (rank_new > rank_prev && rank_new >= Type.Member) {
				StringWriter text = new ();
				text.WriteLine($"Congrats! You've been promoted to **{options_rank[rank_new].label}**.");
				text.WriteLine("If your in-game ranks haven't been updated, just ask an Officer to update them.");
				_ = member.SendMessageAsync(text.output());
			}
		}

		// Callback from the Selection to set the member's guilds.
		static async void set_guilds(List<Guild> guilds, DiscordUser user) {
			DiscordMember? member = await user.member();
			if (member is null) {
				log.error("Could not find DiscordMember to assign roles.");
				log.endl();
				return;
			}

			// Update member so its associated roles are current.
			member = await member.Guild.GetMemberAsync(member.Id);

			// Initialize comparison sets.
			HashSet<Guild> guilds_prev = new ();
			foreach (DiscordRole role in member.Roles) {
				ulong role_id = role.Id;
				if (id_to_guild.ContainsKey(role_id)) {
					guilds_prev.Add(id_to_guild[role_id]);
				}
			}
			HashSet<Guild> guilds_new = new (guilds);

			// Find removed/added guilds.
			HashSet<Guild> guilds_removed = new (guilds_prev);
			guilds_removed.ExceptWith(guilds_new);
			HashSet<Guild> guilds_added = new (guilds_new);
			guilds_added.ExceptWith(guilds_prev);

			// Remove/add guild roles.
			log.info($"  Removing {guilds_removed.Count} guild role(s).");
			foreach (Guild guild in guilds_removed) {
				ulong role_id = guild_to_id[guild];
				log.debug($"    Removing {roles[role_id]}.");
				_ = member.RevokeRoleAsync(roles[role_id]);
			}
			log.info($"  Adding {guilds_added.Count} guild role(s).");
			foreach (Guild guild in guilds_added) {
				ulong role_id = guild_to_id[guild];
				log.debug($"    Adding {roles[role_id]}.");
				_ = member.GrantRoleAsync(roles[role_id]);
			}
			log.endl();
		}

		// Ensures that a member only has a single (their highest)
		// "rank" role.
		// Returns that highest rank.
		static Type sanitize_ranks(DiscordMember member) {
			List<DiscordRole> member_roles = new (member.Roles);
			Type rank = highest_rank(member);

			// Only sanitize ranks lower than Officer for Admin.
			if (rank == Type.Admin) {
				rank = Type.Officer;
			}

			// Clear Officer role ranks, if applicable.
			if (rank != Type.Officer) {
				foreach (DiscordRole role in member_roles) {
					if (roles_officer.Contains(role.Id)) {
						log.info($"    Rank sanitizer: removing role {role.Name}");
						member.RevokeRoleAsync(role);
					}
				}
			}

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

		// Formats the given list of guilds into a string.
		static string print_guilds(List<Guild> guilds) {
			// Special cases for none/singular.
			if (guilds.Count == 0) {
				return "Not a member of any guilds.";
			}
			if (roles.Count == 1) {
				return $"Guild previously set:\n**{options_guild[guilds[0]].label}**";
			}

			// Construct list of guild names.
			StringWriter text = new ();
			text.WriteLine("Guilds previously set:");
			foreach (Guild guild in guilds) {
				text.Write($"**{options_guild[guild].label}**  ");
			}
			return text.output()[..^2];
		}

		// Properly log/respond if the member list isn't singular.
		// Returns true if parent function should exit early, and
		// returns false otherwise.
		static bool check_member_unique(List<DiscordMember> members, Command cmd) {
			if (members.Count == 0) {
				log.info($"  Could not find any members matching {cmd.args}.");
				log.endl();
				StringWriter text = new ();
				text.WriteLine($"Could not find any members matching `{cmd.args}`.");
				text.WriteLine("If their display name doesn't work, try their user ID instead.");
				_ = cmd.msg.RespondAsync(text.output());
				return true;
			}
			if (members.Count > 1) {
				log.info($"  Found multiple members matching {cmd.args}.");
				log.endl();
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

		// Properly log/respond if the `DiscordMember` of the caller
		// couldn't be determined.
		// Returns true if parent function should exit early, and
		// returns false otherwise.
		static bool check_caller_membership(Command cmd) {
			if (cmd.user is not null)
				{ return false; }

			log.warning("  Could not determine caller's DiscordMember value.");
			log.endl();
			StringWriter text = new ();
			text.WriteLine("Could not determine your guild rank.");
			text.WriteLine("This is probably an internal error; contact Ernie for more info.");
			_ = cmd.msg.RespondAsync(text.output());

			return true;
		}

		// Ensure a target user exists.
		// Returns true if parent function should exit early, and
		// returns false otherwise.
		static bool check_arg_exists(Command cmd) {
			if (cmd.args.Trim() != "")
				{ return false; }

			log.info("No target user argument specified.");
			log.endl();
			StringWriter text = new StringWriter();
			text.WriteLine("You must specify a user to modify the rank of.");
			text.WriteLine("This can be an `@mention`, their user ID, or their username, if it's unambiguous.");
			text.WriteLine("See also: `@Irene -help rank`.");

			return true;
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
				erythro.GetAllMembersAsync().Result
			);

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
