using Module = Irene.Modules.Starboard;

namespace Irene.Commands;

class Starboard : CommandHandler {
	public const string
		Command_BestOf  = "best-of",
		Command_Block   = "block",
		Command_Unblock = "unblock",
		Command_Pin     = "pin",
		Command_Unpin   = "unpin";
	public const string
		Arg_Id      = "message-id",
		Arg_Channel = "channel";

	private static readonly IReadOnlyList<ChannelType> _channelsText =
		new List<ChannelType> {
			ChannelType.Text,
			ChannelType.PublicThread,
			ChannelType.PrivateThread,
			ChannelType.News,
			ChannelType.NewsThread,
		};
	private static readonly List<CommandOption> _optionsMessage = new () {
		new (
			Arg_Id,
			"The message ID to block.",
			ApplicationCommandOptionType.String,
			required: true
		),
		new (
			Arg_Channel,
			"The channel where the message was posted.",
			ApplicationCommandOptionType.Channel,
			required: true,
			channelTypes: _channelsText
		),
	};

	public Starboard(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		:lock: {Command.Mention(Command_Block)} `<{Arg_Id}> <{Arg_Channel}>` blocks a message from being pinned,
		:lock: {Command.Mention(Command_Unblock)} `<{Arg_Id}> <{Arg_Channel}>` allows a message to be pinned.
		:lock: {Command.Mention(Command_Pin)} `<{Arg_Id}> <{Arg_Channel}>` immediately pins a message,
		:lock: {Command.Mention(Command_Unpin)} `<{Arg_Id}> <{Arg_Channel}>` allows a message to be unpinned.
		    The list of blocked and force-pinned messages are mutually exclusive.
		    Adding to one list will also remove from the other.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_BestOf,
			"Manage messages in the best-of channel.",
			Permissions.ManageMessages
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				new (
					Command_Block,
					"Prevent a message from being pinned.",
					ApplicationCommandOptionType.SubCommand,
					options: _optionsMessage
				),
				new (BlockAsync)
			),
			new (
				new (
					Command_Unblock,
					"Allow a message to be pinned.",
					ApplicationCommandOptionType.SubCommand,
					options: _optionsMessage
				),
				new (UnblockAsync)
			),
			new (
				new (
					Command_Pin,
					"Immediately pin a message.",
					ApplicationCommandOptionType.SubCommand,
					options: _optionsMessage
				),
				new (PinAsync)
			),
			new (
				new (
					Command_Unpin,
					"Allow a message to be unpinned.",
					ApplicationCommandOptionType.SubCommand,
					options: _optionsMessage
				),
				new (UnpinAsync)
			),
		}
	);

	public async Task BlockAsync(Interaction interaction, IDictionary<string, object> args) {
		DiscordMessage? message = await ParseMessageAsync(interaction, args);
		if (message is null) {
			await RespondParseFailureAsync(interaction);
			return;
		}

		bool isRedundant = !await Module.Block(message);
		string response = isRedundant
			? ":no_entry_sign: That message was already blocked; no changes neccessary."
			: ":no_entry_sign: Blocked message from being pinned.";

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}

	public async Task UnblockAsync(Interaction interaction, IDictionary<string, object> args) {
		DiscordMessage? message = await ParseMessageAsync(interaction, args);
		if (message is null) {
			await RespondParseFailureAsync(interaction);
			return;
		}

		bool isRedundant = !await Module.Block(message, false);
		string response = isRedundant
			? ":page_with_curl: That message was already unblocked; no changes neccessary."
			: ":page_with_curl: Allowing message to be pinned.";

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}

	public async Task PinAsync(Interaction interaction, IDictionary<string, object> args) {
		DiscordMessage? message = await ParseMessageAsync(interaction, args);
		if (message is null) {
			await RespondParseFailureAsync(interaction);
			return;
		}

		bool isRedundant = !await Module.Force(message);
		string response = isRedundant
			? ":pushpin: That message was already pinned; no changes neccessary."
			: ":pushpin: Pinning message.";

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}

	public async Task UnpinAsync(Interaction interaction, IDictionary<string, object> args) {
		DiscordMessage? message = await ParseMessageAsync(interaction, args);
		if (message is null) {
			await RespondParseFailureAsync(interaction);
			return;
		}

		bool isRedundant = !await Module.Force(message, false);
		string response = isRedundant
			? ":page_with_curl: That message could already be unpinned; no changes neccessary."
			: ":page_with_curl: Message can now be unpinned.";

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}

	// The same logic can be used to extract the message object every
	// time, since parameters and names are all shared.
	private static async Task<DiscordMessage?> ParseMessageAsync(Interaction interaction, IDictionary<string, object> args) {
		ulong id = ulong.Parse((string)args[Arg_Id]);
		DiscordChannel? channel =
			interaction.ResolveChannel((ulong)args[Arg_Channel]);

		return (channel is null)
			? null
			: await channel.GetMessageAsync(id);
	}
	private static async Task RespondParseFailureAsync(Interaction interaction) {
		Log.Warning("Failed to parse message object.");
		string response = "Could not parse channel ID.";
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}
}

class StarboardContext : CommandHandler {
	public const string
		Command_Pin = "Pin to #best-of";

	public StarboardContext(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		:lock: `> {Command_Pin}` immediately pins a message.
		    If this message was previously blocked, it will be unblocked.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Pin,
			"",
			new List<CommandOption>(),
			Permissions.ManageMessages
		),
		ApplicationCommandType.MessageContextMenu,
		PinAsync
	);

	public async Task PinAsync(Interaction interaction, IDictionary<string, object> args) {
		DiscordMessage? message = interaction.TargetMessage;
		if (message is null) {
			Log.Error("No target message found for context menu command.");
			return;
		}

		bool isRedundant = !await Module.Force(message);
		string response = isRedundant
			? ":pushpin: That message was already pinned; no changes neccessary."
			: ":pushpin: Successfully pinned message!";

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}
}
