namespace Irene.Commands;

class Version : ICommand {
	private const string
		_pathBuild   = @"config/commit.txt",
		_pathVersion = @"config/tag.txt";

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@"`/version` displays the most recent release version and currently running build.",
			"These values are automatically generated from git when the bot is built."
		} ) };
	}

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"version",
				"Display the build the bot is currently running.",
				options: null,
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferVisibleAsync, RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }
	public static List<AutoCompleteHandler> AutoComplete   { get => new (); }

	public static async Task RunAsync(TimedInteraction interaction) {
		StreamReader file;

		// Read in data.
		file = File.OpenText(_pathBuild);
		string build = file.ReadLine() ?? "";
		if (build.Length > 7)
			build = build[..7];
		file.Close();

		file = File.OpenText(_pathVersion);
		string version = file.ReadLine() ?? "";
		file.Close();

		string output = $"**Irene {version}** build `{build}`";

		// Respond with data.
		await Command.SubmitResponseAsync(
			interaction,
			output,
			"Sending version information.",
			LogLevel.Debug,
			"{Version}, build {Build}".AsLazy(),
			version, build
		);
	}
}
