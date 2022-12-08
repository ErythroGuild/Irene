namespace Irene.Commands;

using Module = Modules.Translate;
using Language = Modules.Translate.Language;
using Result = Modules.Translate.Result;

class Translate : CommandHandler {
	public const string
		CommandTranslate = "translate",
		ArgText = "text", // required options must come first
		ArgSource = "from",
		ArgTarget = "to",
		ArgShare = "share";
	// Leave some room for e.g. formatting.
	public const int MaxLength = 800;

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}{Mention(CommandTranslate)} `<{ArgText}> [{ArgSource}] [{ArgTarget}] [{ArgShare}]` translates the input text.
		{_t}Defaults to "Auto-Detect" + "English" + `false`.
		{_t}Translation powered by DeepL.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandTranslate,
			"Translates the input text.",
			AccessLevel.Guest,
			new List<DiscordCommandOption> {
				new (
					ArgText,
					"The text to translate.",
					ArgType.String,
					required: true,
					maxLength: MaxLength
				),
				new (
					ArgSource,
					"The language of the input text.",
					ArgType.String,
					required: false,
					autocomplete: true
				),
				new (
					ArgTarget,
					"The language to translate to.",
					ArgType.String,
					required: false,
					autocomplete: true
				),
				new (
					ArgShare,
					"Whether the translation is shown to everyone.",
					ArgType.Boolean,
					required: false
				)
			}
		),
		CommandType.SlashCommand,
		RespondAsync,
		new Dictionary<string, AutocompleteHandler> {
			[ArgSource] = AutocompleteSourceAsync,
			[ArgTarget] = AutocompleteTargetAsync,
		}
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		string text = (string)args[ArgText];
		string? argSource = args.ContainsKey(ArgSource)
			? (string)args[ArgSource]
			: null;
		string argTarget = args.ContainsKey(ArgTarget)
			? (string)args[ArgTarget]
			: Module.LanguageEnglishUS;
		Language? languageSource = Module.ParseLanguageCode(argSource);
		Language languageTarget = Module.ParseLanguageCode(argTarget)
			?? throw new ImpossibleArgException(ArgTarget, argTarget);

		Result result = await Module.TranslateText(text, languageSource, languageTarget);
		DiscordEmbed embed = Module.RenderResult(text, result);
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithEmbed(embed);

		bool doShare = args.ContainsKey(ArgShare)
			? (bool)args[ArgShare]
			: false;
		string summary = $"{embed.Title}\n{embed.Description}";
		await interaction.RegisterAndRespondAsync(response, summary, !doShare);
	}

	public async Task AutocompleteSourceAsync(Interaction interaction, object arg, ParsedArgs args) {
		string text = (string)arg;
		IList<(string, string)> options = Module.Autocomplete(true, text);
		await interaction.AutocompleteAsync(options);
	}

	public async Task AutocompleteTargetAsync(Interaction interaction, object arg, ParsedArgs args) {
		string text = (string)arg;
		IList<(string, string)> options = Module.Autocomplete(false, text);
		await interaction.AutocompleteAsync(options);
	}
}

class TranslateContext : CommandHandler {
	public const string CommandTranslate = "Translate";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}`> {CommandTranslate}` translates the message to English.
		    Embeds are not translated, only message content.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandTranslate,
			"",
			AccessLevel.Guest,
			new List<DiscordCommandOption>()
		),
		CommandType.MessageContextMenu,
		TranslateAsync
	);

	public async Task TranslateAsync(Interaction interaction, ParsedArgs args) {
		DiscordMessage? message = interaction.TargetMessage;
		if (message is null) {
			Log.Error("No target message found for context menu command.");
			throw new ImpossibleArgException("Target message", "N/A");
		}

		// Infer translate command arguments.
		string text = message.Content;
		if (text == "") {
			string error =
				$"""
				The message content must not be blank.
				(Embeds cannot be translated.)
				""";
			await interaction.RegisterAndRespondAsync(error, true);
			return;
		}

		Language? languageTarget =
			Module.ParseLanguageCode(Module.LanguageEnglishUS);
		if (languageTarget is null)
			throw new ImpossibleException();
		// This throws if initialization failed somehow.

		// Fetch and display results.
		Result result = await Module.TranslateText(
			text,
			null,
			languageTarget.Value
		);
		DiscordEmbed embed = Module.RenderResult(text, result);
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithEmbed(embed);

		string summary = $"{embed.Title}\n{embed.Description}";
		await interaction.RegisterAndRespondAsync(response, summary);
	}
}
