namespace Irene.Commands;

class Version : AbstractCommand {
	private const string
		_pathBuild   = @"config/commit.txt",
		_pathVersion = @"config/tag.txt";

	public override List<string> HelpPages =>
		new () { new List<string> {
			@"`/version` displays the most recent release version and currently running build.",
			"These values are automatically generated from git when the bot is built."
		}.ToLines() };

	public override List<InteractionCommand> SlashCommands =>
		new () {
			new ( new (
				"version",
				"Display the build the bot is currently running.",
				options: null,
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferVisibleAsync, RunAsync )
		};

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
