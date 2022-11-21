using Module = Irene.Modules.Cap;

namespace Irene.Commands;

class Cap : CommandHandler {
	public const string
		Command_Cap  = "cap",
		Arg_Resource = "resource";
	public const string
		Label_Valor    = "Valor",
		Label_Conquest = "Conquest",
		Label_Renown   = "Renown",
		Label_Torghast = "Tower Knowledge";
	public const string
		Option_Valor    = "valor",
		Option_Conquest = "conquest",
		Option_Renown   = "renown",
		Option_Torghast = "tower-knowledge";

	public Cap(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_Cap)} `<{Arg_Resource}>` displays the current cap of the resource (e.g. valor).
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Cap,
			"Display the current cap of a resource.",
			new List<CommandOption> { new (
				Arg_Resource,
				"The type of resource to display.",
				ApplicationCommandOptionType.String,
				required: true,
				new List<CommandOptionEnum> {
					new (Label_Valor   , Option_Valor	),
					new (Label_Conquest, Option_Conquest),
					new (Label_Renown  , Option_Renown  ),
					new (Label_Torghast, Option_Torghast),
				}
			) },
			Permissions.None
		),
		ApplicationCommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		string id = (string)args[Arg_Resource];
		Func<DateTimeOffset, Module.HideableString> calculator =
			id switch {
				Option_Valor    => Module.DisplayValor,
				Option_Conquest => Module.DisplayConquest,
				Option_Renown   => Module.DisplayRenown,
				Option_Torghast => Module.DisplayTorghast,
				_ => throw new ArgumentException("Unrecognized resource type.", nameof(args)),
			};

		DateTimeOffset now = DateTimeOffset.Now;
		Module.HideableString message = calculator(now);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(
			message.String,
			message.IsEphemeral
		);
		interaction.SetResponseSummary(message.String);
	}
}
