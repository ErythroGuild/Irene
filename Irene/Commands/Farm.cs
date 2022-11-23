using Module = Irene.Modules.Farm;

namespace Irene.Commands;

class Farm : CommandHandler {
	public const string
		Command_Farm = "farm",
		Arg_Material = "material";

	public Farm(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_Farm)} `<{Arg_Material}>` finds farming routes for the material.
		    Data adapted from `wow-professions.com`.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Farm,
			"Find a farming route.",
			new List<CommandOption> { new (
				Arg_Material,
				"The material to find a route for.",
				ApplicationCommandOptionType.String,
				required: true,
				autocomplete: true
			) },
			Permissions.None
		),
		ApplicationCommandType.SlashCommand,
		RespondAsync,
		new Dictionary<string, Func<Interaction, object, IDictionary<string, object>, Task>> {
			[Arg_Material] = AutocompleteAsync,
		}
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		string query = (string)args[Arg_Material];
		Module.Material? material = Module.ParseMaterial(query);

		// Send error message if no matching material was found.
		if (material is null) {
			string error = $"Sorry, couldn't find any routes to farm `{query}`.";
			interaction.RegisterFinalResponse();
			await interaction.RespondCommandAsync(error, true);
			interaction.SetResponseSummary(error);
			return;
		}

		// The module method will handle all responding, since it also
		// needs to register the sent message for component interactions.
		await Module.RespondAsync(interaction, material);
	}

	public async Task AutocompleteAsync(Interaction interaction, object arg, IDictionary<string, object> args) {
		await interaction.AutocompleteAsync(Module.AutocompleteOptions((string)arg));
	}
}
