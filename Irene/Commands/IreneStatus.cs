using Irene.Components;

using StatusModule = Irene.Modules.IreneStatus;

namespace Irene.Commands;

class IreneStatus : AbstractCommand {
	private static readonly ReadOnlyDictionary<string, ActivityType> _statusTypes =
		new (new ConcurrentDictionary<string, ActivityType>() {
			[_optionPlaying  ] = ActivityType.Playing    ,
			[_optionListening] = ActivityType.ListeningTo,
			[_optionWatching ] = ActivityType.Watching   ,
			[_optionCompeting] = ActivityType.Competing  ,
		} );

	private const string
		_commandRandom = "random",
		_commandSet = "set",
		_commandList = "list";
	private const string
		_optionPlaying   = "playing"     ,
		_optionListening = "listening-to",
		_optionWatching  = "watching"    ,
		_optionCompeting = "competing-in";

	public override List<string> HelpPages =>
		new () { new List<string> {
			@":lock: `/irene-status random` chooses a random status,",
			@":lock: `/irene-status set <type> <status>` sets and saves a new status.",
			@"`/irene-status list` lists all possible statuses.",
			"Statuses also randomly rotate every so often.",
		}.ToLines() };

	public override List<InteractionCommand> SlashCommands =>
		new () {
			new (new (
				"irene-status",
				"Set Irene's activity status.",
				options: new List<CommandOption> {
					new (
						_commandRandom,
						"Choose a random status",
						ApplicationCommandOptionType.SubCommand
					),
					new (
						_commandSet,
						"Set and save a new status.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								"type",
								"The type of the status.",
								ApplicationCommandOptionType.String,
								required: true,
								new List<CommandOptionEnum> {
									new ("Playing", _optionPlaying),
									new ("Listening to", _optionListening),
									new ("Watching", _optionWatching),
									new ("Competing in", _optionCompeting),
								}
							),
							new (
								"status",
								"The text of the status.",
								ApplicationCommandOptionType.String,
								required: true
							),
						}
					),
					new (
						_commandList,
						"List all possible statuses.",
						ApplicationCommandOptionType.SubCommand
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferEphemeralAsync, RunAsync )
		};

	public static async Task RunAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string command = args[0].Name;

		// Check for permissions.
		bool doContinue;
		switch (command) {
		case _commandRandom:
		case _commandSet:
			doContinue = await
				interaction.CheckAccessAsync(false, AccessLevel.Officer);
			if (!doContinue)
				return;
			break;
		}

		// Dispatch the correct subcommand.
		InteractionHandler handler = command switch {
			_commandRandom => RandomAsync,
			_commandSet    => SetAsync   ,
			_commandList   => ListAsync  ,
			_ => throw new ArgumentException("Unrecognized subcommand.", nameof(interaction)),
		};
		await handler.Invoke(interaction);
	}

	private static async Task RandomAsync(TimedInteraction interaction) {
		bool didSet = await StatusModule.SetRandom();

		// Indicate if nothing was set.
		if (!didSet) {
			await Command.SubmitResponseAsync(
				interaction,
				"No statuses available to choose from.\nTry :lock: `/irene-status set`?",
				"No statuses available to set.",
				LogLevel.Debug,
				"Status unchanged.".AsLazy()
			);
			return;
		}

		// Send success response.
		await Command.SubmitResponseAsync(
			interaction,
			"Random status set! :astronaut:",
			"Status set to a random choice.",
			LogLevel.Debug,
			"Status randomly selected.".AsLazy()
		);
	}

	private static async Task SetAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();

		// Parse arguments.
		string type_string = (string)args[0].Value;
		ActivityType type = _statusTypes[type_string];
		string content = (string)args[1].Value;

		// Construct status object and dispatch.
		DiscordActivity status = new (content, type);
		await StatusModule.AddAndSet(status);

		// Respond.
		await Command.SubmitResponseAsync(
			interaction,
			"Status updated! (and added to pool) :astronaut:",
			"Status updated and added to pool.",
			LogLevel.Debug,
			"Status updated and saved: {Status}".AsLazy(),
			status.AsStatusText()
		);
	}

	private static async Task ListAsync(TimedInteraction interaction) {
		// Fetch all statuses.
		HashSet<DiscordActivity> statuses =
			new (StatusModule.GetAll());

		// Special case if no statuses available.
		if (statuses.Count == 0) {
			await Command.SubmitResponseAsync(
				interaction,
				"No statuses available.\nTry :lock: `/irene-status set`?",
				"No statuses found to send.",
				LogLevel.Debug,
				"No list sent.".AsLazy()
			);
			return;
		}

		// Format and sort all results.
		List<string> strings = new ();
		foreach (DiscordActivity status in statuses)
			strings.Add(status.AsStatusText());
		strings.Sort();

		// Create Pages object for response.
		DiscordMessage message;
		MessagePromise message_promise = new ();
		DiscordWebhookBuilder response = Pages.Create(
			interaction.Interaction,
			message_promise.Task,
			strings,
			pageSize: 12
		);

		// Send List of statuses, and complete promise.
		message = await Command.SubmitResponseAsync(
			interaction,
			response,
			"Sending list of statuses.",
			LogLevel.Debug,
			"List of possible statuses sent: {Count}".AsLazy(),
			strings.Count
		);
		message_promise.SetResult(message);
	}
}
