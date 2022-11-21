namespace Irene.Modules;

class Help {
	// Returns the help text for a command, and returns null if no handler
	// for the given command has been registered.
	// This function does not perform normalization on its input.
	public static string? CommandHelp(string command) {
		IReadOnlyDictionary<string, CommandHandler> commands =
			CommandDispatcher.HandlerTable;

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
			CommandDispatcher.HandlerTable;

		string HelpText(string command) => commands[command].HelpText;

		return new () {
			$"""
			{HelpText(Commands.Help.Command_Help)}
			    *If you need any help, ask, or DM Ernie! :+1:*

			{flower} **About** {flower}
			{HelpText(Commands.About.Command_About)}

			{flower} **Audit Logs** {flower}
			{/*HelpText("audit-log")*/ "[WIP]"}

			{flower} **Discord Servers** {flower}
			{HelpText(Commands.Invite.Command_Invite)}
			{HelpText(Commands.ClassDiscord.Command_ClassDiscord)}
			""",

			//$"""
			//{flower} **Rank** {flower}
			//{HelpText("rank")}
			//
			//{flower} **Roles** {flower}
			//{HelpText("roles")}
			//
			//{flower} **Birthday** {flower}
			//{HelpText("birthday")}
			//""",

			//$"""
			//{flower} **Keys** {flower}
			//{HelpText("keys")}
			//
			//{flower} **Raid** {flower}
			//{HelpText("raid")}
			//""",

			//$"""
			//{flower} **Roster** {flower}
			//{HelpText("roster")}
			//{HelpText("craft")}
			//""",

			//$"""
			//{flower} **Tags** {flower}
			//{HelpText("tags")}
			//
			//{flower} **Boss Guides** {flower}
			//{HelpText("boss-guide")}
			//
			//{flower} **Farming Guides** {flower}
			//{HelpText("farm-guide")}
			//""",

			$"""
			{flower} **In-Game Data** {flower}
			{HelpText(Commands.Cap.Command_Cap)}
			{/*HelpText("emissaries")*/ "[WIP]"}
			{/*HelpText("assaults")*/ "[WIP]"}
			
			{flower} **Solvers** {flower}
			{/*HelpText("solve")*/ "[WIP]"}
			""",

			$"""
			{flower} **Awards** {flower}
			{/*HelpText("commend")*/ "[WIP]"}
			{HelpText(Commands.Starboard.Command_BestOf)}
			
			{flower} **Moderation** {flower}
			{/*HelpText("move-post")*/ "[WIP]"}
			{/*HelpText("slowmode")*/ "[WIP]"}
			{/*HelpText("change-subject")*/ "[WIP]"}
			{/*HelpText("ban")*/ "[WIP]"}
			""",

			$"""
			{flower} **Utilities** {flower}
			{HelpText(Commands.Roll.Command_Roll)}
			{HelpText(Commands.Random.Command_Random)}

			{flower} **Bot Status** {flower}
			{HelpText(Commands.IreneStatus.Command_Status)}
			""",
		};
	}
}
