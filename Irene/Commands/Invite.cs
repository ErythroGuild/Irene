using Module = Irene.Modules.Invite;

namespace Irene.Commands;

class Invite : CommandHandler {
	public const string
		Command_Invite = "invite",
		Arg_Server = "server";
	public const string
		Label_Erythro = "Erythro",
		Label_Leuko   = "Leuko";
	public const string
		Option_Erythro = "erythro",
		Option_Leuko   = "leuko";

	public Invite(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_Invite)} `<{Arg_Server}>` links an invite to the selected discord server.
		    These invite links can also be found in {Erythro.Channel(id_ch.resources).Mention}.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Invite,
			"Show invite links for the guild discord servers.",
			new List<CommandOption> { new (
				Arg_Server,
				"The server to get an invite link to.",
				ApplicationCommandOptionType.String,
				required: false,
				new List<CommandOptionEnum> {
					new (Label_Erythro, Option_Erythro),
					new (Label_Leuko  , Option_Leuko  ),
				}
			) },
			Permissions.None
		),
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		string id = args.ContainsKey(Arg_Server)
			? (string)args[Arg_Server]
			: Option_Erythro;
		string link = Module.GetInvite(id);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(link);
		interaction.SetResponseSummary(link);
	}
}
