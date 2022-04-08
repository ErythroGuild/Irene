namespace Irene.Commands;

public record class InteractionCommand
	(DiscordApplicationCommand Command, InteractionHandler Handler);
public record class AutoCompleteHandler
	(string CommandName, InteractionHandler Handler);

interface ICommand {
	public static abstract List<string> HelpPages { get; }

	public static abstract List<InteractionCommand> SlashCommands   { get; }
	public static abstract List<InteractionCommand> UserCommands    { get; }
	public static abstract List<InteractionCommand> MessageCommands { get; }

	public static abstract List<AutoCompleteHandler> AutoComplete { get; }
}
