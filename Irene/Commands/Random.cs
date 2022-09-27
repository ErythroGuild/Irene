using Module = Irene.Modules.Random;

namespace Irene.Commands;

class Roll : CommandHandler {
	public const string
		Command_Roll = "roll",
		Arg_Min = "min",
		Arg_Max = "max";
	// Valid argument ranges for min/max args.
	public const int
		Value_Min = 0,
		Value_Max = 1000000000; // 10^9 < 2^31, API uses signed int32

	public Roll(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention(Command_Roll)} generates a number between `1` and `100`,
		{Command.Mention(Command_Roll)} `<{Arg_Max}>` generates a number between `1` and `{Arg_Max}`,
		{Command.Mention(Command_Roll)} `<{Arg_Min}> <{Arg_Max}>` generates a number between `{Arg_Min}` and `{Arg_Max}`.
		    All ranges are inclusive, e.g. `[7, 23]`.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Roll,
			"Generate a random number.",
			new List<CommandOption> {
				new (
					Arg_Min,
					"The lower bound (inclusive).",
					ApplicationCommandOptionType.Integer,
					required: false,
					minValue: Value_Min,
					maxValue: Value_Max
				),
				new (
					Arg_Max,
					"The upper bound (inclusive).",
					ApplicationCommandOptionType.Integer,
					required: false,
					minValue: Value_Min,
					maxValue: Value_Max
				),
			},
			Permissions.None
		),
		RespondAsync
	);

	public async Task RespondAsync(Interaction interaction, IDictionary<string, object> args) {
		List<int> argList = new ();
		// `object` -> `long` -> `int` prevents an `InvalidCastException`.
		// This is because D#+ returns a `long`, even though the value
		// will always fit into an `int`.
		foreach (object arg in args.Values)
			argList.Add((int)(long)arg);
		string response = Module.SlashRoll(argList);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response);
		interaction.SetResponseSummary(response);
	}
}

class Random : CommandHandler {
	public const string
		Command_Random = "random",
		Command_Number = "number",
		Command_Coin   = "coin-flip",
		Command_Card   = "card",
		Command_8Ball  = "8-ball",
		Arg_Question   = "question",
		Arg_Share      = "share";

	public Random(GuildData erythro) : base (erythro) { }

	public override string HelpText =>
		$"""
		{Command.Mention($"{Command_Random} {Command_Number}")} functions the same as `/roll`.
		{Command.Mention($"{Command_Random} {Command_Coin}")} displays the result of a coin flip.
		{Command.Mention($"{Command_Random} {Command_Card}")} draws a card from a standard deck.
		{Command.Mention($"{Command_Random} {Command_8Ball}")} `<{Arg_Question}> [{Arg_Share}]` forecasts the answer to a yes/no question.
		    If `[{Arg_Share}]` isn't specified, the response will be private.
		""";

	public override CommandTree CreateTree() => new (
		new (
			Command_Random,
			"Generate a randomized outcome.",
			Permissions.None
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				new (
					Command_Number,
					"Generate a random number.",
					ApplicationCommandOptionType.SubCommand,
					options: new List<CommandOption> {
						new (
							Roll.Arg_Min,
							"The lower bound (inclusive).",
							ApplicationCommandOptionType.Integer,
							required: false,
							minValue: Roll.Value_Min,
							maxValue: Roll.Value_Max
						),
						new (
							Roll.Arg_Max,
							"The upper bound (inclusive).",
							ApplicationCommandOptionType.Integer,
							required: false,
							minValue: Roll.Value_Min,
							maxValue: Roll.Value_Max
						),
					}
				),
				new (new Roll(Erythro).RespondAsync)
			),
			new (
				new (
					Command_Coin,
					"Flip a coin.",
					ApplicationCommandOptionType.SubCommand
				),
				new (FlipCoinAsync)
			),
			new (
				new (
					Command_Card,
					"Draw a card.",
					ApplicationCommandOptionType.SubCommand
				),
				new (DrawCardAsync)
			),
			new (
				new (
					Command_8Ball,
					@"Forecast the answer to a yes/no question.",
					ApplicationCommandOptionType.SubCommand,
					options: new List<CommandOption> {
						new (
							Arg_Question,
							@"The yes/no question to answer.",
							ApplicationCommandOptionType.String,
							required: true
						),
						new (
							Arg_Share,
							"Whether the prediction is public.",
							ApplicationCommandOptionType.Boolean,
							required: false
						),
					}
				),
				new (Predict8BallAsync)
			),
		}
	);

	public async Task FlipCoinAsync(Interaction interaction, IDictionary<string, object> _) {
		bool result = Module.FlipCoin();
		string response = (result switch {
			true  => Erythro.Emoji(id_e.heads),
			false => Erythro.Emoji(id_e.tails),
		}).ToString();
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response);
		interaction.SetResponseSummary(result ? "Heads" : "Tails");
	}

	public async Task DrawCardAsync(Interaction interaction, IDictionary<string, object> _) {
		Module.PlayingCard card = Module.DrawCard();
		string suit = card.Suit switch {
			Module.Suit.Spades   => "\u2664", // white spade suit
			Module.Suit.Hearts   => "\u2661", // white heart suit
			Module.Suit.Diamonds => "\u2662", // white diamond suit
			Module.Suit.Clubs    => "\u2667", // white club suit
			Module.Suit.Joker    => "\U0001F0CF", // :black_joker:
			_ => throw new InvalidOperationException("Invalid card drawn."),
		};
		string value = card.Value ?? "";
		string response = (card.Suit != Module.Suit.Joker)
			? $"{suit} **{value}**"
			: suit;

		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response);
		interaction.SetResponseSummary(response);
	}

	public async Task Predict8BallAsync(Interaction interaction, IDictionary<string, object> args) {
		string question = (string)args[Arg_Question];
		bool doShare = args.ContainsKey(Arg_Share)
			? (bool)args[Arg_Share]
			: false;
		DateOnly today = DateOnly.FromDateTime(DateTime.Today);
		// Date doesn't need to be server time--the crystal ball works
		// in mysterious ways, after all.

		string response = Module.Magic8Ball(question, today);
		interaction.RegisterFinalResponse();
		await interaction.RespondCommandAsync(response, !doShare);
		interaction.SetResponseSummary(response);
	}
}
