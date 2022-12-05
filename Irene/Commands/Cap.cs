namespace Irene.Commands;

using Module = Modules.Cap;

class Cap : CommandHandler {
	public const string
		CommandCap  = "cap",
		ArgResource = "resource";
	public const string
		LabelValor    = "Valor",
		LabelConquest = "Conquest",
		LabelRenown   = "Renown",
		LabelTorghast = "Tower Knowledge";
	public const string
		OptionValor    = "valor",
		OptionConquest = "conquest",
		OptionRenown   = "renown",
		OptionTorghast = "tower-knowledge";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.None)}{Mention(CommandCap)} `<{ArgResource}>` displays the current cap of the resource (e.g. valor).
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandCap,
			"Display the current cap of a resource.",
			AccessLevel.None,
			new List<DiscordCommandOption> { new (
				ArgResource,
				"The type of resource to display.",
				ArgType.String,
				required: true,
				new List<DiscordCommandOptionEnum> {
					new (LabelValor   , OptionValor	),
					new (LabelConquest, OptionConquest),
					new (LabelRenown  , OptionRenown  ),
					new (LabelTorghast, OptionTorghast),
				}
			) }
		),
		CommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		string id = (string)args[ArgResource];
		Func<DateTimeOffset, Module.HideableString> calculator =
			id switch {
				OptionValor    => Module.DisplayValor,
				OptionConquest => Module.DisplayConquest,
				OptionRenown   => Module.DisplayRenown,
				OptionTorghast => Module.DisplayTorghast,
				_ => throw new ImpossibleArgException(ArgResource, id),
			};

		DateTimeOffset now = DateTimeOffset.Now;
		Module.HideableString message = calculator(now);

		AccessLevel accessLevel = await Modules.Rank.GetRank(interaction.User);
		bool isPrivate = accessLevel == AccessLevel.None;

		await interaction.RegisterAndRespondAsync(
			message.String,
			message.IsEphemeral || isPrivate
		);
	}
}
