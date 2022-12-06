namespace Irene.Commands;

using Module = Modules.Roles;

class Roles : CommandHandler {
	public const string CommandRoles = "roles";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.None)}{Mention(CommandRoles)} shows all self-assignable roles.
		    You can choose what you'd like to get pinged for.
		    Reassign these at any time!
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandRoles,
			"Check and reassign roles to receive pings.",
			AccessLevel.None,
			new List<DiscordCommandOption>()
		),
		CommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		CheckErythroInit();

		// Ensure DiscordMember object is usable.
		// Exit early if the conversion fails.
		DiscordMember? user = await interaction.User.ToMember();
		if (user is null) {
			string error =
				$"""
				Failed to fetch your server data.
				Try running the command again in {Erythro.Channel(id_ch.bots).Mention}?
				""";
			await interaction.RegisterAndRespondAsync(error, true);
			return;
		}

		// Send role selection menu.
		await Module.RespondAsync(interaction, user);
	}
}
