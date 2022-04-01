namespace Irene.Commands;

public record class InteractionCommand
	(DiscordApplicationCommand Command, InteractionHandler Handler);

interface ICommand {
	public static abstract List<string> HelpPages { get; }

	public static abstract List<InteractionCommand> SlashCommands { get; }
	public static abstract List<InteractionCommand> UserCommands { get; }
	public static abstract List<InteractionCommand> MessageCommands { get; }
}
