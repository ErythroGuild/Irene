using System;
using System.Collections.Generic;

using DSharpPlus.Entities;

using static Irene.Const;
using static Irene.Program;
using Irene.Commands;

namespace Irene;

using id_r  = RoleIDs;

class Command {
	public enum AccessLevel {
		None,
		Guest, Member, Officer, Admin,
	};

	// Command data
	static readonly Dictionary<string, Action<Command>> dict_cmd = new () {
		{ "help", Help.run },
		{ "h"   , Help.run },
		{ "?"   , Help.run },
		{ "version", About.run },
		{ "v"      , About.run },
		{ "build"  , About.run },

		{ "roles"      , Roles.set   },
		{ "r"          , Roles.set   },
		{ "roles-info" , Roles.list  },
		{ "rinfo"      , Roles.list  },
		{ "roles-royce", Roles.royce },

		{ "raid-time" , Raid.get_time   },
		{ "raid"      , Raid.get_info   },
		{ "raid-info" , Raid.get_info   },
		{ "raid-set-f", Raid.set_info_F },
		{ "raid-set-s", Raid.set_info_S },
		{ "logs"    , Raid.get_logs },
		{ "l"       , Raid.get_logs },
		{ "logs-set", Raid.set_logs },
		{ "lset"    , Raid.set_logs },
		{ "set-logs", Raid.set_logs },

		{ "rank"       , Rank.set_rank    },
		{ "set-rank"   , Rank.set_rank    },
		{ "promote"    , Rank.set_rank    },
		{ "demote"     , Rank.set_rank    },
		{ "guilds"     , Rank.set_guilds  },
		{ "set-guilds" , Rank.set_guilds  },
		{ "set-erythro", Rank.set_erythro },
		{ "list-trials", Rank.list_trials },
		{ "trials"     , Rank.list_trials },

		{ "tags"       , Tags.run    },
		{ "t"          , Tags.run    },
		{ "tag"        , Tags.run    },
		{ "tags-add"   , Tags.add    },
		{ "tadd"       , Tags.add    },
		{ "tag-add"    , Tags.add    },
		{ "add-tag"    , Tags.add    },
		{ "tags-edit"  , Tags.edit   },
		{ "tedit"      , Tags.edit   },
		{ "tag-edit"   , Tags.edit   },
		{ "edit-tag"   , Tags.edit   },
		{ "tags-remove", Tags.remove },
		{ "tremove"    , Tags.remove },
		{ "tag-remove" , Tags.remove },
		{ "remove-tag" , Tags.remove },

		{ "cap", Cap.run },
		{ "c"  , Cap.run },

		{ "classdiscord"  , ClassDiscords.run },
		{ "cd"            , ClassDiscords.run },
		{ "classdiscords" , ClassDiscords.run },
		{ "class-discord" , ClassDiscords.run },

		{ "invite", Invite.run },
		{ "i"     , Invite.run },
		{ "inv"   , Invite.run },

		{ "roll"  , Roll.run },
		{ "dice"  , Roll.run },
		{ "random", Roll.run },
		{ "rand"  , Roll.run },
	};
	static readonly Dictionary<Action<Command>, Func<string>> dict_help = new () {
		{ Help.run , Help.help  },
		{ About.run, About.help },

		{ Roles.set  , Roles.help },
		{ Roles.list , Roles.help },
		{ Roles.royce, Roles.help },

		{ Raid.get_time  , Raid.help_raid },
		{ Raid.get_info  , Raid.help_raid },
		{ Raid.set_info_F, Raid.help_raid },
		{ Raid.set_info_S, Raid.help_raid },
		{ Raid.get_logs  , Raid.help_logs },
		{ Raid.set_logs  , Raid.help_logs },

		{ Rank.set_rank   , Rank.help },
		{ Rank.set_guilds , Rank.help },
		{ Rank.set_erythro, Rank.help },
		{ Rank.list_trials, Rank.help },

		{ Tags.run   , Tags.help },
		{ Tags.add   , Tags.help },
		{ Tags.edit  , Tags.help },
		{ Tags.remove, Tags.help },

		{ Cap.run, Cap.help },

		{ ClassDiscords.run, ClassDiscords.help },

		{ Invite.run, Invite.help },

		{ Roll.run, Roll.help },
	};
	static readonly Dictionary<Action<Command>, AccessLevel> dict_access = new () {
		{ Help.run , AccessLevel.None  },
		{ About.run, AccessLevel.Guest },

		{ Roles.set  , AccessLevel.Guest },
		{ Roles.list , AccessLevel.None  },
		{ Roles.royce, AccessLevel.Guest },

		{ Raid.get_time  , AccessLevel.None    },
		{ Raid.get_info  , AccessLevel.None    },
		{ Raid.set_info_F, AccessLevel.Officer },
		{ Raid.set_info_S, AccessLevel.Officer },
		{ Raid.get_logs  , AccessLevel.Guest   },
		{ Raid.set_logs  , AccessLevel.Officer },

		{ Rank.set_rank   , AccessLevel.Officer },
		{ Rank.set_guilds , AccessLevel.Officer },
		{ Rank.set_erythro, AccessLevel.Officer },
		{ Rank.list_trials, AccessLevel.Officer },

		{ Tags.run   , AccessLevel.Guest   },
		{ Tags.add   , AccessLevel.Officer },
		{ Tags.edit  , AccessLevel.Officer },
		{ Tags.remove, AccessLevel.Officer },

		{ Cap.run, AccessLevel.None },

		{ ClassDiscords.run, AccessLevel.None },

		{ Invite.run, AccessLevel.Guest },

		{ Roll.run, AccessLevel.Guest },
	};

	// Properties
	public string cmd          { get; private set; }
	public string args         { get; private set; }
	public DiscordMember? user { get; private set; }
	public AccessLevel access  { get; private set; }
	public DiscordMessage msg  { get; private set; }
	
	// Return the help message for a command, without having to
	// actually construct a Command object.
	// Returns null if the command isn't recognized.
	public static string? help(string cmd) {
		cmd = cmd.Trim().ToLower();
		if (dict_cmd.ContainsKey(cmd)) {
			return dict_help[dict_cmd[cmd]]();
		} else {
			return null;
		}
	}

	// Return true if the required permissions for the command
	// are less than or equal to the given AccessLevel.
	public static bool has_permission(Action<Command> cmd, AccessLevel access) {
		return (dict_access[cmd] <= access);
	}
	
	// Extract the needed members (DiscordMember and access level)
	// from the DiscordMessage itself.
	public Command(string cmd, DiscordMessage msg) {
		// Process cmd / args.
		if (!cmd.Contains(' ')) {
			this.cmd = cmd;
			args = "";
		} else {
			string[] split = cmd.Split(' ', 2);
			this.cmd = split[0];
			args = split[1];
		}
		this.cmd = this.cmd.TrimStart('-').ToLower();
		this.msg = msg;

		// Parse the user.
		// If private channel, search through member list.
		if (msg.Channel.IsPrivate) {
			user = msg.Author.member().Result;
		} else {
			user = msg.Author as DiscordMember;
		}

		// Assign permissions.
		if (user is null) {
			access = AccessLevel.None;
			log.warning("  Could not convert message author to DiscordMember.");
		} else {
			List<DiscordRole> roles_user = new (user.Roles);
			access = roles_user switch {
				List<DiscordRole> r when r.Contains(roles[id_r.admin])   => AccessLevel.Admin,
				List<DiscordRole> r when r.Contains(roles[id_r.officer]) => AccessLevel.Officer,
				List<DiscordRole> r when r.Contains(roles[id_r.member])  => AccessLevel.Member,
				List<DiscordRole> r when r.Contains(roles[id_r.guest])   => AccessLevel.Guest,
				_ => AccessLevel.None,
			};
		}
	}

	// Dispatch a fully constructed Command.
	// Checks if the message author has sufficient permissions.
	// Invokes the help command if the command isn't recognized.
	public void invoke() {
		if (dict_cmd.ContainsKey(cmd)) {
			if (has_permission(dict_cmd[cmd], access)) {
				dict_cmd[cmd](this);
			} else {
				AccessLevel access = dict_access[dict_cmd[cmd]];
				msg.RespondAsync($"Sorry, this command requires the {access} role to use. :lock:");
				log.warning($"  {user?.tag() ?? "<unknown user>"} does not have access to this command.");
			}
		} else {
			dict_cmd["help"](this);
		}
	}

	// Returns the help message when the given Command has
	// already been constructed (syntactic sugar for the static
	// equivalent).
	public string help() {
		return dict_help[dict_cmd[cmd]]();
	}
}
