namespace Irene.Commands;

using Module = Modules.Random;

class Roll : CommandHandler {
	public const string
		CommandRoll = "roll",
		ArgMin = "min",
		ArgMax = "max";
	// Valid argument ranges for min/max args.
	public const int
		ValueMin = 0,
		ValueMax = 1000000000; // 10^9 < 2^31, API uses signed int32

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}{Mention(CommandRoll)} `[{ArgMin}] [{ArgMax}]` generates a random number.
		{_t}This works the same way as `/roll` does in-game.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandRoll,
			"Generate a random number.",
			AccessLevel.Guest,
			new List<DiscordCommandOption> {
				new (
					ArgMin,
					"The lower bound (inclusive).",
					ArgType.Integer,
					required: false,
					minValue: ValueMin,
					maxValue: ValueMax
				),
				new (
					ArgMax,
					"The upper bound (inclusive).",
					ArgType.Integer,
					required: false,
					minValue: ValueMin,
					maxValue: ValueMax
				),
			}
		),
		CommandType.SlashCommand,
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, ParsedArgs args) {
		List<int> argList = new ();
		// `object` -> `long` -> `int` prevents an `InvalidCastException`.
		// This is because D#+ returns a `long`, even though the value
		// will always fit into an `int`.
		foreach (object arg in args.Values)
			argList.Add((int)(long)arg);

		string response = Module.SlashRoll(argList);
		await interaction.RegisterAndRespondAsync(response);
	}
}

class Random : CommandHandler {
	public const string
		CommandRandom = "random",
		CommandNumber = "number",
		CommandCoin   = "coin-flip",
		CommandCard   = "card",
		Command8Ball  = "8-ball",
		CommandAnswer = "answer",
		ArgQuestion   = "question",
		ArgShare      = "share";

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}{Mention(CommandRandom, CommandNumber)} is the same as `/roll`.
		{RankIcon(AccessLevel.Guest)}{Mention(CommandRandom, CommandCoin)} displays the result of a coin flip.
		{RankIcon(AccessLevel.Guest)}{Mention(CommandRandom, CommandCard)} draws a card from a standard deck.
		{RankIcon(AccessLevel.Guest)}{Mention(CommandRandom, Command8Ball)} `<{ArgQuestion}> [{ArgShare}]` emulates a Magic 8-Ball,
		{RankIcon(AccessLevel.Guest)}{Mention(CommandRandom, CommandAnswer)} `<{ArgQuestion}> [{ArgShare}]` emulates the "Book of Answers".
		{_t}The questions must be closed (e.g. yes/no) questions.
		{_t}If `[{ArgShare}]` isn't specified, the response will be private.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandRandom,
			"Generate a randomized outcome."
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				AccessLevel.Guest,
				new (
					CommandNumber,
					"Generate a random number.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							Roll.ArgMin,
							"The lower bound (inclusive).",
							ArgType.Integer,
							required: false,
							minValue: Roll.ValueMin,
							maxValue: Roll.ValueMax
						),
						new (
							Roll.ArgMax,
							"The upper bound (inclusive).",
							ArgType.Integer,
							required: false,
							minValue: Roll.ValueMin,
							maxValue: Roll.ValueMax
						),
					}
				),
				new (new Roll().RespondAsync)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandCoin,
					"Flip a coin.",
					ArgType.SubCommand
				),
				new (FlipCoinAsync)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandCard,
					"Draw a card.",
					ArgType.SubCommand
				),
				new (DrawCardAsync)
			),
			new (
				AccessLevel.Guest,
				new (
					Command8Ball,
					@"Forecast the answer to a yes/no question.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							ArgQuestion,
							@"The yes/no question to answer.",
							ArgType.String,
							required: true
						),
						new (
							ArgShare,
							"Whether the prediction is public.",
							ArgType.Boolean,
							required: false
						),
					}
				),
				new (Predict8BallAsync)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandAnswer,
					@"Forecast the answer to a yes/no question.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							ArgQuestion,
							@"The yes/no question to answer.",
							ArgType.String,
							required: true
						),
						new (
							ArgShare,
							"Whether the prediction is public.",
							ArgType.Boolean,
							required: false
						),
					}
				),
				new (PredictAnswerAsync)
			),
		}
	);

	public async Task FlipCoinAsync(Interaction interaction, ParsedArgs args) {
		CheckErythroInit();

		bool result = Module.FlipCoin();

		string response = (result switch {
			true  => Erythro.Emoji(id_e.heads),
			false => Erythro.Emoji(id_e.tails),
		}).ToString();
		string summary = result ? "Heads" : "Tails";
		await interaction.RegisterAndRespondAsync(response, summary);
	}

	public async Task DrawCardAsync(Interaction interaction, ParsedArgs args) {
		Module.PlayingCard card = Module.DrawCard();

		string suit = card.Suit switch {
			Module.Suit.Spades   => "\u2664", // white spade suit
			Module.Suit.Hearts   => "\u2661", // white heart suit
			Module.Suit.Diamonds => "\u2662", // white diamond suit
			Module.Suit.Clubs    => "\u2667", // white club suit
			Module.Suit.Joker    => "\U0001F0CF", // :black_joker:
			_ => throw new UnclosedEnumException(typeof(Module.Suit), card.Suit),
		};
		string value = card.Value ?? "";
		string response = (card.Suit != Module.Suit.Joker)
			? $"{suit} **{value}**"
			: suit;

		await interaction.RegisterAndRespondAsync(response);
	}

	public async Task Predict8BallAsync(Interaction interaction, ParsedArgs args) {
		string question = (string)args[ArgQuestion];
		bool doShare = args.ContainsKey(ArgShare)
			? (bool)args[ArgShare]
			: false;
		DateOnly today = DateOnly.FromDateTime(DateTime.Today);
		// Date doesn't need to be server time--the crystal ball works
		// in mysterious ways, after all.

		string response = Module.Magic8Ball(question, today);

		await interaction.RegisterAndRespondAsync(response, !doShare);
	}

	public async Task PredictAnswerAsync(Interaction interaction, ParsedArgs args) {
		string question = (string)args[ArgQuestion];
		bool doShare = args.ContainsKey(ArgShare)
			? (bool)args[ArgShare]
			: false;
		DateOnly today = DateOnly.FromDateTime(DateTime.Today);
		// Date doesn't need to be server time--the crystal ball works
		// in mysterious ways, after all.

		string response = Module.PickAnswer(question, today);

		await interaction.RegisterAndRespondAsync(response, !doShare);
	}
}
