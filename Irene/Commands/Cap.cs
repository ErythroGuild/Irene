using Module = Irene.Modules.Cap;

namespace Irene.Commands;

class Cap : CommandHandler {
	public const string
		Id_Command   = "cap",
		Arg_Resource = "resource";
	public const string
		Label_Valor    = "Valor",
		Label_Conquest = "Conquest",
		Label_Renown   = "Renown",
		Label_Torghast = "Tower Knowledge";
	public const string
		Opt_Valor    = "valor",
		Opt_Conquest = "conquest",
		Opt_Renown   = "renown",
		Opt_Torghast = "tower-knowledge";

	public Cap(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Id_Command)} `<{Arg_Resource}>` displays the current cap of the resource (e.g. valor).
		""";

	public override CommandTree CreateTree() => new (
		new (
			Id_Command,
			"Display the current cap of a resource.",
			new List<CommandOption> { new (
				Arg_Resource,
				"The type of resource to display.",
				ApplicationCommandOptionType.String,
				required: true,
				new List<CommandOptionEnum> {
					new (Label_Valor   , Opt_Valor	),
					new (Label_Conquest, Opt_Conquest),
					new (Label_Renown  , Opt_Renown  ),
					new (Label_Torghast, Opt_Torghast),
				}
			) },
			Permissions.None
		),
		RespondAsync
	);

	public static async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		string id = (string)args[Arg_Resource];
		Func<DateTimeOffset, Module.HideableString> calculator =
			id switch {
				Opt_Valor    => Module.DisplayValor,
				Opt_Conquest => Module.DisplayConquest,
				Opt_Renown   => Module.DisplayRenown,
				Opt_Torghast => Module.DisplayTorghast,
				_ => throw new ArgumentException("Unrecognized resource type.", nameof(args)),
			};

		DateTimeOffset now = DateTimeOffset.Now;
		Module.HideableString message = calculator(now);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(
			message.String,
			message.IsEphemeral
		);
	}
}
