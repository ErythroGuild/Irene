namespace Irene.Commands;

using Module = Modules.WowToken;

class WowToken : CommandHandler {
	public const string
		CommandWowToken = "wow-token",
		ArgRegion = "region";
	public const string
		LabelUS = "US",
		LabelEU = "EU",
		LabelKR = "Korea",
		LabelTW = "Taiwan",
		LabelCN = "China";
	public const string
		OptionUS = "us",
		OptionEU = "eu",
		OptionKR = "kr",
		OptionTW = "tw",
		OptionCN = "cn";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}{Mention(CommandWowToken)} `[{ArgRegion}]` shows the latest token price.
		    If no region is specified, defaults to US prices.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandWowToken,
			"Show the latest token price.",
			AccessLevel.Guest,
			new List<DiscordCommandOption> { new (
				ArgRegion,
				"The region to show prices for.",
				ArgType.String,
				required: false,
				new List<DiscordCommandOptionEnum> {
					new (LabelUS, OptionUS),
					new (LabelEU, OptionEU),
					new (LabelKR, OptionKR),
					new (LabelTW, OptionTW),
					new (LabelCN, OptionCN),
				}
			) }
		),
		CommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		// Parse region argument, defaulting to the US if unspecified.
		string arg = args.ContainsKey(ArgRegion)
			? (string)args[ArgRegion]
			: OptionUS;
		Module.Region region = arg switch {
			OptionUS => Module.Region.US,
			OptionEU => Module.Region.EU,
			OptionKR => Module.Region.KR,
			OptionTW => Module.Region.TW,
			OptionCN => Module.Region.CN,
			_ => throw new ImpossibleArgException(ArgRegion, arg),
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
			await interaction.RegisterAndRespondAsync(error, true);
			return;
		}

		// Respond with price display.
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithEmbed(embed);
		await interaction.RegisterAndRespondAsync(response, embed.Title);
	}
}
