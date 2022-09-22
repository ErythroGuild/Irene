namespace Irene.Commands;

class About : AbstractCommand {
	private static readonly object _lock = new ();
	private const string
		_pathBuild   = @"config/commit.txt",
		_pathVersion = @"config/tag.txt";

	public override List<string> HelpPages =>
		new () { new List<string> {
			@"`/about` displays the most recent release version and currently running build.",
			"These values are automatically generated from git when the bot is built."
		}.ToLines() };

	public override List<InteractionCommand> SlashCommands =>
		new () {
			new ( new (
				"about",
				"Display the build the bot is currently running.",
				options: null,
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferVisibleAsync, RunAsync )
		};

	public static async Task RunAsync(TimedInteraction interaction) {
		StreamReader file;
		string build = "";
		string version = "";

		// Read in data.
		lock (_lock) {
			file = File.OpenText(_pathBuild);
			build = file.ReadLine() ?? "";
			if (build.Length > 7)
				build = build[..7];
			file.Close();
		}

		lock (_lock) {
			file = File.OpenText(_pathVersion);
			version = file.ReadLine() ?? "";
			file.Close();
		}

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
