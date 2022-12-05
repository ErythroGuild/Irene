namespace Irene.Modules;

class Help {
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
		const string flower = "\u273F";
		IReadOnlyDictionary<string, CommandHandler> commands =
			Dispatcher.Table;

		string HelpText(string command) => commands[command].HelpText;

		return new () {
			$"""
			{HelpText(Commands.Help.CommandHelp)}
			    *If you need any help, ask, or DM Ernie! :+1:*

			{flower} **About** {flower}
			{HelpText(Commands.About.Command_About)}

			{flower} **Discord Servers** {flower}
			{HelpText(Commands.Invite.CommandInvite)}
			{HelpText(Commands.ClassDiscord.CommandClassDiscord)}

			{flower} **Suggestions** {flower}
			{/*HelpText("suggest")*/ "[WIP]"}
			""",

			$"""
			{flower} **Rank** {flower}
			{/*HelpText("rank")*/ "[WIP]"}
			
			{flower} **Roles** {flower}
			{HelpText(Commands.Roles.Command_Roles)}
			
			{flower} **Birthday** {flower}
			{/*HelpText("birthday")*/ "[WIP]"}
			""",

			$"""
			{flower} **Keys** {flower}
			{/*HelpText("keys")*/ "[WIP]"}
			
			{flower} **Raid** {flower}
			{/*HelpText("raid")*/ "[WIP]"}
			""",

			$"""
			{flower} **Roster** {flower}
			{/*HelpText("roster")*/ "[WIP]"}

			{flower} **Crafters** {flower}
			{/*HelpText("craft")*/ "[WIP]"}
			""",

			$"""
			{flower} **Tags** {flower}
			{/*HelpText("tags")*/ "[WIP]"}
			
			{flower} **Farming Guides** {flower}
			{HelpText(Commands.Farm.Command_Farm)}

			{flower} **M+ Routes** {flower}
			{/*HelpText("mdt")*/ "[WIP]"}
			""",

			$"""
			{flower} **Raid Boss Guides** {flower}
			{/*HelpText("boss-guide")*/ "[WIP]"}
			
			{flower} **Dungeon Guides** {flower}
			{/*HelpText("dungeon-guide")*/ "[WIP]"}
			""",

			$"""
			{flower} **In-Game Data** {flower}
			{HelpText(Commands.Cap.Command_Cap)}
			{/*HelpText("emissaries")*/ "[WIP]"}
			{/*HelpText("assaults")*/ "[WIP]"}
			{HelpText(Commands.WowToken.CommandWowToken)}
			
			{flower} **Solvers** {flower}
			{/*HelpText("solve")*/ "[WIP]"}
			""",

			$"""
			{flower} **Macros** {flower}
			{/*HelpText("macro")*/ "[WIP]"}

			{flower} **Tools** {flower}
			{/*HelpText("remind-me")*/ "[WIP]"}
			{/*HelpText("checklist")*/ "[WIP]"}
			{HelpText(Commands.Translate.CommandTranslate)}
			""",

			$"""
			{flower} **Awards** {flower}
			{/*HelpText("commend")*/ "[WIP]"}
			{HelpText(Commands.Starboard.Command_BestOf)}
			{/*HelpText("cupcake")*/ "[WIP]"}

			{flower} **Audit Logs** {flower}
			{/*HelpText("audit-log")*/ "[WIP]"}
			""",

			$"""
			{flower} **Moderation** {flower}
			{/*HelpText("move-post")*/ "[WIP]"}
			{/*HelpText("slowmode")*/ "[WIP]"}
			{/*HelpText("change-subject")*/ "[WIP]"}
			{/*HelpText("ban")*/ "[WIP]"}

			{flower} **Community** {flower}
			{/*HelpText("poll")*/ "[WIP]"}
			{/*HelpText("event")*/ "[WIP]"}
			{/*HelpText("movie-night")*/ "[WIP]"}
			""",

			$"""
			{flower} **Utilities** {flower}
			{HelpText(Commands.Roll.Command_Roll)}
			{HelpText(Commands.Random.Command_Random)}
			{/*HelpText("aww")*/ "[WIP]"}
			{HelpText(Commands.Mimic.CommandMimic)}

			{flower} **Bot Status** {flower}
			{HelpText(Commands.IreneStatus.Command_Status)}
			""",
		};
	}
}
