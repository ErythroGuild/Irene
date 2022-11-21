using Module = Irene.Modules.Secret;

namespace Irene.Commands;

class Secret : CommandHandler {
	public const string
		Command_Secret = "secret",
		Arg_Passphrase = "passphrase";

	public Secret(GuildData erythro) : base (erythro) { }

	// Help text is nonstandard--this is a secret, after all!
	public override string HelpText => "Shh...";

	public override CommandTree CreateTree() => new (
		new (
			Command_Secret,
			"...",
			new List<CommandOption> { new (
				Arg_Passphrase,
				"?",
				ApplicationCommandOptionType.String,
				required: true
			) },
			Permissions.None
		),
		ApplicationCommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		DateTimeOffset now = DateTimeOffset.UtcNow;
		string attempt = (string)args[Arg_Passphrase];
		DiscordMember? member = await interaction.User.ToMember();

		if (member is null) {
			string error = "Could not fetch your data. Try again in a bit?";
			interaction.RegisterFinalResponse();
			await interaction.RespondCommandAsync(error, true);
			interaction.SetResponseSummary(error);
			return;
		}

		string response = await Module.Respond(now, attempt, member, Erythro);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, true);
		interaction.SetResponseSummary(response);
	}
}
