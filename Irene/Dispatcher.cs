namespace Irene;

using System.Reflection;

using static CommandHandler;

static class Dispatcher {
	public static IReadOnlyDictionary<string, CommandHandler> Table { get; private set; }
	public static IReadOnlyList<string> CommandNames =>
		new List<string>(Table.Keys);
	public static IReadOnlyList<CommandHandler> Handlers =>
		new List<CommandHandler>(Table.Values);

	static Dispatcher() =>
		Table = new ConcurrentDictionary<string, CommandHandler>();

	// Requires the command to be registered (`ReplaceAllHandlers()`).
	public static string Mention(string command, params string[] subcommands) {
		List<string> tokens = new () { command };
		tokens.AddRange(subcommands);
		string commandString = string.Join(" ", tokens);
		return Util.Mention(Table[command].Command, commandString);
	}

	public static bool CanHandle(string commandName) =>
		Table.ContainsKey(commandName);
	public static Task<ResultType> HandleAsync(string commandName, Interaction interaction) =>
		Table.ContainsKey(commandName)
			? Table[commandName].HandleAsync(interaction)
			: throw new UnknownCommandException(commandName);

	// This replaces the entire internal handler table with a snapshot
	// of the handlers evaluated at call time. This is called by the static
	// initializer, but can also be manually invoked.
	public static void ReplaceAllHandlers() {
		ConcurrentDictionary<string, CommandHandler> handlerTable = new ();

		List<Type> types = new (Assembly.GetExecutingAssembly().GetTypes());
		foreach (Type type in types) {
			if (type.BaseType != typeof(CommandHandler))
				continue;

			// Check for the default (no args) constructor.
			ConstructorInfo? constructor =
				type.GetConstructor(Array.Empty<Type>());
			if (constructor is null) {
				Log.Error("  Could not find default constructor for CommandHandler class: {ClassName}", type.FullName);
				continue;
			}

			// Create an handler instance to register to the lookup table.
			CommandHandler handler =
				(CommandHandler)constructor
				.Invoke(Array.Empty<object>());
			handlerTable.TryAdd(handler.Command.Name, handler);
		}

		Log.Debug("  Added {HandlerCount} commands to Dispatcher.", handlerTable.Count);
		Table = handlerTable;
	}
}
