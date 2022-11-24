using Module = Irene.Modules.WowToken;

namespace Irene.Commands;

class WowToken : CommandHandler {
	public const string
		Command_WowToken = "wow-token",
		Arg_Region = "region";
	public const string
		Label_US = "US",
		Label_EU = "EU",
		Label_KR = "Korea",
		Label_TW = "Taiwan",
		Label_CN = "China";
	public const string
		Option_US = "us",
		Option_EU = "eu",
		Option_KR = "kr",
		Option_TW = "tw",
		Option_CN = "cn";

	public WowToken(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_WowToken)} `[{Arg_Region}]` shows the latest token price.
		    If no region is specified, defaults to US prices.
		    Data from `wowtokenprices.com`.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_WowToken,
			"Show the latest token price.",
			new List<CommandOption> { new (
				Arg_Region,
				"The region to show prices for.",
				ApplicationCommandOptionType.String,
				required: false,
				new List<CommandOptionEnum> {
					new (Label_US, Option_US),
					new (Label_EU, Option_EU),
					new (Label_KR, Option_KR),
					new (Label_TW, Option_TW),
					new (Label_CN, Option_CN),
				}
			) },
			Permissions.None
		),
		ApplicationCommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		// Parse region argument, defaulting to the US if unspecified.
		string arg = args.ContainsKey(Arg_Region)
			? (string)args[Arg_Region]
			: Option_US;
		Module.Region region = arg switch {
			Option_US => Module.Region.US,
			Option_EU => Module.Region.EU,
			Option_KR => Module.Region.KR,
			Option_TW => Module.Region.TW,
			Option_CN => Module.Region.CN,
			_ => throw new ArgumentException("Unknown region selected.", nameof(args)),
		};

		// Fetch price display.
		DiscordEmbed? embed = await Module.DisplayPrices(region);

		// Notify if prices could not be fetched.
		if (embed is null) {
			string error =
				$"""
				:satellite: Could not fetch token prices.
				Wait a moment and try again?
				""";
			interaction.RegisterFinalResponse();
			await interaction.RespondCommandAsync(error);
			interaction.SetResponseSummary(error);
			return;
		}

		// Respond with price display.
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithEmbed(embed);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response);
		interaction.SetResponseSummary(embed.Title);
	}
}
