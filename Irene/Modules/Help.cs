namespace Irene.Modules;

class Help {
	// Most commonly used / most useful / most confusing help commands.
	private static readonly List<string> _defaultOptions = new () {
		Commands.Help.CommandHelp,
		Commands.Roles.CommandRoles,
		// Alphabetize the rest:
		//Commands.Birthday.CommandBirthday,
		Commands.Farm.CommandFarm,
		Commands.Random.CommandRandom,
	};

	// Autocompleter.
	public static readonly Completer Completer = new StringCompleter(
		args => GetCommandNames(),
		args => _defaultOptions,
		12
	);
	// Returns only the slash commands' names.
	private static List<string> GetCommandNames() {
		List<string> commandNames = new ();
		IReadOnlyDictionary<string, CommandHandler> commands = Dispatcher.Table;
		foreach (string command in commands.Keys) {
			if (commands[command].Command.Type == CommandType.SlashCommand) {
				commandNames.Add(command);
			}
		}
		return commandNames;
	}

	// Returns the help text for a command, and returns null if no handler
	// for the given command has been registered.
	// This function does not perform normalization on its input.
	public static string? CommandHelp(string command) {
		IReadOnlyDictionary<string, CommandHandler> commands =
			Dispatcher.Table;

		return (!commands.ContainsKey(command))
			? null
			: $"""
				**Command Help: `/{command}`**
				{commands[command].HelpText}
				""";
	}

	// Returns the compiled list of help text for all available commands,
	// paginated with interactable Pages.
	public static List<string> GeneralHelp() {
		const string
			_h = "\u200B\u2003", // zero-width space + em space
			_t = "\u2003\u2002", // em space + en space
			_l = "\u296A",       // left harpoon above line
			_r = "\u296C";       // right harpoon above line
		IReadOnlyDictionary<string, CommandHandler> commands =
			Dispatcher.Table;

		string HelpText(string command) => commands[command].HelpText;

		return new () {
			$"""
			{_h}{_l} **Help** {_r}
			{HelpText(Commands.Help.CommandHelp)}
			{_t}*If you need any help, ask, or DM Ernie! :+1:*

			{_h}{_l} **About** {_r}
			{HelpText(Commands.About.CommandAbout)}

			{_h}{_l} **Server Invites** {_r}
			{HelpText(Commands.Invite.CommandInvite)}
			{HelpText(Commands.ClassDiscord.CommandClassDiscord)}

			{_h}{_l} **Suggestions** {_r}
			{/*HelpText("suggest")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **Rank** {_r}
			{/*HelpText("rank")*/ "[WIP]"}
			
			{_h}{_l} **Roles** {_r}
			{HelpText(Commands.Roles.CommandRoles)}
			
			{_h}{_l} **Birthday** {_r}
			{/*HelpText("birthday")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **Keys** {_r}
			{/*HelpText("keys")*/ "[WIP]"}
			
			{_h}{_l} **Raid** {_r}
			{/*HelpText("raid")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **Roster** {_r}
			{/*HelpText("roster")*/ "[WIP]"}

			{_h}{_l} **Crafters** {_r}
			{/*HelpText("craft")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **Tags** {_r}
			{HelpText(Commands.Tag.CommandTag)}
			
			{_h}{_l} **Farming Guides** {_r}
			{HelpText(Commands.Farm.CommandFarm)}

			{_h}{_l} **M+ Routes** {_r}
			{/*HelpText("mdt")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **Raid Boss Guides** {_r}
			{/*HelpText("boss-guide")*/ "[WIP]"}
			
			{_h}{_l} **Dungeon Guides** {_r}
			{/*HelpText("dungeon-guide")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **In-Game Data** {_r}
			{HelpText(Commands.Cap.CommandCap)}
			{/*HelpText("emissaries")*/ "[WIP]"}
			{/*HelpText("assaults")*/ "[WIP]"}
			{HelpText(Commands.WowToken.CommandWowToken)}
			
			{_h}{_l} **Solvers** {_r}
			{/*HelpText("solve")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **Macros** {_r}
			{/*HelpText("macro")*/ "[WIP]"}

			{_h}{_l} **Tools** {_r}
			{/*HelpText("remind-me")*/ "[WIP]"}
			{/*HelpText("checklist")*/ "[WIP]"}
			{HelpText(Commands.Translate.CommandTranslate)}
			""",

			$"""
			{_h}{_l} **Awards** {_r}
			{/*HelpText("commend")*/ "[WIP]"}
			{HelpText(Commands.Starboard.CommandBestOf)}
			{/*HelpText("cupcake")*/ "[WIP]"}

			{_h}{_l} **Audit Logs** {_r}
			{/*HelpText("audit-log")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **Moderation** {_r}
			{/*HelpText("move-post")*/ "[WIP]"}
			{/*HelpText("slowmode")*/ "[WIP]"}
			{/*HelpText("change-subject")*/ "[WIP]"}
			{/*HelpText("ban")*/ "[WIP]"}

			{_h}{_l} **Community** {_r}
			{/*HelpText("poll")*/ "[WIP]"}
			{/*HelpText("event")*/ "[WIP]"}
			{/*HelpText("movie-night")*/ "[WIP]"}
			""",

			$"""
			{_h}{_l} **Utilities** {_r}
			{HelpText(Commands.Roll.CommandRoll)}
			{HelpText(Commands.Random.CommandRandom)}
			{/*HelpText("aww")*/ "[WIP]"}
			{HelpText(Commands.Mimic.CommandMimic)}

			{_h}{_l} **Bot Status** {_r}
			{HelpText(Commands.IreneStatus.CommandStatus)}
			""",
		};
	}
}
