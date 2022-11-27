using Module = Irene.Modules.Translate;
using Language = Irene.Modules.Translate.Language;
using Result = Irene.Modules.Translate.Result;

namespace Irene.Commands;

class Translate : CommandHandler {
	public const string
		Command_Translate = "translate",
		Arg_Text = "text", // required options must come first
		Arg_Source = "from",
		Arg_Target = "to",
		Arg_Share = "share";
	public const int MaxLength = 800;

	public Translate(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_Translate)} `<{Arg_Text}> [{Arg_Source}] [{Arg_Target}] [{Arg_Share}]` translates the input text.
		    Defaults to "Auto-Detect" + "English" + `false`.
		    Translation powered by DeepL.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Translate,
			"Translates the input text.",
			new List<CommandOption> {
				new (
					Arg_Text,
					"The text to translate.",
					ApplicationCommandOptionType.String,
					required: true,
					maxLength: MaxLength
				),
				new (
					Arg_Source,
					"The language of the input text.",
					ApplicationCommandOptionType.String,
					required: false,
					autocomplete: true
				),
				new (
					Arg_Target,
					"The language to translate to.",
					ApplicationCommandOptionType.String,
					required: false,
					autocomplete: true
				),
				new (
					Arg_Share,
					"Whether the translation is shown to everyone.",
					ApplicationCommandOptionType.Boolean,
					required: false
				)
			},
			Permissions.SendMessages
		),
		ApplicationCommandType.SlashCommand,
		RespondAsync,
		new Dictionary<string, Func<Interaction, object, IDictionary<string, object>, Task>> {
			[Arg_Source] = AutocompleteSourceAsync,
			[Arg_Target] = AutocompleteTargetAsync,
		}
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		string text = (string)args[Arg_Text];
		string? argSource = args.ContainsKey(Arg_Source)
			? (string)args[Arg_Source]
			: null;
		string argTarget = args.ContainsKey(Arg_Target)
			? (string)args[Arg_Target]
			: Module.Language_EnglishUS;
		Language? languageSource = Module.ParseLanguageCode(argSource);
		Language languageTarget = Module.ParseLanguageCode(argTarget)
			?? throw new ArgumentException("Unsupported target language.", nameof(args));

		Result result = await Module.TranslateText(text, languageSource, languageTarget);
		DiscordEmbed embed = Module.RenderResult(text, result);
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithEmbed(embed);

		bool doShare = args.ContainsKey(Arg_Share)
			? (bool)args[Arg_Share]
			: false;
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, !doShare);
		interaction.SetResponseSummary($"{embed.Title}\n{embed.Description}");
	}

	public async Task AutocompleteSourceAsync(Interaction interaction, object arg, IDictionary<string, object> args) {
		string text = (string)arg;
		IList<(string, string)> options = Module.Autocomplete(true, text);
		await interaction.AutocompleteAsync(options);
	}

	public async Task AutocompleteTargetAsync(Interaction interaction, object arg, IDictionary<string, object> args) {
		string text = (string)arg;
		IList<(string, string)> options = Module.Autocomplete(false, text);
		await interaction.AutocompleteAsync(options);
	}
}

class TranslateContext : CommandHandler {
	public const string
		Command_Translate = "Translate";

	public TranslateContext(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		:lock: `> {Command_Translate}` translates the message to English.
		    Embeds are not translated, only message content.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Translate,
			"",
			new List<CommandOption>(),
			Permissions.None
		),
		ApplicationCommandType.MessageContextMenu,
		TranslateAsync
	);

	public async Task TranslateAsync(Interaction interaction, IDictionary<string, object> args) {
		DiscordMessage? message = interaction.TargetMessage;
		if (message is null) {
			Log.Error("No target message found for context menu command.");
			return;
		}

		// Infer translate command arguments.
		string text = message.Content;
		if (text == "") {
			string error =
				$"""
				The message content must not be blank.
				(Embeds cannot be translated.)
				""";
			interaction.RegisterFinalResponse();
			await interaction.RespondCommandAsync(error, true);
			interaction.SetResponseSummary(error);
			return;
		}

		Language? languageTarget =
			Module.ParseLanguageCode(Module.Language_EnglishUS);
		if (languageTarget is null)
			throw new ArgumentException("Invalid target language.");

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

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response);
		interaction.SetResponseSummary($"{embed.Title}\n{embed.Description}");
	}
}
