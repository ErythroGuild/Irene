using StarboardObj = Irene.Modules.Starboard;

namespace Irene.Commands;

class Starboard : AbstractCommand {
	private const string
		_commandBlock = "block",
		_commandUnblock = "unblock",
		_commandPinBestOf = "Pin #best-of";

	public override List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@"`/best-of block <message-id> <channel>` blocks a message from being pinned.",
			@"`/best-of unblock <message-id> <channel>` allows a message to be pinned again.",
			"When a message is blocked, existing pins are removed.",
			"Unblocking will only create a pin if the post meets current requirements."
		} ) };
	}

	public override List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"best-of",
				"Manage messages on the best-of channel.",
				options: new List<CommandOption> {
					new (
						_commandBlock,
						"Prevent a message from being pinned.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								"message-id",
								"The message ID to block.",
								ApplicationCommandOptionType.String,
								required: true
							),
							new (
								"channel",
								"The channel the message is in.",
								ApplicationCommandOptionType.Channel,
								required: true
							),
						}
					),
					new (
						_commandUnblock,
						"Allow a message to be pinned again.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								"message-id",
								"The message ID to block.",
								ApplicationCommandOptionType.String,
								required: true
							),
							new (
								"channel",
								"The channel the message is in.",
								ApplicationCommandOptionType.Channel,
								required: true
							),
						}
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferEphemeralAsync, RunAsync )
		};
	}

	public override List<InteractionCommand> MessageCommands { get =>
		new () {
			new ( new (
				_commandPinBestOf,
				"",	// description field must be "" instead of null
				defaultPermission: true,
				type: ApplicationCommandType.MessageContextMenu
			), Command.DeferEphemeralAsync, PinBestOfAsync )
		};
	}

	public static async Task RunAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string command = args[0].Name;

		// Check for permissions.
		bool doContinue;
		doContinue = await
			interaction.CheckAccessAsync(false, AccessLevel.Officer);
		if (!doContinue)
			return;

		// Use subcommand name to determine if blocking or unblocking.
		bool doBlock = command switch {
			_commandBlock   => true,
			_commandUnblock => false,
			_ => throw new ArgumentException("Unrecognized subcommand.", nameof(interaction)),
		};

		// Parse message ID.
		string messageId_str = (string)args[0].GetArgs()[0].Value;
		ulong? messageId = null;
		try {
			messageId = ulong.Parse(messageId_str);
		} catch (FormatException) { }
		if (messageId is null) {
			await Command.SubmitResponseAsync(
				interaction,
				$"Could not parse message ID `{messageId_str}`.\nNo changes made.",
				"Not updating blacklist: invalid message ID.",
				LogLevel.Information,
				"specified message ID: {Arg}".AsLazy(),
				messageId_str
			);
			return;
		}

		// Parse channel and fetch message object.
		DiscordChannel channel =
			interaction.Interaction.GetTargetChannel();
		DiscordMessage? message = null;
		try {
			message = await channel.GetMessageAsync(messageId.Value);
		} catch (Exception) { }
		if (message is null) {
			await Command.SubmitResponseAsync(
				interaction,
				$"Could not fetch a message with the ID: `{messageId_str}` from {channel.Mention}.\nNo changes made.",
				"Not updating blacklist: could not fetch message object.",
				LogLevel.Information,
				"channel ID: {ChannelId}, message ID: {MessageId}".AsLazy(),
				channel.Id, messageId_str
			);
			return;
		}

		// Update blacklist.
		bool didUpdate =
			StarboardObj.SetBlacklist(message, doBlock);
		string response = (doBlock, didUpdate) switch {
			(true , true ) => $"Successfully added `{messageId_str}` to blocked messages.",
			(true , false) => $"`{messageId_str}` already added to blocked messages; no changes made.",
			(false, true ) => $"Successfully removed `{messageId_str}` from blocked messages.",
			(false, false) => $"`{messageId_str}` not currently blocked; no changes made.",
		};

		// Update pins.
		if (doBlock && didUpdate) {
			bool hasPin = (await StarboardObj.FetchPinAsync(message))
				is not null;
			if (hasPin) {
				await StarboardObj.RemovePinAsync(message);
				response += "\nRemoved existing pin.";
			}
		}
		if (!doBlock && didUpdate) {
			bool doPin = await StarboardObj.DoPin(message) ?? false;
			if (doPin) {
				await StarboardObj.UpdatePinAsync(message);
				response += "\nPinned message (it meets requirements).";
			}
		}

		// Submit response.
		await Command.SubmitResponseAsync(
			interaction,
			response,
			"Updating blocked messages.",
			LogLevel.Debug,
			"Blocked: {DoBlock}, ID: {MessageId}".AsLazy(),
			doBlock, messageId
		);
	}

	public static async Task PinBestOfAsync(TimedInteraction interaction) {
		// Check for permissions.
		bool doContinue = await
			interaction.CheckAccessAsync(false, AccessLevel.Officer);
		if (!doContinue)
			return;
		Log.Information("  Pinning message to #best-of.");

		// Fetch the first resolved message.
		DiscordMessage message =
			interaction.Interaction.GetTargetMessage();

		// Check that message is not on the blacklist.
		string response;
		bool isBlocked = StarboardObj.IsBlacklisted(message);
		if (isBlocked) {
			response =
				":no_entry_sign: That message has been blocked from #best-of.\n" +
				"If you still want to pin it, unblock it first with `/best-of unblock`.\n" +
				"See `/help best-of` for more details.";
			await interaction.Interaction.UpdateMessageAsync(response);
			Log.Information("    Message not pinned--blocked with blacklist.");
			interaction.Timer.LogMsecDebug("    Processed in {Time} msec.");
			return;
		}

		// Proceed to pin message.
		await StarboardObj.UpdatePinAsync(message);
		response = "Successfully pinned message.";
		await interaction.Interaction.UpdateMessageAsync(response);
		interaction.Timer.LogMsecDebug("  Pinned in {Time} msec.");
	}
}
