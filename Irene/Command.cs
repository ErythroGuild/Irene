using System.Reflection;

using Irene.Commands;

namespace Irene;

using RoleList = List<DiscordRole>;

class Command {
	public static List<DiscordApplicationCommand> Commands { get; private set; }
	public static Dictionary<string, InteractionHandler> Handlers { get; private set; }
	public static Dictionary<string, InteractionHandler> AutoCompletes { get; private set; }

	// Force static initializer to run.
	public static void Init() { return; }
	static Command() {
		// Set the static properties.
		Commands = new ();
		Handlers = new ();
		AutoCompletes = new ();

		// Find all classes inheriting from ICommand, and collate their application
		// commands into a single Dictionary.
		Type[] types = Assembly.GetExecutingAssembly().GetTypes();
		foreach (Type type in types) {
			List<Type> interfaces = new (type.GetInterfaces());
			if (interfaces.Contains(typeof(ICommand))) {
				// Fetch the property, null-checking at every step.
				// If any step fails, simply return early.
				void AddPropertyInteractions(string name) {
					PropertyInfo? property =
						type.GetProperty(name, typeof(List<InteractionCommand>));
					if (property is null)
						return;

					List<InteractionCommand>? commands =
						property?.GetValue(null) as List<InteractionCommand>
						?? null;
					if (commands is null)
						return;

					foreach (InteractionCommand command in commands) {
						Commands.Add(command.Command);
						Handlers.Add(command.Command.Name, command.Handler);
					}
				}
				void AddAutoCompletes() {
					PropertyInfo? property =
						type.GetProperty("AutoComplete", typeof(List<AutoCompleteHandler>));
					if (property is null)
						return;

					List<AutoCompleteHandler>? handlers =
						property?.GetValue(null) as List<AutoCompleteHandler>
						?? null;
					if (handlers is null)
						return;

					foreach (AutoCompleteHandler handler in handlers) {
						AutoCompletes.Add(handler.CommandName, handler.Handler);
					}
				}

				AddPropertyInteractions("SlashCommands");
				AddPropertyInteractions("UserCommands");
				AddPropertyInteractions("MessageCommands");
				AddAutoCompletes();
			}
		}
	}

	// Returns the highest available access level of the user who invoked the
	// the interaction.
	public static async Task<AccessLevel> GetAccessLevel(DiscordInteraction interaction) {
		// Extract channel/user data.
		DiscordChannel channel = interaction.Channel;
		DiscordUser user = interaction.User;
		DiscordMember? member = channel.IsPrivate
			? await user.ToMember()
			: user as DiscordMember;

		// Warn if could not cast to DiscordMember.
		if (member is null) {
			Log.Warning("    Could not convert user ({UserTag}) to member.", user.Tag());
			return AccessLevel.None;
		}

		// Return no results if guild not initialized yet.
		if (Guild is null) {
			Log.Warning("    Guild not initialized yet. Assigning default permissions.");
			return AccessLevel.None;
		}

		return GetAccessLevel(member);
	}
	// Returns the highest access level the member has access to.
	public static AccessLevel GetAccessLevel(DiscordMember member) {
		static bool HasRole(RoleList r, ulong id) =>
			r.Contains(Program.Roles[id]);
		RoleList roles = new (member.Roles);
		return roles switch {
			RoleList r when HasRole(r, id_r.admin  ) => AccessLevel.Admin,
			RoleList r when HasRole(r, id_r.officer) => AccessLevel.Officer,
			RoleList r when HasRole(r, id_r.member ) => AccessLevel.Member,
			RoleList r when HasRole(r, id_r.guest  ) => AccessLevel.Guest,
			_ => AccessLevel.None,
		};
	}

	// Command data
	static readonly Dictionary<string, Action<Command>> dict_cmd = new () {
		{ "help", Help.run },
		{ "h"   , Help.run },
		{ "?"   , Help.run },

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
	};
	static readonly Dictionary<Action<Command>, Func<string>> dict_help = new () {
		{ Help.run , Help.help  },

		{ Raid.get_time  , Raid.help_raid },
		{ Raid.get_info  , Raid.help_raid },
		{ Raid.set_info_F, Raid.help_raid },
		{ Raid.set_info_S, Raid.help_raid },
		{ Raid.get_logs  , Raid.help_logs },
		{ Raid.set_logs  , Raid.help_logs },
	};
	static readonly Dictionary<Action<Command>, AccessLevel> dict_access = new () {
		{ Help.run , AccessLevel.None  },

		{ Raid.get_time  , AccessLevel.None    },
		{ Raid.get_info  , AccessLevel.None    },
		{ Raid.set_info_F, AccessLevel.Officer },
		{ Raid.set_info_S, AccessLevel.Officer },
		{ Raid.get_logs  , AccessLevel.Guest   },
		{ Raid.set_logs  , AccessLevel.Officer },
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
			user = msg.Author.ToMember().Result;
		} else {
			user = msg.Author as DiscordMember;
		}

		// Assign permissions.
		if (user is null) {
			access = AccessLevel.None;
			Log.Warning("  Could not convert message author to DiscordMember.");
		} else {
			RoleList roles_user = new (user.Roles);
			access = roles_user switch {
				RoleList r when r.Contains(Program.Roles[id_r.admin])   => AccessLevel.Admin,
				RoleList r when r.Contains(Program.Roles[id_r.officer]) => AccessLevel.Officer,
				RoleList r when r.Contains(Program.Roles[id_r.member])  => AccessLevel.Member,
				RoleList r when r.Contains(Program.Roles[id_r.guest])   => AccessLevel.Guest,
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
				Log.Warning($"  {user?.Tag() ?? "<unknown user>"} does not have access to this command.");
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
