using static Irene.Modules.Invite;

namespace Irene.Commands;

class Invite : CommandHandler {
	public const string
		Id_Command = "invite",
		Arg_Server = "server";
	public const string
		Label_Erythro = "Erythro",
		Label_Leuko   = "Leuko";
	public const string
		Data_Erythro = "erythro",
		Data_Leuko   = "leuko";

	public Invite(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Id_Command)} `<{Arg_Server}>` links an invite to the selected discord server.
		These invite links can also be found in {Erythro.Channel(id_ch.resources).Mention}.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Id_Command,
			"Show invite links for the guild discord servers.",
			new List<CommandOption> { new (
				Arg_Server,
				"The server to get an invite link to.",
				ApplicationCommandOptionType.String,
				required: false,
				new List<CommandOptionEnum> {
					new (Label_Erythro, Data_Erythro),
					new (Label_Leuko  , Data_Leuko  ),
				}
			) },
			Permissions.None
		),
		RespondAsync
	);

	public static async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		string id = (string)args[Arg_Server];
		string link = GetInvite(id);
		interaction.RegisterEvent(Interaction.Events.FinalResponse);
		await interaction.RespondCommandAsync(link);
	}
}
