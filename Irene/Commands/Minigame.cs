using Irene.Components.Minigames;

using static Irene.Modules.Minigame;

using MinigameFunc = System.Func<
	Irene.TimedInteraction,
	DSharpPlus.Entities.DiscordUser,
	DSharpPlus.Entities.DiscordUser,
	System.Threading.Tasks.Task
>;

namespace Irene.Commands;

class Minigame : AbstractCommand {
	public static ReadOnlyCollection<CommandOptionEnum> GameOptions =>
		new (new List<CommandOptionEnum> {
			new (DisplayName(Game.RPS    ), _optionRPS    ),
			new (DisplayName(Game.RPSLS  ), _optionRPSLS  ),
			//new (DisplayName(Game.Morra  ), _optionMorra  ),
			//new (DisplayName(Game.Balloon), _optionBalloon),
			//new (DisplayName(Game.Duel   ), _optionDuel   ),
			//new (DisplayName(Game.Duel2  ), _optionDuel2  ),
		} );

	public static Game OptionToGame(string id) => id switch {
		_optionRPS     => Game.RPS,
		_optionRPSLS   => Game.RPSLS,
		_optionMorra   => Game.Morra,
		_optionBalloon => Game.Balloon,
		_optionDuel    => Game.Duel,
		_optionDuel2   => Game.Duel2,
		_ => throw new ArgumentException("Unknown ID.", nameof(id)),
	};

	private const string
		_commandPlay = "play",
		_commandRules = "rules";
	private const string
		_optionRPS     = "rps"    ,
		_optionRPSLS   = "rpsls"  ,
		_optionMorra   = "morra"  ,
		_optionBalloon = "balloon",
		_optionDuel    = "duel"   ,
		_optionDuel2   = "duel2"  ;

	public override List<string> HelpPages =>
		new () { new List<string> {
			@"`/minigame play <game> <opponent>` initiates a game against an opponent,",
			@"`/minigame rules <game>` displays the rules for a game.",
			"See also: `/help minigame-score`.",
		}.ToLines() };

	public override List<InteractionCommand> SlashCommands =>
		new () {
			new ( new (
				"minigame",
				"Play a minigame against somebody.",
				options: new List<CommandOption> {
					new (
						_commandPlay,
						"Play a game against somebody else.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								"game",
								"The type of game to play.",
								ApplicationCommandOptionType.String,
								required: true,
								GameOptions
							),
							new (
								"opponent",
								"Who to play against.",
								ApplicationCommandOptionType.User,
								required: true
							),
						}
					),
					new (
						_commandRules,
						"Display the rules for a game.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"game",
							"The game to view rules for.",
							ApplicationCommandOptionType.String,
							required: true,
							GameOptions
						) }
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), DeferAsync, RunAsync )
		};

	public static async Task DeferAsync(TimedInteraction interaction) =>
		await MinigameAsync(new (interaction, true));
	public static async Task RunAsync(TimedInteraction interaction) =>
		await MinigameAsync(new (interaction, false));

	// Both commands are handled together.
	// `/minigame rules` is processed immediately.
	public static async Task MinigameAsync(DeferrerHandler handler) {
		List<DiscordInteractionDataOption> args =
			handler.GetArgs();
		string command = args[0].Name;

		// Parse selected game.
		args = args[0].GetArgs();
		Game game = ToGame((string)args[0].Value);

		// Send rules for rules command.
		if (command == _commandRules) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				GetRules(game),
				$"Sending rules for {game}.",
				LogLevel.Debug,
				"Rules sent.".AsLazy()
			);
			return;
		}

		// Parse users involved.
		DiscordUser? challenger =
			handler.Interaction.Interaction.User;
		DiscordUser opponent =
			handler.Interaction.Interaction.GetTargetMember();

		// The only bot opponent allowed is Irene.
		if (opponent.IsBot && opponent != Client.CurrentUser) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Sorry, the only bot you can challenge is me. :feather:",
				$"Attempted to challenge (non-Irene) bot.",
				LogLevel.Information,
				"Attempted to challenge {Bot}.".AsLazy(),
				opponent.Tag()
			);
			return;
		}

		// You cannot play against yourself.
		if (challenger == opponent) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"You cannot play against yourself. :exploding_head:",
				$"Game requested against self.",
				LogLevel.Information,
				"User: {UserTag}.".AsLazy(),
				challenger.Tag()
			);
			return;
		}

		// After this point, message will always be visible.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, false);
			return;
		}

		// Determine game to respond with.
		MinigameFunc function_game = game switch {
			Game.RPS => RPS.RespondWithGame,
			Game.RPSLS => RPSLS.RespondWithGame,
			_ => throw new NotImplementedException("That game hasn't been implemented yet."),
		};
		await function_game.Invoke(
			handler.Interaction,
			challenger,
			opponent
		);
	}

	public static Game ToGame(string option) => OptionToGame(option);
}
