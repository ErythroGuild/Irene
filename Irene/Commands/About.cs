using Module = Irene.Modules.About;

namespace Irene.Commands;

class About : CommandHandler {
	public const string Id_Command = "about";

	public About(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Id_Command)} displays the currently running version and status.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Id_Command,
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
		interaction.SetResponseSummary(
			$"""
			Irene {Module.String_Version} build {Module.String_Build}
			[status info] [...]
			Uptime: {Module.GetUptime():c}
			"""
		);
	}
}
