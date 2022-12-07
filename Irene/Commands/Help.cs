namespace Irene.Commands;

using Irene.Interactables;

using Module = Modules.Help;

class Help : CommandHandler {
	public const string
		CommandHelp = "help",
		ArgCommand = "command";
	private const int _maxOptions = 20;

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.None)}{Mention(CommandHelp)} lists help for all commands,
		{RankIcon(AccessLevel.None)}{Mention(CommandHelp)} `<{ArgCommand}>` displays the help for that command.
		{_t}Required permissions for commands: {RankIcon(AccessLevel.Guest)}{RankIcon(AccessLevel.Member)}{RankIcon(AccessLevel.Officer)}
		{_t}Arguments: `<required> [optional] [option A|option B]`
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandHelp,
			"Display help for any command(s).",
			AccessLevel.None,
			new List<DiscordCommandOption> { new (
				ArgCommand,
				"The command to display help for.",
				ArgType.String,
				required: false,
				autocomplete: true
			) }
		),
		CommandType.SlashCommand,
		RespondAsync,
		new Dictionary<string, Autocompleter> {
			[ArgCommand] = AutocompleteAsync,
		}
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		// Respond with help for specific command, if one was specified.
		if (args.Count > 0) {
			string command = NormalizeCommand(args[ArgCommand]);
			string help = Module.CommandHelp(command) ??
				$"""
				:thought_balloon: Unknown command: `/{command}`
				You can check {Mention(CommandHelp)} for a list of valid commands.
				""";
			await interaction.RegisterAndRespondAsync(help, true);
			return;
		}

		// Otherwise, respond with general help command.
		MessagePromise messagePromise = new ();
		DiscordMessageBuilder response = Pages.Create(
			interaction,
			messagePromise.Task,
			Module.GeneralHelp(),
			pageSize: 1
		);
		string summary = "General help pages sent.";
		await interaction.RegisterAndRespondAsync(response, summary, true);

		DiscordMessage message = await interaction.GetResponseAsync();
		messagePromise.SetResult(message);
	}

	public async Task AutocompleteAsync(Interaction interaction, object arg, ParsedArgs args) {
		string input = NormalizeCommand(arg);
		List<(string, string)> options = new ();

		// Search through registered commands for matching slash commands.
		IReadOnlyDictionary<string, CommandHandler> commands = Dispatcher.Table;
		foreach (string command in commands.Keys) {
			if (command.Contains(input) &&
				commands[command].Command.Type == CommandType.SlashCommand
			) {
				options.Add((command, command));
			}
		}

		// Limit the number of provided options.
		if (options.Count > _maxOptions)
			options = options.GetRange(0, _maxOptions);

		await interaction.AutocompleteAsync(options);
	}

	private static string NormalizeCommand(object arg) =>
		((string)arg).Trim().ToLower();
}
