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
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		string placeholder = "What do we have here? The time is not yet ripe...";
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(placeholder, true);
		interaction.SetResponseSummary(placeholder);
	}
}
