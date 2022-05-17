using System.Timers;

using Component = DSharpPlus.Entities.DiscordComponent;
using RpsAi = Irene.Components.Minigames.AI.RPS;

using static Irene.Modules.Minigame;

namespace Irene.Components.Minigames;

class RPS {
	public enum Choice { Rock, Paper, Scissors };

	private enum State { Request, Selection, Tie, Result };
	private enum Outcome { ChallengerWins, OpponentWins, Tie };

	// Table of all games to handle, indexed by the message ID of the
	// owning message.
	// This also serves as a way to "hold" fired timers, preventing them
	// from going out of scope and being destroyed.
	// NB: The same user can initiate multiple simultaneous games
	// (unlike non-game components, which are unique per-user).
	private static readonly ConcurrentDictionary<ulong, RPS> _requests = new ();
	private static readonly ConcurrentDictionary<ulong, RPS> _games = new ();

	private static readonly ReadOnlyDictionary<string, Choice> _idToChoice =
		new (new ConcurrentDictionary<string, Choice> {
			[_idButtonRock    ] = Choice.Rock    ,
			[_idButtonPaper   ] = Choice.Paper   ,
			[_idButtonScissors] = Choice.Scissors,
		} );

	private static readonly TimeSpan
		_timeoutConfirm = TimeSpan.FromMinutes(10),
		_timeoutMove    = TimeSpan.FromMinutes(15),
		_timeDisplayTie = TimeSpan.FromSeconds(3);
	private const string
		_idButtonDecline  = "rps_decline",
		_idButtonAccept   = "rps_accept",
		_idButtonRock     = "rps_rock",
		_idButtonPaper    = "rps_paper",
		_idButtonScissors = "rps_scissors";
	private static readonly DiscordEmoji
		_emojiRockL     = DiscordEmoji.FromUnicode("\u270A"),     // :fist:
		_emojiRockR     = DiscordEmoji.FromUnicode("\u270A"),
		_emojiPaperL    = DiscordEmoji.FromUnicode("\U0001F590"), // :hand_splayed:
		_emojiPaperR    = DiscordEmoji.FromUnicode("\U0001F590"),
		_emojiScissorsL = DiscordEmoji.FromUnicode("\u270C"),     // :v:
		_emojiScissorsR = DiscordEmoji.FromUnicode("\u270C");
	private const string
		_en      = "\u2002",
		_dash    = "\u2014",
		_space   = "\u200B",
		_arrows  = "\u21C4",
		_arrowL  = "\u2190",
		_arrowR  = "\u2192",
		_arrowLR = "\u2194\u200D\uFE0E",
		_loading = ":hourglass:",
		_loaded  = ":envelope:";


	public static void Init() { }
	static RPS() {
		// Handler for minigame requests.
		Client.ComponentInteractionCreated += (client, e) => {
			_ = Task.Run(async () => {
				ulong id = e.Message.Id;

				// Only handle interactions from a registered message.
				if (!_requests.ContainsKey(id))
					return;
				await e.Interaction.AcknowledgeComponentAsync();
				e.Handled = true;

				RPS game = _requests[id];
				// Only handle relevant users' interactions.
				if (e.User != game._opponent)
					return;

				// Handle buttons.
				switch (e.Id) {
				case _idButtonDecline:
					await game.DeclineGame();
					break;
				case _idButtonAccept:
					await game.StartGame();
					break;
				}
			});
			return Task.CompletedTask;
		};

		// Handler for game interactions.
		Client.ComponentInteractionCreated += (client, e) => {
			_ = Task.Run(async () => {
				ulong id = e.Message.Id;

				// Only handle interactions from a registered message.
				if (!_games.ContainsKey(id))
					return;
				await e.Interaction.AcknowledgeComponentAsync();
				e.Handled = true;

				RPS game = _games[id];
				// Only handle relevant users' interactions.
				if (e.User != game._challenger && e.User != game._opponent)
					return;
				game._timer.Stop();

				// Parse choice and update display.
				Choice choice = _idToChoice[e.Id];
				if (e.User == game._challenger)
					game._choiceChallenger = choice;
				if (e.User == game._opponent)
					game._choiceOpponent = choice;
				await game.UpdateGame();
			});
			return Task.CompletedTask;
		};

		Log.Debug("  Created handler for minigame: RPS");
	}

	public static async Task RespondWithGame(
		TimedInteraction interaction,
		DiscordUser challenger,
		DiscordUser opponent
	) {
		// Create game object.
		RPS game = new (
			interaction.Interaction,
			challenger,
			opponent
		);

		// Send interaction response (request message).
		DiscordMessage message = 
			await Command.SubmitResponseAsync(
				interaction,
				game.GetRequest(),
				"Sending minigame request.",
				LogLevel.Debug,
				"Request sent: rock-paper-scissors".AsLazy()
			);
		game._requestId = message.Id;
		_requests.TryAdd(message.Id, game);
		game._timer.Start();

		// Start AI entry point (if needed).
		if (opponent == Client.CurrentUser) {
			_ = Task.Run(async () => {
				int msec = Random.Shared.Next(800, 1600);
				await Task.Delay(msec);

				// "Accept" game request.
				_ = game.StartGame();
			});
		}
	}

	// Instance properties.
	private DiscordMessageBuilder MessageGame => _state switch {
		State.Selection => GetGameSelection(),
		State.Tie       => GetGameResult(),
		State.Result    => GetGameResult(),
		_ => throw new InvalidOperationException("Invalid/unrecognized game state."),
	};

	// Instance fields (game state).
	private Timer _timer;
	private readonly DiscordInteraction _interaction;
	private ulong? _requestId;
	private DiscordMessage? _message;
	private readonly DiscordUser _challenger, _opponent;
	private State _state;
	private Choice? _choiceChallenger, _choiceOpponent;
	private int _ties;

	// Private constructor.
	// Use RPS.RespondWithGame() to create a new instance.
	private RPS(
		DiscordInteraction interaction,
		DiscordUser challenger,
		DiscordUser opponent
	) {
		// Set up timer.
		_timer = Util.CreateTimer(_timeoutConfirm, false);
		_timer.Elapsed += async (obj, e) =>
			await TimeoutRequest();

		// Set up the rest of the fields.
		_interaction = interaction;
		_requestId = null;
		_message = null;
		_challenger = challenger;
		_opponent = opponent;
		_state = State.Request;
		_choiceChallenger = null;
		_choiceOpponent = null;
		_ties = 0;
	}

	// Throws an exception if either player hasn't selected
	// their choice yet.
	private Outcome CalculateOutcome() {
		if (_choiceChallenger is null || _choiceOpponent is null)
			throw new InvalidOperationException("Attempted to calculate outcome without data.");

		if (_choiceChallenger == _choiceOpponent)
			return Outcome.Tie;

		return (_choiceChallenger, _choiceOpponent) switch {
			(Choice.Rock, Choice.Paper)     => Outcome.OpponentWins,
			(Choice.Rock, Choice.Scissors)  => Outcome.ChallengerWins,
			(Choice.Paper, Choice.Rock)     => Outcome.ChallengerWins,
			(Choice.Paper, Choice.Scissors) => Outcome.OpponentWins,
			(Choice.Scissors, Choice.Rock)  => Outcome.OpponentWins,
			(Choice.Scissors, Choice.Paper) => Outcome.ChallengerWins,
			_ => throw new InvalidOperationException("Failed to calculate outcome."),
		};
	}

	private async Task TimeoutRequest() {
		await _interaction
			.EditOriginalResponseAsync(GetRequestTimedOut());
		if (_requestId is not null)
			_requests.TryRemove(_requestId.Value, out _);
	}
	private async Task TimeoutGame() {
		if (_message is not null) {
			DiscordMessageBuilder message =
				await GetGameTimedOut();
			await _message.ModifyAsync(message);
			_games.TryRemove(_message.Id, out _);
		}
	}

	private async Task StartGame() {
		_timer.Stop();
		_state = State.Selection;

		// Update request message.
		await _interaction
			.EditOriginalResponseAsync(GetRequestAccepted());
		if (_requestId is not null)
			_requests.TryRemove(_requestId.Value, out _);

		// Send game message.
		if (_message is null) {
			DiscordMessage message = await _interaction
				.Channel.SendMessageAsync(GetGameSelection());
			_message = message;
			_games.TryAdd(message.Id, this);
		} else {
			await _message.ModifyAsync(GetGameSelection());
		}

		// Create new timer object.
		Timer timer = Util.CreateTimer(_timeoutMove, false);
		timer.Elapsed += async (obj, e) =>
			await TimeoutGame();
		_timer = timer;
		_timer.Start();

		// Start AI (if needed).
		if (_opponent == Client.CurrentUser) {
			_ = Task.Run(async () => {
				// Fetch choice (AI task also has a fuzzed delay).
				await Task.Delay(1200);
				Choice choice =
					await RpsAi.NextChoice(_challenger.Id);
				_choiceOpponent = choice;

				// Update game display.
				await UpdateGame();
			});
		}
	}
	private async Task DeclineGame() {
		_timer.Stop();
		await _interaction
			.EditOriginalResponseAsync(GetRequestDeclined());
		if (_requestId is not null)
			_requests.TryRemove(_requestId.Value, out _);
	}
	private async Task UpdateGame() {
		_timer.Stop();

		// Update state if both sides have chosen.
		if (_choiceChallenger is not null &&
			_choiceOpponent is not null
		) {
			_state = (CalculateOutcome() == Outcome.Tie)
				? State.Tie
				: State.Result;
		}

		if (_message is not null) {
			// Update game display.
			await _message.ModifyAsync(MessageGame);

			// Remove game if not tied.
			if (_state == State.Result)
				_games.TryRemove(_message.Id, out _);

			// Restart timer if still pending a choice.
			if (_state == State.Selection)
				_timer.Start();
		}

		// Restart game if tied.
		if (_state == State.Tie) {
			_ties++;
			_choiceChallenger = null;
			_choiceOpponent = null;
			await Task.Delay(_timeDisplayTie)
				.ContinueWith(async (t) => {
					await StartGame();
				});
		}

		// Update scores if complete.
		if (_state == State.Result)
			UpdateScores();
	}

	private DiscordWebhookBuilder GetRequest() {
		string response =
			$"""
			{_opponent.Mention}, {_challenger.Mention} has requested a game of Rock-Paper-Scissors.
			*You can check rules at any time with `/minigame rules`.*
			""";
		return new DiscordWebhookBuilder()
			.WithContent(response)
			.AddComponents(GetButtonsConfirm());
	}
	private DiscordWebhookBuilder GetRequestAccepted() =>
		new DiscordWebhookBuilder()
			.WithContent($"*Request accepted by* {_opponent.Mention}.");
	private DiscordWebhookBuilder GetRequestDeclined() =>
		new DiscordWebhookBuilder()
			.WithContent($"*Request declined by* {_opponent.Mention}.");
	private DiscordWebhookBuilder GetRequestTimedOut() =>
		new DiscordWebhookBuilder()
			.WithContent($"Request for Rock-Paper-Scissors game with {_opponent.Mention} timed out.");

	private DiscordMessageBuilder GetGameSelection() {
		List<string> response = new () { "**Rock Paper Scissors**" };

		string summary = $"{_challenger.Mention} vs {_opponent.Mention}";
		summary += _ties switch {
			0 => "",
			1 => $"{_en}{_dash}{_en}__1 tie__",
			2 => $"{_en}{_dash}{_en}__{_ties} ties__",
			_ => $"{_en}{_dash}{_en}__**{_ties} ties!**__",
		};
		response.Add(summary);
		response.Add("");

		string status = $"{_en}{_en}{_en}{_en}{_en}{_en}";
		status += (_choiceChallenger is null)
			? _loading
			: _loaded;
		status += $"  {_arrows}  ";
		status += (_choiceOpponent is null)
			? _loading
			: _loaded;
		response.Add(status);
		response.Add(_space);

		return new DiscordMessageBuilder()
			.WithContent(response.ToLines())
			.AddComponents(GetButtonsGame(true));
	}
	private DiscordMessageBuilder GetGameResult() {
		List<string> response = new () { "**Rock Paper Scissors**" };

		string summary = $"{_challenger.Mention} vs {_opponent.Mention}";
		summary += _ties switch {
			0 => "",
			1 => $"{_en}{_dash}{_en}__1 tie__",
			2 => $"{_en}{_dash}{_en}__{_ties} ties__",
			_ => $"{_en}{_dash}{_en}__**{_ties} ties!**__",
		};
		response.Add(summary);
		response.Add("");

		Outcome outcome = CalculateOutcome();
		string status = $"{_en}{_en}{_en}{_en}{_en}{_en}";
		status += _choiceChallenger switch {
			Choice.Rock     => _emojiRockL,
			Choice.Paper    => _emojiPaperL,
			Choice.Scissors => _emojiScissorsL,
			_ => throw new InvalidOperationException("Invalid challenger choice."),
		};
		string arrow_outcome = outcome switch {
			Outcome.ChallengerWins => _arrowR,
			Outcome.OpponentWins   => _arrowL,
			Outcome.Tie            => _arrowLR,
			_ => throw new InvalidOperationException("Unrecognized outcome."),
		};
		status += $"  {arrow_outcome}  ";
		status += _choiceOpponent switch {
			Choice.Rock     => _emojiRockR,
			Choice.Paper    => _emojiPaperR,
			Choice.Scissors => _emojiScissorsR,
			_ => throw new InvalidOperationException("Invalid opponent choice."),
		};
		response.Add(status);
		if (outcome == Outcome.Tie)
			response.Add(_space);
		else
			response.Add("");

		string result = outcome switch {
			Outcome.ChallengerWins => $"**{_challenger.Mention} wins!**",
			Outcome.OpponentWins   => $"**{_opponent.Mention} wins!**",
			Outcome.Tie            => "**Tied!**",
			_ => throw new InvalidOperationException("Unrecognized outcome."),
		};
		if (_opponent == Client.CurrentUser && outcome == Outcome.OpponentWins)
			result += " :innocent:";
		response.Add(result);

		// Add disabled game buttons if tie.
		// (Affordance that game will continue.)
		DiscordMessageBuilder message = new ();
		if (outcome == Outcome.Tie) {
			message =
				message.AddComponents(GetButtonsGame(false));
		}

		// Return response.
		message = message.WithContent(response.ToLines());
		return message;
	}
	private async Task<DiscordMessageBuilder> GetGameTimedOut() {
		string content = "";
		if (_message is not null) {
			_message = await
				_message.Channel.GetMessageAsync(_message.Id, true);
			content = _message.Content + "\n";
		}
		content += "*Game timed out.*\n(start a fresh game with `/minigame play`)";
		return new DiscordMessageBuilder().WithContent(content);
	}

	private void UpdateScores() {
		Outcome outcome = CalculateOutcome();

		// Only record both players if opponent wasn't Irene.
		if (_opponent != Client.CurrentUser) {
			Record record_challenger =
					GetRecord(_challenger.Id, Game.RPS);
			Record record_opponent =
					GetRecord(_opponent.Id, Game.RPS);

			switch (outcome) {
			case Outcome.ChallengerWins:
				record_challenger.Wins++;
				record_opponent.Losses++;
				break;
			case Outcome.OpponentWins:
				record_opponent.Wins++;
				record_challenger.Losses++;
				break;
			}

			UpdateRecord(
				_challenger.Id,
				Game.RPS,
				record_challenger
			);
			UpdateRecord(
				_opponent.Id,
				Game.RPS,
				record_opponent
			);
		}

		// If Irene was challenged, record her record.
		if (_opponent == Client.CurrentUser) {
			Record record_irene =
					GetRecord(Client.CurrentUser.Id, Game.RPS);
			switch (outcome) {
			case Outcome.ChallengerWins:
				record_irene.Losses++;
				break;
			case Outcome.OpponentWins:
				record_irene.Wins++;
				break;
			}
			UpdateRecord(
				Client.CurrentUser.Id,
				Game.RPS,
				record_irene
			);
		}
	}

	private static Component[] GetButtonsConfirm() =>
		new Component[] {
			new DiscordButtonComponent(
				ButtonStyle.Danger,
				_idButtonDecline,
				"Decline"
			),
			new DiscordButtonComponent(
				ButtonStyle.Success,
				_idButtonAccept,
				"Accept"
			),
		};
	private static Component[] GetButtonsGame(bool isEnabled=true) =>
		new Component[] {
			new DiscordButtonComponent(
				ButtonStyle.Primary,
				_idButtonRock,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiRockL)
			),
			new DiscordButtonComponent(
				ButtonStyle.Primary,
				_idButtonPaper,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiPaperL)
			),
			new DiscordButtonComponent(
				ButtonStyle.Primary,
				_idButtonScissors,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiScissorsL)
			),
		};
}
