namespace Irene.Commands;

public record class InteractionCommand(
	DiscordApplicationCommand Command,
	InteractionHandler Deferrer,
	InteractionHandler Handler
);
public record class AutoCompleteHandler(
	string CommandName,
	InteractionHandler Handler
);
public record class DeferrerHandler(
	TimedInteraction Interaction,
	bool IsDeferrer
);

interface ICommand {
	public static abstract List<string> HelpPages { get; }

	public static abstract List<InteractionCommand> SlashCommands   { get; }
	public static abstract List<InteractionCommand> UserCommands    { get; }
	public static abstract List<InteractionCommand> MessageCommands { get; }

	public static abstract List<AutoCompleteHandler> AutoComplete { get; }
}
