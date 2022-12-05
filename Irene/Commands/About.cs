namespace Irene.Commands;

using Module = Modules.About;

class About : CommandHandler {
	public const string CommandAbout = "about";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.None)}{Mention(CommandAbout)} displays the currently running version and status.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandAbout,
			"Display bot version and status.",
			AccessLevel.None,
			new List<DiscordCommandOption>()
		),
		CommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithEmbed(Module.CollateStatusEmbed())
			.WithAllowedMentions(Mentions.None);

		AccessLevel accessLevel = await Modules.Rank.GetRank(interaction.User);
		bool isPrivate = accessLevel == AccessLevel.None;
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, isPrivate);

		// Extract status data from the submitted response.
		string[] bodyText = response.Embed.Description.Split('\n');
		List<string> statusText = new ();
		foreach (string line in bodyText[2..^4])
			statusText.Add(line[3..]);

		interaction.SetResponseSummary(
			$"""
			Irene {Module.StringVersion} build {Module.StringBuild}
			{string.Join("\n", statusText)}
			{response.Embed.Footer.Text}
			"""
		);
	}
}
