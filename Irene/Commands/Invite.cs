namespace Irene.Commands;

using Module = Modules.Invite;

class Invite : CommandHandler {
	public const string
		CommandInvite = "invite",
		ArgServer     = "server";
	public const string
		LabelErythro = "Erythro"   ,
		LabelLeuko   = "Leuko"     ,
		LabelBnet    = "Battle.net";
	public const string
		OptionErythro = "erythro",
		OptionLeuko   = "leuko"  ,
		OptionBnet    = "bnet"   ;

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.None)}{Mention(CommandInvite)} `[{ArgServer}]` links server invites.
		{_t}These links can also be found in {Erythro?.Channel(id_ch.resources).Mention ?? "#resources"}.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandInvite,
			"Show invite links for guild-related servers.",
			AccessLevel.None,
			new List<DiscordCommandOption> { new (
				ArgServer,
				"The server to get an invite link to.",
				ArgType.String,
				required: false,
				new List<DiscordCommandOptionEnum> {
					new (LabelErythro, OptionErythro),
					new (LabelLeuko  , OptionLeuko  ),
					new (LabelBnet   , OptionBnet   ),
				}
			) }
		),
		CommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		string id = args.TryGetValue(ArgServer, out object? value)
			? (string)value
			: OptionErythro;
		Module.Server server = id switch {
			OptionErythro => Module.Server.Erythro,
			OptionLeuko   => Module.Server.Leuko  ,
			OptionBnet    => Module.Server.Bnet   ,
			_ => throw new ImpossibleArgException(ArgServer, id),
		};

		AccessLevel accessLevel = await Modules.Rank.GetRank(interaction.User);
		bool isPrivate = accessLevel == AccessLevel.None;

		string link = Module.GetInvite(server);
		await interaction.RegisterAndRespondAsync(link, isPrivate);
	}
}
