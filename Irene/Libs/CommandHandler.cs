using static Irene.Interaction;

using InteractionArg = DSharpPlus.Entities.DiscordInteractionDataOption;
using DCommand = DSharpPlus.Entities.DiscordApplicationCommand;
using NodeHandler = System.Func<Irene.Interaction, System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;
using Autocompleter = System.Func<Irene.Interaction, object, System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;

namespace Irene;

// All application commands (e.g. slash commands, context menu commands)
// should inherit from this class.
abstract class CommandHandler {
	public abstract string HelpText { get; }
	public abstract CommandTree Tree { get; }
	public DCommand Command => Tree.Command;

	// Updating a registered command should only be done to populate the
	// command's ID once it has been registered with Discord.
	// This method will check that the command name matches, and will
	// throw an exception if otherwise.
	public void UpdateRegisteredCommand(DCommand command) {
		if (command.Name != Command.Name)
			throw new ArgumentException("New command name doesn't match.", nameof(command));
		Tree.UpdateRegisteredCommand(command);
	}
	public Task HandleAsync(Interaction interaction) =>
		Tree.HandleAsync(interaction);


	public class CommandTree {
		// --------
		// Record definitions (used in constructors, etc.):
		// --------

		// Commands (root-level) and subcommands share a set of fields,
		// and subcommand groups have their own.
		public record class GroupArgs(
			string Name,
			string Description,
			Permissions? DefaultPermissions
		);
		public record class LeafArgs(
			string Name,
			string Description,
			IList<CommandOption> Options,
			Permissions? DefaultPermissions
		);
		// Handlers are associated with leaf nodes (or the root command).
		public record class Handler(
			NodeHandler NodeHandler,
			IReadOnlyDictionary<string, Autocompleter> Autocompleters
		);


		// --------
		// Properties/fields, constructors, and methods:
		// --------

		// `ITree` handles most logic instead of `CommandTree`.
		private readonly ITree _tree;
		public DCommand Command { get; private set; }

		// Construct a tree that only has a single command.
		public CommandTree(
			LeafArgs args,
			NodeHandler handler,
			IDictionary<string, Autocompleter>? autocompleters=null
		) {
			autocompleters ??= new Dictionary<string, Autocompleter>();
			_tree = new RootTree(handler, new (autocompleters));

			Command = new (
				args.Name,
				args.Description,
				args.Options,
				type: ApplicationCommandType.SlashCommand,
				defaultMemberPermissions: args.DefaultPermissions
			);
		}
		// Construct a tree with subcommands / subcommand groups.
		public CommandTree(
			GroupArgs args,
			IList<GroupNode> groups,
			IList<LeafNode> leaves
		) {
			_tree = new SubTree(new (groups), new (leaves));

			// Collate all child nodes that are one level down.
			List<CommandOption> optionList = new ();
			foreach (GroupNode group in groups)
				optionList.Add(group.Group);
			foreach (LeafNode leaf in leaves)
				optionList.Add(leaf.Command);
			Command = new (
				args.Name,
				args.Description,
				optionList,
				type: ApplicationCommandType.SlashCommand,
				defaultMemberPermissions: args.DefaultPermissions
			);
		}

		public void UpdateRegisteredCommand(DCommand command) {
			Command = command;
		}
		public Task HandleAsync(Interaction interaction) =>
			_tree.HandleAsync(interaction);


		// --------
		// Internal subtree structure implementation:
		// --------

		// The `GroupNode` and `LeafNode` records are used when defining
		// a CommandTree implemented via a SubTree.
		public record class GroupNode {
			public readonly CommandOption Group;
			public readonly IReadOnlyList<LeafNode> Leaves;
			public readonly IReadOnlyDictionary<string, Handler> LeafTable;

			public GroupNode(string name, string description, IList<LeafNode> leaves) {
				Leaves = new ReadOnlyCollection<LeafNode>(leaves);

				// Collate subcommands from child nodes.
				List<CommandOption> commandList = new ();
				foreach (LeafNode leaf in leaves)
					commandList.Add(leaf.Command);
				Group = new (
					name,
					description,
					ApplicationCommandOptionType.SubCommandGroup,
					options: commandList
				);

				// Populate lookup table.
				ConcurrentDictionary<string, Handler> leafTable = new ();
				foreach (LeafNode leaf in leaves)
					leafTable.TryAdd(leaf.Command.Name, leaf.Handler);
				LeafTable = leafTable;
			}
		}
		public record class LeafNode {
			public readonly CommandOption Command;
			public readonly Handler Handler;

			public LeafNode(CommandOption command, Handler handler) {
				Command = command;
				Handler = handler;
			}
		}

		// A slash command can have either a single command (`RootTree`),
		// or it can have child nodes (`SubTree`).
		private interface ITree {
			public Task HandleAsync(Interaction interaction);
		}

		private class RootTree: ITree {
			private readonly Handler _handler;

			public RootTree(
				NodeHandler handler,
				ConcurrentDictionary<string, Autocompleter>? autocompleters=null
			) {
				autocompleters ??= new ();
				_handler = new (handler, autocompleters);
			}

			public Task HandleAsync(Interaction interaction) =>
				HandleByType(interaction, _handler);
		}
		private class SubTree: ITree {
			private readonly ConcurrentDictionary<string, GroupNode> _groupTable;
			private readonly ConcurrentDictionary<string, Handler> _leafTable;

			public SubTree(List<GroupNode> groups, List<LeafNode> leaves) {
				// Populate lookup tables.
				_groupTable = new ();
				foreach (GroupNode group in groups)
					_groupTable.TryAdd(group.Group.Name, group);
				_leafTable = new ();
				foreach (LeafNode leaf in leaves)
					_leafTable.TryAdd(leaf.Command.Name, leaf.Handler);
			}

			public Task HandleAsync(Interaction interaction) {
				InteractionArg node = GetArgs(interaction)[0];

				// Check if the subcommand matches any immediate leaves.
				if (_leafTable.ContainsKey(node.Name))
					return HandleByType(interaction, node, _leafTable);

				// Check if the subcommand matches any subcommand groups.
				if (_groupTable.ContainsKey(node.Name)) {
					GroupNode group = _groupTable[node.Name];
					node = GetArgs(node)[0];
					if (group.LeafTable.ContainsKey(node.Name))
						return HandleByType(interaction, node, group.LeafTable);
				}

				// Throw exception if nothing matched.
				throw new ArgumentException("Unrecognized slash command.", nameof(interaction));
			}
		}


		// --------
		// Internal helper functions:
		// --------

		// `ITree` makes more sense as an interface instead of an abstract
		// class, so these have to be helper functions instead of members
		// of `ITree`.

		// Functions to automatically disambiguate which member of a `Handler`
		// should be invoked.
		private static Task HandleByType(Interaction interaction, Handler handler) =>
			interaction.Type switch {
				InteractionType.ApplicationCommand =>
					InvokeNodeHandlerFromRoot(interaction, handler),
				InteractionType.AutoComplete =>
					InvokeAutocompleterFromRoot(interaction, handler),
				_ => throw new ArgumentException("The command tree cannot handle that type of interaction.", nameof(interaction))
			};
		private static Task HandleByType(
			Interaction interaction,
			InteractionArg node,
			IReadOnlyDictionary<string, Handler> handlers
		) =>
			interaction.Type switch {
				InteractionType.ApplicationCommand =>
					InvokeNodeHandlerFromNode(interaction, node, handlers),
				InteractionType.AutoComplete =>
					InvokeAutocompleterFromNode(interaction, node, handlers),
				_ => throw new ArgumentException("The command tree cannot handle that type of interaction.", nameof(interaction))
			};

		// Functions to invoke disambiguated handlers with packaged data.
		// Invoking the autocompleter will throw if the focused field is
		// invalid.
		private static Task InvokeNodeHandlerFromRoot(Interaction interaction, Handler handler) {
			IList<InteractionArg> argList = GetArgs(interaction);
			IDictionary<string, object> argTable = UnpackArgs(argList);

			return handler.NodeHandler.Invoke(interaction, argTable);
		}
		private static Task InvokeAutocompleterFromRoot(Interaction interaction, Handler handler) {
			IList<InteractionArg> argList = GetArgs(interaction);
			IDictionary<string, object> argTable = UnpackArgs(argList);
			InteractionArg? arg = GetFocusedArg(argList);

			if (arg is null || !handler.Autocompleters.ContainsKey(arg.Name))
				throw new InvalidOperationException("No autocompleter for the given field was found.");
			return handler
				.Autocompleters[arg.Name]
				.Invoke(interaction, arg.Value, argTable);
		}
		private static Task InvokeNodeHandlerFromNode(
			Interaction interaction,
			InteractionArg node,
			IReadOnlyDictionary<string, Handler> handlers
		) {
			IList<InteractionArg> argList = GetArgs(node);
			IDictionary<string, object> argTable = UnpackArgs(argList);

			return handlers[node.Name]
				.NodeHandler
				.Invoke(interaction, argTable);
		}
		private static Task InvokeAutocompleterFromNode(
			Interaction interaction,
			InteractionArg node,
			IReadOnlyDictionary<string, Handler> handlers
		) {
			IList<InteractionArg> argList = GetArgs(node);
			IDictionary<string, object> argTable = UnpackArgs(argList);
			InteractionArg? arg = GetFocusedArg(argList);

			if (arg is null || !handlers[node.Name].Autocompleters.ContainsKey(arg.Name))
				throw new InvalidOperationException("No autocompleter for the given field was found.");
			return handlers[node.Name]
				.Autocompleters[arg.Name]
				.Invoke(interaction, arg.Value, argTable);
		}
	}
}
