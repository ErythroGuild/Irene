using static Irene.ClassSpec;

using Module = Irene.Modules.ClassDiscord;

namespace Irene.Commands;

class ClassDiscord : CommandHandler {
	public const string
		Command_ClassDiscord = "class-discord",
		Arg_Class = "class";

	public ClassDiscord(GuildData erythro) : base(erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_ClassDiscord)} `<{Arg_Class}>` links an invite to the class discord server.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_ClassDiscord,
			"Get the invite link to a class discord.",
			new List<CommandOption> { new (
				Arg_Class,
				"The class discord to get an invite to.",
				ApplicationCommandOptionType.String,
				required: true,
				new List<CommandOptionEnum> {
					OptionFromClass(Class.DH     ),
					OptionFromClass(Class.DK     ),
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
			) },
			Permissions.None
		),
		RespondAsync
	);

	private static CommandOptionEnum OptionFromClass(Class @class) =>
		new (@class.Name(), @class.ToString());

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		Class @class = Enum.Parse<Class>((string)args[Arg_Class]);
		string response = Module.GetInvite(@class);

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response);
		interaction.SetResponseSummary(response);
	}
}
