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
		IReadOnlyDictionary<string, CommandHandler> commands =
			CommandDispatcher.HandlerTable;

		string HelpText(string command) => commands[command].HelpText;

		return new () {
			$"""
			{HelpText(Commands.Help.Command_Help)}
			    *If you need any help, ask, or DM Ernie! :+1:*

			**About**
			{HelpText(Commands.About.Command_About)}

			**Audit Logs**
			{/*HelpText("audit-log")*/ "[WIP]"}

			**Discord Servers**
			{HelpText(Commands.Invite.Command_Invite)}
			{HelpText(Commands.ClassDiscord.Command_ClassDiscord)}
			""",

			//$"""
			//**Rank**
			//{HelpText("rank")}
			//
			//**Roles**
			//{HelpText("roles")}
			//
			//**Birthday**
			//{HelpText("birthday")}
			//""",

			//$"""
			//**Keys**
			//{HelpText("keys")}
			//
			//**Raid**
			//{HelpText("raid")}
			//""",

			//$"""
			//**Roster**
			//{HelpText("roster")}
			//{HelpText("craft")}
			//""",

			//$"""
			//**Tags**
			//{HelpText("tags")}
			//
			//**Boss Guides**
			//{HelpText("boss-guide")}
			//
			//**Farming Guides**
			//{HelpText("farm-guide")}
			//""",

			$"""
			**In-Game Data**
			{HelpText(Commands.Cap.Command_Cap)}
			{/*HelpText("emissaries")*/ "[WIP]"}
			{/*HelpText("assaults")*/ "[WIP]"}
			
			**Solvers**
			{/*HelpText("solve")*/ "[WIP]"}
			""",

			$"""
			**Awards**
			{/*HelpText("commend")*/ "[WIP]"}
			{HelpText(Commands.Starboard.Command_BestOf)}
			
			**Moderation**
			{/*HelpText("move-post")*/ "[WIP]"}
			{/*HelpText("slowmode")*/ "[WIP]"}
			{/*HelpText("change-subject")*/ "[WIP]"}
			{/*HelpText("ban")*/ "[WIP]"}
			""",

			$"""
			**Utilities**
			{HelpText(Commands.Roll.Command_Roll)}
			{HelpText(Commands.Random.Command_Random)}

			**Bot Status**
			{HelpText(Commands.IreneStatus.Command_Status)}
			""",
		};
	}
}
