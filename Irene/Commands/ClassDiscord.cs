namespace Irene.Commands;

using static Irene.ClassSpec;

using Module = Modules.ClassDiscord;

class ClassDiscord : CommandHandler {
	public const string
		CommandClassDiscord = "class-discord",
		ArgClass = "class";

	public override string HelpText =>
		$"""
		{RankEmoji(AccessLevel.None)}{Command.Mention(CommandClassDiscord)} `<{ArgClass}>` links an invite to the class discord server.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandClassDiscord,
			"Get the invite link to a class discord.",
			AccessLevel.None,
			new List<DiscordCommandOption> { new (
				ArgClass,
				"The class discord to get an invite to.",
				ArgType.String,
				required: true,
				new List<DiscordCommandOptionEnum> {
					OptionFromClass(Class.DK     ),
					OptionFromClass(Class.DH     ),
					OptionFromClass(Class.Druid  ),
					OptionFromClass(Class.Evoker ),
					OptionFromClass(Class.Hunter ),
					OptionFromClass(Class.Mage   ),
					OptionFromClass(Class.Monk   ),
					OptionFromClass(Class.Paladin),
					OptionFromClass(Class.Priest ),
					OptionFromClass(Class.Rogue  ),
					OptionFromClass(Class.Shaman ),
					OptionFromClass(Class.Warlock),
					OptionFromClass(Class.Warrior),
				}
			) }
		),
		CommandType.SlashCommand,
		RespondAsync
	);

	private static DiscordCommandOptionEnum OptionFromClass(Class @class) =>
		new (@class.Name(), @class.ToString());

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		Class @class = Enum.Parse<Class>((string)args[ArgClass]);
		string response = Module.GetInvite(@class);

		AccessLevel accessLevel = await Modules.Rank.GetRank(interaction.User);
		bool isPrivate = accessLevel == AccessLevel.None;

		await interaction.RegisterAndRespondAsync(response, isPrivate);
	}
}
