namespace Irene.Commands;

using Module = Modules.Starboard;

class Starboard : CommandHandler {
	public const string
		CommandBestOf  = "best-of",
		CommandBlock   = "block",
		CommandUnblock = "unblock",
		CommandPin     = "pin",
		CommandUnpin   = "unpin",
		ArgId      = "id",
		ArgChannel = "channel";

	private static readonly IReadOnlyList<ChannelType> _channelsText =
		new List<ChannelType> {
			ChannelType.Text,
			ChannelType.PublicThread,
			ChannelType.PrivateThread,
			ChannelType.News,
			ChannelType.NewsThread,
		};
	private static readonly List<DiscordCommandOption> _optionsMessage = new () {
		new (
			ArgId,
			"The message's ID.",
			ArgType.String,
			required: true
		),
		new (
			ArgChannel,
			"The channel where the message was posted.",
			ArgType.Channel,
			required: true,
			channelTypes: _channelsText
		),
	};

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Officer)}{Mention($"{CommandBestOf} {CommandBlock}")} `<{ArgId}> <{ArgChannel}>` blocks a message from being pinned,
		{RankIcon(AccessLevel.Officer)}{Mention($"{CommandBestOf} {CommandUnblock}")} `<{ArgId}> <{ArgChannel}>` allows a message to be pinned.
		{RankIcon(AccessLevel.Officer)}{Mention($"{CommandBestOf} {CommandPin}")} `<{ArgId}> <{ArgChannel}>` immediately pins a message,
		{RankIcon(AccessLevel.Officer)}{Mention($"{CommandBestOf} {CommandUnpin}")} `<{ArgId}> <{ArgChannel}>` allows a message to be unpinned.
		{_t}Blocked and pinned messages are mutually exclusive.
		{_t}Adding to one list will remove from the other.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandBestOf,
			"Manage messages in the best-of channel."
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				AccessLevel.Officer,
				new (
					CommandBlock,
					"Prevent a message from being pinned.",
					ArgType.SubCommand,
					options: _optionsMessage
				),
				new (BlockAsync)
			),
			new (
				AccessLevel.Officer,
				new (
					CommandUnblock,
					"Allow a message to be pinned.",
					ArgType.SubCommand,
					options: _optionsMessage
				),
				new (UnblockAsync)
			),
			new (
				AccessLevel.Officer,
				new (
					CommandPin,
					"Immediately pin a message.",
					ArgType.SubCommand,
					options: _optionsMessage
				),
				new (PinAsync)
			),
			new (
				AccessLevel.Officer,
				new (
					CommandUnpin,
					"Allow a message to be unpinned.",
					ArgType.SubCommand,
					options: _optionsMessage
				),
				new (UnpinAsync)
			),
		}
	);

	public async Task BlockAsync(Interaction interaction, ParsedArgs args) {
		DiscordMessage? message = await ParseMessageAsync(interaction, args);
		if (message is null) {
			await RespondParseFailureAsync(interaction);
			return;
		}

		bool isRedundant = !await Module.Block(message);
		string response = isRedundant
			? ":no_entry_sign: That message was already blocked; no changes neccessary."
			: ":no_entry_sign: Blocked message from being pinned.";
		await interaction.RegisterAndRespondAsync(response, true);
	}

	public async Task UnblockAsync(Interaction interaction, ParsedArgs args) {
		DiscordMessage? message = await ParseMessageAsync(interaction, args);
		if (message is null) {
			await RespondParseFailureAsync(interaction);
			return;
		}

		bool isRedundant = !await Module.Block(message, false);
		string response = isRedundant
			? ":page_with_curl: That message was already unblocked; no changes neccessary."
			: ":page_with_curl: Allowing message to be pinned.";
		await interaction.RegisterAndRespondAsync(response, true);
	}

	public async Task PinAsync(Interaction interaction, ParsedArgs args) {
		DiscordMessage? message = await ParseMessageAsync(interaction, args);
		if (message is null) {
			await RespondParseFailureAsync(interaction);
			return;
		}

		bool isRedundant = !await Module.Force(message);
		string response = isRedundant
			? ":pushpin: That message was already pinned; no changes neccessary."
			: ":pushpin: Pinning message.";
		await interaction.RegisterAndRespondAsync(response, true);
	}

	public async Task UnpinAsync(Interaction interaction, ParsedArgs args) {
		DiscordMessage? message = await ParseMessageAsync(interaction, args);
		if (message is null) {
			await RespondParseFailureAsync(interaction);
			return;
		}

		bool isRedundant = !await Module.Force(message, false);
		string response = isRedundant
			? ":page_with_curl: That message could already be unpinned; no changes neccessary."
			: ":page_with_curl: Message can now be unpinned.";
		await interaction.RegisterAndRespondAsync(response, true);
	}

	// The same logic can be used to extract the message object every
	// time, since parameters and names are all shared.
	private static async Task<DiscordMessage?> ParseMessageAsync(Interaction interaction, ParsedArgs args) {
		ulong id = ulong.Parse((string)args[ArgId]);
		DiscordChannel? channel =
			interaction.ResolveChannel((ulong)args[ArgChannel]);

		return (channel is null)
			? null
			: await channel.GetMessageAsync(id);
	}
	private static async Task RespondParseFailureAsync(Interaction interaction) {
		Log.Warning("Failed to parse message object.");
		string response = "Could not parse channel ID.";
		await interaction.RegisterAndRespondAsync(response, true);
	}
}

class StarboardContext : CommandHandler {
	public const string CommandPin = "Pin to #best-of";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Officer)} `> {CommandPin}` immediately pins a message.
		    If this message was previously blocked, it will be unblocked.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandPin,
			"",
			AccessLevel.Officer,
			new List<DiscordCommandOption>()
		),
		CommandType.MessageContextMenu,
		PinAsync
	);

	public async Task PinAsync(Interaction interaction, ParsedArgs args) {
		DiscordMessage? message = interaction.TargetMessage;
		if (message is null) {
			Log.Error("No target message found for context menu command.");
			throw new ImpossibleArgException("Target message", "N/A");
		}

		bool isRedundant = !await Module.Force(message);
		string response = isRedundant
			? ":pushpin: That message was already pinned; no changes neccessary."
			: ":pushpin: Successfully pinned message!";
		await interaction.RegisterAndRespondAsync(response, true);
	}
}
