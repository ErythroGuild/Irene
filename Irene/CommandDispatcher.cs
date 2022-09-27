using System.Reflection;

namespace Irene;

static class CommandDispatcher {
	public static IReadOnlyDictionary<string, CommandHandler> HandlerTable { get; private set; }
	public static IReadOnlyList<CommandHandler> Handlers =>
		new List<CommandHandler>(HandlerTable.Values);

	static CommandDispatcher() {
		HandlerTable = new ConcurrentDictionary<string, CommandHandler>();
	}

	// Registration replaces the internal handler table with a snapshot
	// of handlers at call time. This is called by the static initializer,
	// but can also be manually invoked.
	public static void Register(GuildData guildData) {
		ConcurrentDictionary<string, CommandHandler> handlerTable = new ();

		List<Type> types = new (Assembly.GetExecutingAssembly().GetTypes());
		foreach (Type type in types) {
			if (type.BaseType != typeof(CommandHandler))
				continue;

			// Check for the standard `GuildData` constructor.
			ConstructorInfo? constructor =
				type.GetConstructor(new Type[1] {typeof(GuildData)});
			if (constructor is null) {
				Log.Error("Could not find default constructor for CommandHandler class: {ClassName}", type.FullName);
				continue;
			}

			// Create an handler instance to register to the lookup table.
			CommandHandler handler =
				(CommandHandler)constructor.Invoke(new object[1] {guildData});
			handlerTable.TryAdd(handler.Command.Name, handler);
		}

		Log.Debug("Registered {HandlerCount} commands.", handlerTable.Count);
		HandlerTable = handlerTable;
	}

	public static bool CanHandle(string commandName) =>
		HandlerTable.ContainsKey(commandName);
	public static Task HandleAsync(string commandName, Interaction interaction) {
		if (!HandlerTable.ContainsKey(commandName))
			throw new ArgumentException("Unregistered command.", nameof(commandName));

		return HandlerTable[commandName].HandleAsync(interaction);
	}
}
