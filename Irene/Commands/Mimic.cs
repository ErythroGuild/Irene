using Module = Irene.Modules.Mimic;

namespace Irene.Commands;

class Mimic : CommandHandler {
	public const string
		Command_Mimic = "mimic",
		Arg_Language = "language",
		Arg_Text = "text";
	public const int Length_Max = 800;

	public Mimic(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_Mimic)} `<{Arg_Language}> <{Arg_Text}>` obfuscates the input text.
		    This isn't a translator; it just uses the same words as in-game.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Mimic,
			"Obfuscate the given input text.",
			new List<CommandOption> {
				new (
					Arg_Language,
					"The language to mimic.",
					ApplicationCommandOptionType.String,
					required: true,
					autocomplete: true
				),
				new (
					Arg_Text,
					"The text to obfuscate.",
					ApplicationCommandOptionType.String,
					required: true,
					maxLength: Length_Max
				),
			},
			Permissions.SendMessages
		),
		ApplicationCommandType.SlashCommand,
		RespondAsync,
		new Dictionary<string, Func<Interaction, object, IDictionary<string, object>, Task>> {
			[Arg_Language] = AutocompleteAsync,
		}
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		string language = (string)args[Arg_Language];
		string text = (string)args[Arg_Text];

		string translated = Module.Translate(language, text);
		translated =
			$"""
			**{language}:**
			{translated}
			""";
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(translated);
		interaction.SetResponseSummary(translated);
	}

	public async Task AutocompleteAsync(Interaction interaction, object arg, IDictionary<string, object> args) {
		string input = (string)arg;
		List<(string, string)> options = Module.AutocompleteLanguage(input);
		await interaction.AutocompleteAsync(options);
	}
}
