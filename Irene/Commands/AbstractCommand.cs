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

public abstract class AbstractCommand {
	public abstract List<string> HelpPages { get; }

	public abstract List<InteractionCommand> SlashCommands { get; }
	public virtual List<InteractionCommand> UserCommands    => new ();
	public virtual List<InteractionCommand> MessageCommands => new ();

	public virtual List<AutoCompleteHandler> AutoCompletes => new ();
}

interface IInit {
	public static void Init() { }
}

interface IInitAsync {
	public static Task InitAsync() { return Task.CompletedTask; }
}
