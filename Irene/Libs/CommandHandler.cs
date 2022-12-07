namespace Irene;

using static Irene.Interaction;

using DiscordArg = DiscordInteractionDataOption;

// Usage:
// To implement an application command (slash commands or context menu
// commands), inherit from this class, and override the two mandatory
// abstract members (`HelpText` and `CreateTree()`).
// To implement both slash commands and context menu commands, derive
// two separate classes, one for each. (They can be in the same file.)
abstract class CommandHandler {
	// The possible results of handling an interaction.
	public enum ResultType {
		Success,
		NoPermissions, Exception,
	}

	// Syntax sugar for accessing rank emojis.
	public static DiscordEmoji RankIcon(AccessLevel level) =>
		Modules.Rank.Emoji(level);
	// Syntax sugar for helptext. Roughly aligns with rank emoji.
	protected const string _t = "\u2003\u2002"; // em space + en space

	public abstract string HelpText { get; }
	// `CreateTree()` should define the `CommandTree` of this command.
	public abstract CommandTree CreateTree();

	public CommandTree Tree { get; }
	public DiscordCommand Command => Tree.Command;
	public string Mention(string display) => Command.Mention(display);

	public CommandHandler() {
		Tree = CreateTree();
	}
	// Updating a registered command should only be done to populate the
	// command's ID once it has been registered with Discord.
	// This method will check that the command name matches, and will
	// throw an exception if otherwise.
	public void UpdateRegisteredCommand(DiscordCommand command) {
		if (command.Name != Command.Name)
			throw new ArgumentException("New command name doesn't match original.", nameof(command));
		Tree.UpdateRegisteredCommand(command);
	}
	public Task<ResultType> HandleAsync(Interaction interaction) =>
		Tree.HandleAsync(interaction);


	public class CommandTree {
		// --------
		// Record definitions (used in constructors, etc.):
		// --------

		// Commands (root-levelNeeded) and subcommands share a set of fields,
		// and subcommand groups have their own.
		public record class GroupArgs(
			string Name,
			string Description
		);
		public record class LeafArgs(
			string Name,
			string Description,
			AccessLevel AccessLevel,
			IList<DiscordCommandOption> Options
		);
		// Handlers are associated with leaf nodes (or the root command).
		public record class Handler {
			public Responder Responder { get; }
			public AutocompleterTable Autocompleters { get; }

			public Handler(
				Responder responder,
				AutocompleterTable? autocompleters=null
			) {
				Responder = responder;
				Autocompleters = autocompleters
					?? new Dictionary<string, Autocompleter>();
			}
		}


		// --------
		// Properties/fields, constructors, and methods:
		// --------

		// `ITree` handles most logic instead of `CommandTree`.
		private readonly AbstractTree _tree;
		public DiscordCommand Command { get; private set; }

		// Construct a tree that only has a single command.
		// Context menu commands should always use this constructor.
		public CommandTree(
			LeafArgs args,
			CommandType type,
			Responder responder,
			AutocompleterTable? autocompleters=null
		) {
			autocompleters ??= new Dictionary<string, Autocompleter>();
			_tree = new RootTree(
				args.AccessLevel,
				new (responder, autocompleters)
			);
			Command = new (
				args.Name,
				args.Description,
				args.Options,
				type: type
			);
		}
		// Construct a tree with subcommands / subcommand groups.
		public CommandTree(
			GroupArgs args,
			IList<GroupNode> groups,
			IList<LeafNode> leaves
		) {
			_tree = new SubTree(groups, leaves);

			// Collate all child nodes that are one levelNeeded down.
			List<DiscordCommandOption> optionList = new ();
			foreach (GroupNode group in groups)
				optionList.Add(group.Group);
			foreach (LeafNode leaf in leaves)
				optionList.Add(leaf.Command);
			Command = new (
				args.Name,
				args.Description,
				optionList,
				type: CommandType.SlashCommand
			);
			// Context menu commands always have one and only one leaf
			// node, so only slash commands will use this constructor.
		}

		public void UpdateRegisteredCommand(DiscordCommand command) =>
			Command = command;
		public async Task<ResultType> HandleAsync(Interaction interaction) {
			try {
				return await _tree.HandleAsync(interaction);
			} catch (IreneException e) {
				await interaction
					.RegisterAndRespondAsync(e.ResponseMessage, true);
				e.Log();
				return ResultType.Exception;
			}
		}


		// --------
		// Internal subtree (`AbstractTree`) implementation:
		// --------

		// The `GroupNode` and `LeafNode` records are used when defining
		// a CommandTree implemented via a SubTree.
		public record class GroupNode {
			public readonly DiscordCommandOption Group;
			public readonly IReadOnlyList<LeafNode> Leaves;
			public readonly IReadOnlyDictionary<string, LeafNode> LeafTable;

			public GroupNode(
				string name,
				string description,
				IList<LeafNode> leaves
			) {
				Leaves = new ReadOnlyCollection<LeafNode>(leaves);

				// Collate subcommands from child nodes.
				List<DiscordCommandOption> commandList = new ();
				foreach (LeafNode leaf in leaves)
					commandList.Add(leaf.Command);
				Group = new (
					name,
					description,
					ArgType.SubCommandGroup,
					options: commandList
				);

				// Populate lookup table.
				ConcurrentDictionary<string, LeafNode> leafTable = new ();
				foreach (LeafNode leaf in leaves)
					_ = leafTable.TryAdd(leaf.Command.Name, leaf);
				LeafTable = leafTable;
			}
		}
		public record class LeafNode {
			public readonly AccessLevel AccessLevel;
			public readonly DiscordCommandOption Command;
			public readonly Handler Handler;

			public LeafNode(
				AccessLevel accessLevel,
				DiscordCommandOption command,
				Handler handler
			) {
				AccessLevel = accessLevel;
				Command = command;
				Handler = handler;
			}
		}

		// A slash command can have either a single command (`RootTree`),
		// or it can have child nodes (`SubTree`).
		private abstract class AbstractTree {
			// The data needed from a derived `AbstractTree` class to
			// handle an interaction.
			protected record class LeafData(
				AccessLevel AccessLevel,
				Handler Handler,
				IList<DiscordArg> ArgList
			);

			// Return the `LeafNode` containing the correct `Handler`
			// for the given `Interaction`, and its unpacked args.
			protected abstract LeafData GetLeafData(Interaction interaction);

			// The main method which handles interactions.
			public async Task<ResultType> HandleAsync(Interaction interaction) {
				if (interaction.Type
					is not InteractionType.ApplicationCommand
					and not InteractionType.AutoComplete
				) {
					throw new ArgumentException("Can only dispatch Responders and Autocompleters.", nameof(interaction));
				}

				// Populate common fields.
				(AccessLevel level, Handler handler, IList<DiscordArg> argList) =
					GetLeafData(interaction);
				ParsedArgs argTable = UnpackArgs(argList);

				// If the interaction is an application command, check
				// for permissions to access and catch any exceptions.
				if (interaction.Type is InteractionType.ApplicationCommand) {
					bool hasAccess = await HasAccessAsync(interaction, level);
					if (!hasAccess) {
						await RespondNoAccessAsync(interaction, level);
						return ResultType.NoPermissions;
					}
					await handler.Responder.Invoke(interaction, argTable);
					return ResultType.Success;
				}

				// Autocompleters can throw and we won't respond; the
				// user simply sees an empty list (no error).
				if (interaction.Type is InteractionType.AutoComplete) {
					DiscordArg? arg = GetFocusedArg(argList);
					if (arg is null || !handler.Autocompleters.ContainsKey(arg.Name))
						throw new InvalidOperationException("No autocompleter for the given field was found.");

					await handler.Autocompleters[arg.Name]
						.Invoke(interaction, arg.Value, argTable);
					return ResultType.Success;
				}

				// The very first check should ensure the following two
				// clauses will always handle whatever interaction is
				// passed in. Execution should never reach here.
				throw new ImpossibleException();
			}

			// Helper method for `HandleAsync()` to determine if a user
			// has sufficient access levelNeeded for a command interaction.
			private static async Task<bool> HasAccessAsync(Interaction interaction, AccessLevel levelNeeded) {
				if (levelNeeded == AccessLevel.None)
					return true;

				DiscordMember? member = await interaction.User.ToMember();
				// `node.AccessLevel` must be > `None`, so if there is
				// no rank data, the user cannot have access.
				if (member is null)
					return false;

				AccessLevel levelMember = Modules.Rank.GetRank(member);
				return levelMember >= levelNeeded;
			}

			// Helper method for `HandleAsync()` to respond to a user
			// if they didn't have permission for a command interaction.
			private static async Task RespondNoAccessAsync(Interaction interaction, AccessLevel levelNeeded) {
				string emojiFace = ":face_with_open_eyes_and_hand_over_mouth:";
				string emojiRank = Modules.Rank.Emoji(levelNeeded);
				string commandHelp = Dispatcher.Table[Commands.Help.CommandHelp]
					.Mention(Commands.Help.CommandHelp);
				string response =
					$"""
					{emojiFace} Sorry, that command requires {emojiRank}{levelNeeded} permissions to use.
					(You can check which commands you can use with {commandHelp}.)
					""";
				await interaction.RegisterAndRespondAsync(response, true);
			}
		}

		private class RootTree : AbstractTree {
			private readonly AccessLevel _accessLevel;
			private readonly Handler _handler;

			public RootTree(AccessLevel accessLevel, Handler handler) {
				_accessLevel = accessLevel;
				_handler = handler;
			}

			protected override LeafData GetLeafData(Interaction interaction) =>
				new (
					_accessLevel,
					_handler,
					GetArgs(interaction)
				);
		}
		private class SubTree : AbstractTree {
			private readonly ConcurrentDictionary<string, GroupNode> _groupTable;
			private readonly ConcurrentDictionary<string, LeafNode> _leafTable;

			public SubTree(IList<GroupNode> groups, IList<LeafNode> leaves) {
				// Populate lookup tables.
				_groupTable = new ();
				foreach (GroupNode group in groups)
					_groupTable.TryAdd(group.Group.Name, group);
				_leafTable = new ();
				foreach (LeafNode leaf in leaves)
					_leafTable.TryAdd(leaf.Command.Name, leaf);
			}

			protected override LeafData GetLeafData(Interaction interaction) {
				DiscordArg node = GetArgs(interaction)[0];

				// Check if the subcommand matches any immediate leaves.
				if (_leafTable.ContainsKey(node.Name)) {
					return new (
						_leafTable[node.Name].AccessLevel,
						_leafTable[node.Name].Handler,
						GetArgs(node)
					);
				}

				// Check if the subcommand matches any subcommand groups.
				if (_groupTable.ContainsKey(node.Name)) {
					GroupNode group = _groupTable[node.Name];
					node = GetArgs(node)[0];
					if (group.LeafTable.ContainsKey(node.Name)) {
						return new (
							group.LeafTable[node.Name].AccessLevel,
							group.LeafTable[node.Name].Handler,
							GetArgs(node)
						);
					}
				}

				// Throw exception if nothing matched.
				throw new UnknownCommandException(node.Name);
			}
		}
	}
}
