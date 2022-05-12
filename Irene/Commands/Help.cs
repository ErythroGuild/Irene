using Irene.Components;

namespace Irene.Commands;

class Help : AbstractCommand {
	private const string _commandHelp = "help";
	private const string
		_l = "\U0001F512", // :lock:
		_p = "\U0001F464", // :bust_in_silhouette:
		_s = "\U0001F465", // :busts_in_silhouette:
		_t = "\u2003"    ; // tab

	public override List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@"`/help` lists all available commands, along with a short description for each,",
			@"`/help <command>` shows a more detailed guide on how to use the command.",
			"If you have any questions at all, Ernie will be happy to answer them."
		} ) };
	}

	public override List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				_commandHelp,
				"List available commands and how to use them.",
				new List<CommandOption> { new (
					"command",
					"The command to get detailed help for.",
					ApplicationCommandOptionType.String,
					required: false,
					autocomplete: true
				) },
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferVisibleAsync, RunAsync)
		};
	}

	public override List<AutoCompleteHandler> AutoCompletes { get => new () {
		new (_commandHelp, AutoCompleteAsync),
	}; }

	private static List<string> HelpGeneral { get => new () {
		{ string.Join("\n", new List<string> {
			"If you need any help, ask, or shoot Ernie a message! :+1:",
			$"{_s} - public by default",
			$"{_p} - private by default",
			$"{_l} - restricted permissions",
			"`<required>`, `[optional]`, `[option A | option B]`",
			"",
			"**Help**",
			$@"{_p} `/help`: Display this help text.",
			$@"{_p} `/help <command>`: Display help for a specific command.",
			"",
			"**Version**",
			$@"{_s} `/version`: Display the currently running version + build.",
		} ) },
		{ string.Join("\n", new List<string> {
			"**Roles**",
			$@"{_p} `/roles`: Set (or clear) roles to be pinged for.",
			"",
			"**Rank**",
			$@"{_p}{_l} `/rank set <member>`: Set the rank of the specified member.",
			$@"{_p} `/rank set-guilds`: Set your own guild roles.",
			$@"{_p}{_l} `/rank set-guilds <member>`: Set the guild roles of the specified member.",
			$@"{_p}{_l} `/rank set-officer <member>`: Set the officer roles of the specified member.",
			$@"{_s}{_l} `/rank list-trials`: Display a list of all trials (Guest + <Erythro>).",
		} ) },
		{ string.Join("\n", new List<string> {
			"**Raid**",
			$@"{_p} `/raid info [share]`: Shows the plans for the upcoming raid.",
			//$@"{_p} `/raid eligibility`: Shows the raid requirements (and checks if you meet them).",
			//$@"{_p}{_l} `/raid eligibility <member>`: Checks the raid requirements for a specific member.",
			$@"{_s} `/raid view-logs <date>`: Displays the logs for the given date.",
			$@"{_s}{_l} `/raid set-logs <group> <date> <link>`: Sets the logs for the given date.",
			$@"{_s}{_l} `/raid set-plan <date>`: Sets the plans for the given date.",
			$@"{_s}{_l} `/raid cancel <date> [do-cancel]`: Mark the raids on a date as canceled.",
		//	"",
		//	"**Keys**",
		//	$@"{_p} `/keys [share] [sort] [filter]`:",
		}) },
		{
			string.Join("\n", new List<string> {
			"**Tags**",
			$@"{_p} `/tags view <name> [share]`: Display the named tag.",
			$@"{_p} `/tags list`: List all available tags.",
			$@"{_p}{_l} `/tags set <name>`: Edit (or create a new) named tag.",
			$@"{_p}{_l} `/tags delete <name>`: Delete the named tag.",
			//"",
			//"**Boss Guides**",
			//$@"{_e} `/boss-guide <raid> <boss> [difficulty] [share] [type]`:",
		} ) },
		{ string.Join("\n", new List<string> {
			"**In-Game Data**",
			$@"{_s} `/cap <type>`: Display the current cap of the named resource.",
			//$@"{_s} `/emissaries <type>`:",
			//$@"{_s} `/assaults <type>`:",
			//"",
			//"**Solvers**",
			//$@"{_p} `/solve mrrl`:",
			//$@"{_p} `/solve lights-out`:",
		} ) },
		{ string.Join("\n", new List<string> {
			"**Awards**",
			//$@"{_p} `/commend`:",
			$@"{_p}{_l} `/best-of block <message-id> <channel>`: Blocks a message from being pinned.",
			$@"{_p}{_l} `/best-of unblock <message-id> <channel>`: Allows a message to be pinned again.",
			//"",
			//"**Moderation**",
			//$@"{_s}{_l} `/slowmode <channel>`:",
			//$@"{_p}{_l} `/change-subject `:",
		} ) },
		//{ string.Join("\n", new List<string> {
		//	"****",
		//	$@"{_p} `/`:",
		//} ) },
		{
			string.Join("\n", new List<string> {
			"**Utilities**",
			$@"{_s} `/roll`: Generate a number between `1` and `100`.",
			$@"{_s} `/roll <max>`: Generate a number between `1` and `max`.",
			$@"{_s} `/roll <min> <max>`: Generate a number between `min` and `max`.",
			$@"{_s} `/8-ball <query> [keep-private]`: Predict the answer to a yes/no question.",
			"",
			"**Discord Servers**",
			$@"{_s} `/invite [erythro|leuko]`: Display the invite link for the guild servers.",
			$@"{_s} `/class-discord <class>`: Display the invite link(s) for the specified class discord.",
		} ) },
	}; }

	public static async Task RunAsync(TimedInteraction interaction) {
		// See if general or specific help page is requested.
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		DiscordWebhookBuilder response;
		DiscordMessage message;
		MessagePromise message_promise = new ();

		// If specific help is requested.
		if (args.Count > 0) {
			string command = (string)args[0].Value;
			command = command.Trim().ToLower();
			if (!Command.HelpPages.ContainsKey(command)) {
				string response_error =
					"Sorry, no command with that name found." +
					"\nSee `/help` for a list of valid commands.";
				await Command.SubmitResponseAsync(
					interaction,
					response_error,
					"Help for command not found.",
					LogLevel.Information,
					"Command not found: `/{Command}`.".AsLazy(),
					command
				);
				return;
			}

			List<string> help = Command.HelpPages[command].Invoke();
			response = Pages.Create(
				interaction.Interaction,
				message_promise.Task,
				help,
				pageSize: 1
			);
			message = await Command.SubmitResponseAsync(
				interaction,
				response,
				"Sending specific help.",
				LogLevel.Debug,
				"Sent help for `/{Command}`.".AsLazy(),
				command
			);
			message_promise.SetResult(message);
			return;
		}

		// Else, send general help.
		response = Pages.Create(
			interaction.Interaction,
			message_promise.Task,
			HelpGeneral,
			pageSize: 1
		);
		message = await Command.SubmitResponseAsync(
			interaction,
			response,
			"Sending general help.",
			LogLevel.Debug,
			"Help text sent.".AsLazy()
		);
		message_promise.SetResult(message);
	}

	public static async Task AutoCompleteAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string arg = (string)args[0].Value;
		arg = arg.Trim().ToLower();

		// Search through available options.
		List<string> results = new ();
		List<string> commands = new (Command.HelpPages.Keys);
		foreach (string command in commands) {
			if (command.Contains(arg))
				results.Add(command);
		}

		// Only return first 25 results.
		if (results.Count > 25)
			results = results.GetRange(0, 25);

		await interaction.AutoCompleteResultsAsync(results);
	}
}
