namespace Irene.Commands;

class Version : ICommand {
	private const string
		_pathBuild   = @"config/commit.txt",
		_pathVersion = @"config/tag.txt";

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@"`/version` displays the most recent release version and currently running build."
		} )
	}; }

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"version",
				"Display the build the bot is currently running.",
				null,
				true,
				ApplicationCommandType.SlashCommand
			), RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }

	private static async Task RunAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
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
		Log.Debug("  Sending version information.");
		Log.Debug("    {VersionString}", output);
		stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
		await interaction.RespondMessageAsync(output);
		Log.Information("  Version information sent.");
	}
}
