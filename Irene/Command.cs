using System;
using System.Collections.Generic;

using DSharpPlus.Entities;

using static Irene.Program;
using Irene.Modules;

namespace Irene {
	using id_r  = RoleIDs;

	class Command {
		public enum AccessLevel {
			None,
			Guest, Member, Officer, Admin,
		};

		// Command data
		static readonly Dictionary<string, Action<Command>> dict_cmd = new () {
			{ "invite", Invite.run_command },
			{ "inv",    Invite.run_command },
			{ "i",      Invite.run_command },
		};
		static readonly Dictionary<Action<Command>, Func<string>> dict_help = new () {
			{ Invite.run_command, Invite.help },
		};
		static readonly Dictionary<Action<Command>, AccessLevel> dict_access = new () {
			{ Invite.run_command, AccessLevel.None },
		};

		// Properties
		public string cmd          { get; private set; }
		public string args         { get; private set; }
		public DiscordMember? user { get; private set; }
		public AccessLevel access  { get; private set; }
		public DiscordMessage msg  { get; private set; }
		
		// Return the help message for a command, without having to
		// actually construct a Command object.
		public static string help(string cmd) {
			return dict_help[dict_cmd[cmd]]();
		}

		// Return true if the required permissions for the command
		// are less than or equal to the given AccessLevel.
		public static bool has_permission(Action<Command> cmd, AccessLevel access) {
			return (dict_access[cmd] <= access);
		}
		
		// Extract the needed members (DiscordMember and access level)
		// from the DiscordMessage itself.
		public Command(string cmd, DiscordMessage msg) {
			if (!cmd.Contains(' ')) {
				this.cmd = cmd;
				args = "";
			} else {
				string[] split = cmd.Split(' ', 2);
				this.cmd = split[0];
				args = split[1];
			}
			this.cmd = this.cmd.TrimStart('-');

			this.msg = msg;
			user = msg.Author as DiscordMember;

			if (user is null) {
				access = AccessLevel.None;
				log.warning("  Could not convert message author to DiscordMember.");
			} else {
				access = user.Roles switch {
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
					msg.RespondAsync($"Sorry, this command requires the {access} to use. ::");
					log.warning($"  {user?.Username??"x"}#{user?.Discriminator??"xxxx"} does not have access to this command.");
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
}
