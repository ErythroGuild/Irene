using Irene.Interactables;

using Module = Irene.Modules.IreneStatus;

namespace Irene.Commands;

class IreneStatus : CommandHandler {
	public const string
		Command_Status = "irene-status",
		Command_List   = "list",
		Command_Set    = "set",
		Command_Random = "random",
		Arg_Type   = "type",
		Arg_Status = "status";
	public const string
		Label_Playing   = "Playing",
		Label_Listening = "Listening to",
		Label_Watching  = "Watching",
		Label_Competing = "Competing in";
	public const string
		Option_Playing   = "Playing",
		Option_Listening = "Listening to",
		Option_Watching  = "Watching",
		Option_Competing = "Competing in";

	public IreneStatus(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention($"{Command_Status} {Command_List}")} lists all saved statuses.
		:lock: {Command.Mention($"{Command_Status} {Command_Set}")} `<{Arg_Type}> <{Arg_Status}>` sets and saves a new status,
		:lock: {Command.Mention($"{Command_Status} {Command_Random}")} randomly picks a saved status.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Status,
			"Change Irene's activity status.",
			Permissions.ManageGuild
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				new (
					Command_List,
					"List all saved statuses.",
					ApplicationCommandOptionType.SubCommand
				),
				new (ListAsync)
			),
			new (
				new (
					Command_Set,
					"Set and save a new status.",
					ApplicationCommandOptionType.SubCommand,
					options: new List<CommandOption> {
						new (
							Arg_Type,
							"The type of the status.",
							ApplicationCommandOptionType.String,
							required: true,
							new List<CommandOptionEnum> {
								new (Label_Playing  , Option_Playing  ),
								new (Label_Listening, Option_Listening),
								new (Label_Watching , Option_Watching ),
								new (Label_Competing, Option_Competing),
							}
						),
						new (
							Arg_Status,
							"The text of the status.",
							ApplicationCommandOptionType.String,
							required: true
						),
					}
				),
				new (SetAsync)
			),
			new (
				new (
					Command_Random,
					"Randomly pick a saved status.",
					ApplicationCommandOptionType.SubCommand
				),
				new (RandomizeAsync)
			),
		}
	);

	public async Task ListAsync(Interaction interaction, IDictionary<string, object> args) {
		IList<Module.Status> statuses = await Module.GetAll();

		// Special case if no saved statuses are available.
		if (statuses.Count == 0) {
			string responseEmpty = $"""
				No statuses saved. :duck:
				(add some with {Command.Mention($"{Command_Status} {Command_Set}")}?)
				""";
			interaction.RegisterFinalResponse();
			await interaction.RespondCommandAsync(responseEmpty);
			interaction.SetResponseSummary(responseEmpty);
			return;
		}

		// Convert all statuses to strings for display.
		List<string> lines = new ();
		foreach (Module.Status status in statuses)
			lines.Add(status.AsStatusText());
		// Should already be sorted.

		// Respond with Pages interactable.
		MessagePromise messagePromise = new ();
		DiscordMessageBuilder response = Pages.Create(
			interaction,
			messagePromise.Task,
			lines,
			pageSize: 12
		);

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary($"<List of {lines.Count} available statuses sent.>");

		DiscordMessage message = await interaction.GetResponseAsync();
		messagePromise.SetResult(message);
		return;
	}

	public async Task SetAsync(Interaction interaction, IDictionary<string, object> args) {
		ActivityType type = (string)args[Arg_Type] switch {
			Option_Playing   => ActivityType.Playing    ,
			Option_Listening => ActivityType.ListeningTo,
			Option_Watching  => ActivityType.Watching   ,
			Option_Competing => ActivityType.Competing  ,
			_ => throw new ArgumentException("Unknown status type.", nameof(args)),
		};
		string status = (string)args[Arg_Status];
		DateTimeOffset endTime = DateTimeOffset.UtcNow + TimeSpan.FromDays(1);
		await Module.SetAndAdd(new (type, status), endTime);

		string response = ":astronaut: Status updated! (and added to pool)";
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}

	public async Task RandomizeAsync(Interaction interaction, IDictionary<string, object> args) {
		bool didSet = await Module.SetRandom();
		string response = !didSet
			? $"""
				No saved statuses available to choose from. :duck:
				(add some with {Command.Mention($"{Command_Status} {Command_Set}")}?)
				"""
			: "Random status set! :astronaut:";
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}
}
