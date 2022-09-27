using System.Linq;

using Module = Irene.Modules.About;

namespace Irene.Commands;

class About : CommandHandler {
	public const string Command_About = "about";

	public About(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_About)} displays the currently running version and status.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_About,
			"Display bot version and status.",
			new List<CommandOption>(),
			Permissions.None
		),
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> _) {
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithEmbed(Module.CollateStatusEmbed())
			.WithAllowedMentions(Mentions.None);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response);

		// Extract status data from the submitted response.
		string[] bodyText = response.Embed.Description.Split('\n');
		List<string> statusText = new ();
		foreach (string line in bodyText[2..^4])
			statusText.Add(line[3..]);

		interaction.SetResponseSummary(
			$"""
			Irene {Module.String_Version} build {Module.String_Build}
			{string.Join("\n", statusText)}
			{response.Embed.Footer.Text}
			"""
		);
	}
}
