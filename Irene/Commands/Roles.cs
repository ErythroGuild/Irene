using Module = Irene.Modules.Roles;

namespace Irene.Commands;

class Roles : CommandHandler {

	public const string Command_Roles = "roles";

	public Roles(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_Roles)} shows all self-assignable roles.
		    You can choose what you'd like to get pinged for.
		    Reassign these at any time!
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Roles,
			"Check and reassign roles to receive pings.",
			new List<CommandOption>(),
			Permissions.None
		),
		ApplicationCommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		// Ensure DiscordMember object is usable.
		// Exit early if the conversion fails.
		DiscordMember? user = await interaction.User.ToMember();
		if (user is null) {
			string error =
				$"""
				Failed to fetch your server data.
				Try running the command again in {Erythro.Channel(id_ch.bots).Mention}?
				""";
			interaction.RegisterFinalResponse();
			await interaction.RespondCommandAsync(error, true);
			interaction.SetResponseSummary(error);
			return;
		}

		// Send role selection menu.
		await Module.RespondAsync(interaction, user);
	}
}
