namespace Irene.Commands;

using Module = Modules.Mimic;

class Mimic : CommandHandler {
	public const string
		CommandMimic = "mimic",
		ArgLanguage = "language",
		ArgText = "text";
	public const int LengthMax = 800;

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}{Mention(CommandMimic)} `<{ArgLanguage}> <{ArgText}>` obfuscates the input text.
		{_t}This isn't a translator; it just uses the same words as in-game.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandMimic,
			"Obfuscate the given input text.",
			AccessLevel.Guest,
			new List<DiscordCommandOption> {
				new (
					ArgLanguage,
					"The language to mimic.",
					ArgType.String,
					required: true,
					autocomplete: true
				),
				new (
					ArgText,
					"The text to obfuscate.",
					ArgType.String,
					required: true,
					maxLength: LengthMax
				),
			}
		),
		CommandType.SlashCommand,
		RespondAsync,
		new Dictionary<string, Completer> {
			[ArgLanguage] = Module.Completer,
		}
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		string language = (string)args[ArgLanguage];
		string text = (string)args[ArgText];

		string translated = Module.Translate(language, text);
		translated =
			$"""
			**{language}:**
			{translated}
			""";
		await interaction.RegisterAndRespondAsync(translated);
	}
}
