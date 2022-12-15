namespace Irene.Commands;

using Irene.Interactables;

using Module = Modules.IreneStatus;

class IreneStatus : CommandHandler {
	public const string
		CommandStatus = "irene-status",
		CommandList   = "list",
		CommandSet    = "set",
		CommandRandom = "random",
		ArgType   = "type",
		ArgStatus = "status";
	public const string
		LabelPlaying   = "Playing",
		LabelListening = "Listening to",
		LabelWatching  = "Watching",
		LabelCompeting = "Competing in";
	public const string
		OptionPlaying   = "Playing",
		OptionListening = "Listening to",
		OptionWatching  = "Watching",
		OptionCompeting = "Competing in";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Member)}{Mention($"{CommandStatus} {CommandList}")} lists all saved statuses.
		{RankIcon(AccessLevel.Officer)}{Mention($"{CommandStatus} {CommandSet}")} `<{ArgType}> <{ArgStatus}>` sets and saves a new status,
		{RankIcon(AccessLevel.Officer)}{Mention($"{CommandStatus} {CommandRandom}")} randomly picks a saved status.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandStatus,
			"Change Irene's activity status."
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				AccessLevel.Member,
				new (
					CommandList,
					"List all saved statuses.",
					ApplicationCommandOptionType.SubCommand
				),
				new (ListAsync)
			),
			new (
				AccessLevel.Officer,
				new (
					CommandSet,
					"Set and save a new status.",
					ApplicationCommandOptionType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							ArgType,
							"The type of the status.",
							ApplicationCommandOptionType.String,
							required: true,
							new List<DiscordCommandOptionEnum> {
								new (LabelPlaying  , OptionPlaying  ),
								new (LabelListening, OptionListening),
								new (LabelWatching , OptionWatching ),
								new (LabelCompeting, OptionCompeting),
							}
						),
						new (
							ArgStatus,
							"The text of the status.",
							ApplicationCommandOptionType.String,
							required: true
						),
					}
				),
				new (SetAsync)
			),
			new (
				AccessLevel.Officer,
				new (
					CommandRandom,
					"Randomly pick a saved status.",
					ApplicationCommandOptionType.SubCommand
				),
				new (RandomizeAsync)
			),
		}
	);

	public async Task ListAsync(Interaction interaction, ParsedArgs args) {
		IList<Module.Status> statuses = await Module.GetAll();

		// Special case if no saved statuses are available.
		if (statuses.Count == 0) {
			string responseEmpty =
				$"""
				No statuses saved. :duck:
				(add some with {Mention($"{CommandStatus} {CommandSet}")}?)
				""";
			await interaction.RegisterAndRespondAsync(responseEmpty);
			return;
		}

		// Convert all statuses to strings for display.
		List<string> lines = new ();
		foreach (Module.Status status in statuses)
			lines.Add(status.AsStatusText());
		// Should already be sorted.

		// Respond with StringPages interactable.
		MessagePromise messagePromise = new ();
		DiscordMessageBuilder response = StringPages.Create(
			interaction,
			messagePromise.Task,
			lines,
			new StringPages.Options { PageSize = 12 }
		);

		string summary = $"<List of {lines.Count} available statuses sent.>";
		await interaction.RegisterAndRespondAsync(response, summary, true);

		DiscordMessage message = await interaction.GetResponseAsync();
		messagePromise.SetResult(message);
		return;
	}

	public async Task SetAsync(Interaction interaction, ParsedArgs args) {
		ActivityType type = (string)args[ArgType] switch {
			OptionPlaying   => ActivityType.Playing    ,
			OptionListening => ActivityType.ListeningTo,
			OptionWatching  => ActivityType.Watching   ,
			OptionCompeting => ActivityType.Competing  ,
			_ => throw new ImpossibleArgException(ArgType, (string)args[ArgType]),
		};
		string status = (string)args[ArgStatus];
		DateTimeOffset endTime = DateTimeOffset.UtcNow + TimeSpan.FromDays(1);
		await Module.SetAndAdd(new (type, status), endTime);

		string response = ":astronaut: Status updated! (and added to pool)";
		await interaction.RegisterAndRespondAsync(response, true);
	}

	public async Task RandomizeAsync(Interaction interaction, ParsedArgs args) {
		bool didSet = await Module.SetRandom();
		string response = !didSet
			? $"""
				No saved statuses available to choose from. :duck:
				(add some with {Command.Mention($"{CommandStatus} {CommandSet}")}?)
				"""
			: "Random status set! :astronaut:";
		await interaction.RegisterAndRespondAsync(response, true);
	}
}
