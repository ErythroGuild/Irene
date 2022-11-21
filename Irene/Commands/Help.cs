using Irene.Interactables;

using Module = Irene.Modules.Help;

namespace Irene.Commands;

class Help : CommandHandler {
	public const string
		Command_Help = "help",
		Arg_Command = "command";
	private const int _maxOptions = 20;

	public Help(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_Help)} lists help for *all* commands,
		{Command.Mention(Command_Help)} `<{Arg_Command}>` displays the help for that command.
		    :lock: indicates a command is only available to officers.
		    Arguments: `<required> [optional] [option A | option B]`
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Help,
			"Display help for any command(s).",
			new List<CommandOption> { new (
				Arg_Command,
				"The command to display help for.",
				ApplicationCommandOptionType.String,
				required: false,
				autocomplete: true
			) },
			Permissions.None
		),
		ApplicationCommandType.SlashCommand,
		RespondAsync,
		new Dictionary<string, Func<Interaction, object, IDictionary<string, object>, Task>> {
			[Arg_Command] = AutocompleteAsync,
		}
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		// Respond with help for specific command, if one was specified.
		if (args.Count > 0) {
			string command = NormalizeCommand(args[Arg_Command]);
			string help = Module.CommandHelp(command) ??
				$"""
				:thought_balloon: Unknown command: `/{command}`
				You can check {Command.Mention(Command_Help)} for a list of valid commands.
				""";

			interaction.RegisterFinalResponse();
			await interaction.RespondCommandAsync(help, true);
			interaction.SetResponseSummary(help);
			return;
		}

		// Otherwise, respond with general help command.
		MessagePromise message_promise = new ();
		DiscordMessageBuilder response = Pages.Create(
			interaction,
			message_promise.Task,
			Module.GeneralHelp(),
			pageSize: 1
		);

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response.Content);

		DiscordMessage message = await interaction.GetResponseAsync();
		message_promise.SetResult(message);
	}

	public async Task AutocompleteAsync(Interaction interaction, object arg, IDictionary<string, object> args) {
		string input = NormalizeCommand(arg);
		List<(string, string)> options = new ();

		// Search through registered commands for matching slash commands.
		IReadOnlyDictionary<string, CommandHandler> commands =
			CommandDispatcher.HandlerTable;
		foreach (string command in commands.Keys) {
			if (command.Contains(input) &&
				commands[command].Command.Type == ApplicationCommandType.SlashCommand
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
