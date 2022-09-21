using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Irene.Commands;

namespace Irene;

static class CommandDispatcher {
	private static ReadOnlyDictionary<string, CommandHandler> _handlerTable;

	static CommandDispatcher() {
		Register();
	}

	// Registration replaces the internal handler table with a snapshot
	// of handlers at call time. This is called by the static initializer,
	// but can also be manually invoked.
	[MemberNotNull(nameof(_handlerTable))]
	public static void Register() {
		ConcurrentDictionary<string, CommandHandler> handlerTable = new ();

		List<Type> types = new (Assembly.GetExecutingAssembly().GetTypes());
		foreach (Type type in types) {
			if (type.BaseType != typeof(AbstractCommand))
				continue;

			// Check for a default constructor.
			ConstructorInfo? constructor = type.GetConstructor(Type.EmptyTypes);
			if (constructor is null) {
				Log.Error("Could not find default constructor for CommandHandler class: {ClassName}", type.FullName);
				continue;
			}

			// Create an handler instance to register to the lookup table.
			// Passing `null` to `Invoke()` invokes with no args.
			CommandHandler handler = (CommandHandler)constructor.Invoke(null);
			handlerTable.TryAdd(handler.Command.Name, handler);
		}

		Log.Debug("Registered {HandlerCount} commands.", handlerTable.Count);
		_handlerTable = new (handlerTable);
	}

	public static bool CanHandle(string commandName) =>
		_handlerTable.ContainsKey(commandName);
	public static Task HandleAsync(string commandName, Interaction interaction) {
		if (!_handlerTable.ContainsKey(commandName))
			throw new ArgumentException("Unregistered command.", nameof(commandName));

		return _handlerTable[commandName].HandleAsync(interaction);
	}
}
