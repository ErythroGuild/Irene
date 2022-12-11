namespace Irene.Commands;

using Module = Modules.Farm;

class Farm : CommandHandler {
	public const string
		CommandFarm = "farm",
		ArgMaterial = "material";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}{Mention(CommandFarm)} `<{ArgMaterial}>` finds farming routes for the material.
		{_t}Data adapted from `wow-professions.com`.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandFarm,
			"Find a farming route.",
			AccessLevel.Guest,
			new List<DiscordCommandOption> { new (
				ArgMaterial,
				"The material to find a route for.",
				ArgType.String,
				required: true,
				autocomplete: true
			) }
		),
		CommandType.SlashCommand,
		RespondAsync,
		new Dictionary<string, Completer> {
			[ArgMaterial] = Module.Completer,
		}
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		string query = (string)args[ArgMaterial];
		Module.Material? material = Module.ParseMaterial(query);

		// Send error message if no matching material was found.
		if (material is null) {
			string error = $"Sorry, couldn't find any routes to farm `{query}`.";
			await interaction.RegisterAndRespondAsync(error, true);
			return;
		}

		// The module method will handle all responding, since it also
		// needs to register the sent message for component interactions.
		await Module.RespondAsync(interaction, material);
	}
}
