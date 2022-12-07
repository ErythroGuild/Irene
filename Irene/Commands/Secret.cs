namespace Irene.Commands;

using Module = Modules.Secret;

class Secret : CommandHandler {
	public const string
		CommandSecret = "secret",
		ArgPassphrase = "passphrase";

	// Help text is nonstandard--this is a secret, after all!
	public override string HelpText => "Shh...";

	public override CommandTree CreateTree() => new (
		new (
			CommandSecret,
			"...",
			AccessLevel.Guest,
			new List<DiscordCommandOption> { new (
				ArgPassphrase,
				"?",
				ArgType.String,
				required: true
			) }
		),
		CommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		DateTimeOffset now = DateTimeOffset.UtcNow;
		string attempt = (string)args[ArgPassphrase];
		DiscordMember? member = await interaction.User.ToMember();

		if (member is null) {
			string error = "Could not fetch your data. Try again in a bit?";
			await interaction.RegisterAndRespondAsync(error, true);
			return;
		}

		string response = await Module.Respond(now, attempt, member);
		await interaction.RegisterAndRespondAsync(response, true);
	}
}
