using System.Timers;

using Component = DSharpPlus.Entities.DiscordComponent;
using MorraAi = Irene.Interactables.Minigames.AI.Morra;

using static Irene.Modules.Minigame;

namespace Irene.Interactables.Minigames;

class Morra {
	private enum State { Request, Selection, Tie, Result };
	private enum Outcome { ChallengerWins, OpponentWins, NeitherWins, Tie };

	// Table of all games to handle, indexed by the message ID of the
	// owning message.
	// This also serves as a way to "hold" fired timers, preventing them
	// from going out of scope and being destroyed.
	// NB: The same user can initiate multiple simultaneous games
	// (unlike non-game components, which are unique per-user).
	private static readonly ConcurrentDictionary<ulong, Morra> _requests = new ();
	private static readonly ConcurrentDictionary<ulong, Morra> _games = new ();

	private static readonly ReadOnlyDictionary<string, int> _idToChoice =
		new (new ConcurrentDictionary<string, int> {
			[_idButtonChoice1] = 1,
			[_idButtonChoice2] = 2,
			[_idButtonChoice3] = 3,
		} );
	private static readonly ReadOnlyDictionary<string, int> _idToGuess =
		new (new ConcurrentDictionary<string, int> {
			[_idButtonGuess2] = 2,
			[_idButtonGuess3] = 3,
			[_idButtonGuess4] = 4,
			[_idButtonGuess5] = 5,
			[_idButtonGuess6] = 6,
		} );

	private static readonly TimeSpan
		_timeoutConfirm = TimeSpan.FromMinutes(10),
		_timeoutMove    = TimeSpan.FromMinutes(15),
		_timeDisplayTie = TimeSpan.FromSeconds(4);
	private const string
		_idButtonDecline = "morra_decline",
		_idButtonAccept  = "morra_accept",
		_idButtonBlankL  = "morra_blank_L",
		_idButtonBlankR  = "morra_blank_R",
		_idButtonChoice1 = "morra_choice_1",
		_idButtonChoice2 = "morra_choice_2",
		_idButtonChoice3 = "morra_choice_3",
		_idButtonGuess2  = "morra_guess_2",
		_idButtonGuess3  = "morra_guess_3",
		_idButtonGuess4  = "morra_guess_4",
		_idButtonGuess5  = "morra_guess_5",
		_idButtonGuess6  = "morra_guess_6";
	private static readonly DiscordEmoji
		_emojiHand1 = DiscordEmoji.FromUnicode("\u0031\uFE0F\u20E3"),
		_emojiHand2 = DiscordEmoji.FromUnicode("\u0032\uFE0F\u20E3"),
		_emojiHand3 = DiscordEmoji.FromUnicode("\u0033\uFE0F\u20E3"),
		_emojiNumber2 = DiscordEmoji.FromUnicode("\u0032\uFE0F\u20E3"),
		_emojiNumber3 = DiscordEmoji.FromUnicode("\u0033\uFE0F\u20E3"),
		_emojiNumber4 = DiscordEmoji.FromUnicode("\u0034\uFE0F\u20E3"),
		_emojiNumber5 = DiscordEmoji.FromUnicode("\u0035\uFE0F\u20E3"),
		_emojiNumber6 = DiscordEmoji.FromUnicode("\u0036\uFE0F\u20E3");
	private const string
		_en      = "\u2002",
		_em      = "\u2003",
		_dash    = "\u2014",
		_space   = "\u200B",
		_dashedL = "\u21E0",
		_dashedR = "\u21E2",
		_arrowL  = "\u2190",
		_arrowR  = "\u2192",
		_unknown = ":game_die:",
		_plus    = "+",
		_loading = ":hourglass:",
		_loaded  = ":envelope:";

	public static void Init() { }
	static Morra() {
		// Handler for minigame requests.
		Client.ComponentInteractionCreated += (client, e) => {
			_ = Task.Run(async () => {
				ulong id = e.Message.Id;

				// Only handle interactions from a registered message.
				if (!_requests.ContainsKey(id))
					return;
				await e.Interaction.AcknowledgeComponentAsync();
				e.Handled = true;

				Morra game = _requests[id];
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

				Morra game = _games[id];
				// Only handle relevant users' interactions.
				if (e.User != game._challenger && e.User != game._opponent)
					return;
				game._timer.Stop();

				// Parse choice/guess and update display.
				if (_idToChoice.ContainsKey(e.Id)) {
					int choice = _idToChoice[e.Id];
					if (e.User == game._challenger)
						game._choiceChallenger = choice;
					if (e.User == game._opponent)
						game._choiceOpponent = choice;
				}
				if (_idToGuess.ContainsKey(e.Id)) {
					int guess = _idToGuess[e.Id];
					if (e.User == game._challenger)
						game._guessChallenger = guess;
					if (e.User == game._opponent)
						game._guessOpponent = guess;
				}
				await game.UpdateGame();
			});
			return Task.CompletedTask;
		};

		Log.Debug("  Created handler for minigame: Morra");
	}

	public static async Task RespondWithGame(
		TimedInteraction interaction,
		DiscordUser challenger,
		DiscordUser opponent
	) {
		// Create game object.
		Morra game = new (
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
				"Request sent: morra".AsLazy()
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
	private int?
		_choiceChallenger, _choiceOpponent,
		_guessChallenger, _guessOpponent;

	// Private constructor.
	// Use RPS.RespondWithGame() to create a new instance.
	private Morra(
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
		_guessChallenger = null;
		_guessOpponent = null;
	}

	// Throws an exception if either player hasn't selected
	// their choice yet.
	private Outcome CalculateOutcome() {
		if (_choiceChallenger is null ||
			_choiceOpponent is null ||
			_guessChallenger is null ||
			_guessOpponent is null
		) {
			throw new InvalidOperationException("Attempted to calculate outcome without data.");
		}

		int sum = _choiceChallenger.Value + _choiceOpponent.Value;
		bool is_challenger_correct = _guessChallenger == sum;
		bool is_opponent_correct = _guessOpponent == sum;

		return (is_challenger_correct, is_opponent_correct) switch {
			(true , false) => Outcome.ChallengerWins,
			(false, true ) => Outcome.OpponentWins,
			(false, false) => Outcome.NeitherWins,
			(true , true ) => Outcome.Tie,
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
				(_choiceOpponent, _guessOpponent) = await
					MorraAi.NextChoiceGuess(_challenger.Id);

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
			_choiceOpponent is not null &&
			_guessChallenger is not null &&
			_guessOpponent is not null
		) {
			_state = CalculateOutcome() switch {
				Outcome.ChallengerWins or
				Outcome.OpponentWins =>
					State.Result,
				Outcome.NeitherWins or
				Outcome.Tie =>
					State.Tie,
				_ => throw new InvalidOperationException("Unrecognized outcome."),
			};
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
			_choiceChallenger = null;
			_choiceOpponent = null;
			_guessChallenger = null;
			_guessOpponent = null;
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
			{_opponent.Mention}, {_challenger.Mention} has requested a game of **Morra**.
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
			.WithContent($"Request for **Morra** game with {_opponent.Mention} timed out.");

	private DiscordMessageBuilder GetGameSelection() {
		List<string> response = new () { "**Morra**" };

		string summary = $"{_challenger.Mention} vs {_opponent.Mention}";
		response.Add(summary);
		response.Add("");

		static string ValueIndicator(int? value) => (value is null)
			? _loading
			: _loaded;

		string status_guess = $"{_em}{_em}{_em}{_em}";
		status_guess += ValueIndicator(_guessChallenger);
		status_guess += $"  {_dashedR}  {_unknown}  {_dashedL}  ";
		status_guess += ValueIndicator(_guessOpponent);
		response.Add(status_guess);

		string status_choice = $"{_em}{_em}{_em}{_em}{_em}{_en} ";
		status_choice += ValueIndicator(_choiceChallenger);
		status_choice += $"{_en}{_en}{_en}";
		status_choice += ValueIndicator(_choiceOpponent);
		response.Add(status_choice);
		response.Add(_space);

		return new DiscordMessageBuilder()
			.WithContent(response.ToLines())
			.AddComponents(GetButtonsGuess(true))
			.AddComponents(GetButtonsChoice(true));
	}
	private DiscordMessageBuilder GetGameResult() {
		List<string> response = new () { "**Morra**" };

		string summary = $"{_challenger.Mention} vs {_opponent.Mention}";
		response.Add(summary);
		response.Add("");

		Outcome outcome = CalculateOutcome();
		if (_choiceChallenger is null || _choiceOpponent is null)
			throw new InvalidOperationException("Choices not complete.");
		int sum = _choiceChallenger.Value + _choiceOpponent.Value;

		string guesses = $"{_em}{_em}{_em}{_em}";
		guesses += _guessChallenger switch {
			2 => _emojiNumber2,
			3 => _emojiNumber3,
			4 => _emojiNumber4,
			5 => _emojiNumber5,
			6 => _emojiNumber6,
			_ => throw new InvalidOperationException("Invalid challenger guess."),
		};
		string arrow_outcome_L = outcome switch {
			Outcome.ChallengerWins => _arrowL,
			Outcome.OpponentWins   => _em,
			Outcome.NeitherWins    => _dash,
			Outcome.Tie            => _arrowL,
			_ => throw new InvalidOperationException("Unrecognized outcome."),
		};
		string sum_emoji = sum switch {
			2 => _emojiNumber2,
			3 => _emojiNumber3,
			4 => _emojiNumber4,
			5 => _emojiNumber5,
			6 => _emojiNumber6,
			_ => throw new InvalidOperationException("Invalid sum."),
		};
		string arrow_outcome_R = outcome switch {
			Outcome.ChallengerWins => _em,
			Outcome.OpponentWins   => _arrowR,
			Outcome.NeitherWins    => _dash,
			Outcome.Tie            => _arrowR,
			_ => throw new InvalidOperationException("Unrecognized outcome."),
		};
		guesses += $"  {arrow_outcome_L}  {sum_emoji}  {arrow_outcome_R}  ";
		guesses += _guessOpponent switch {
			2 => _emojiNumber2,
			3 => _emojiNumber3,
			4 => _emojiNumber4,
			5 => _emojiNumber5,
			6 => _emojiNumber6,
			_ => throw new InvalidOperationException("Invalid opponent guess."),
		};
		response.Add(guesses);

		string choices = $"{_em}{_em}{_em}{_em}{_em}{_en} ";
		choices += _choiceChallenger switch {
			1 => _emojiHand1,
			2 => _emojiHand2,
			3 => _emojiHand3,
			_ => throw new InvalidOperationException("Invalid challenger choice."),
		};
		choices += $"{_en}{_plus}{_en}";
		choices += _choiceOpponent switch {
			1 => _emojiHand1,
			2 => _emojiHand2,
			3 => _emojiHand3,
			_ => throw new InvalidOperationException("Invalid opponent choice."),
		};
		response.Add(choices);

		if (outcome == Outcome.Tie)
			response.Add(_space);
		else
			response.Add("");

		string result = outcome switch {
			Outcome.ChallengerWins => $"**{_challenger.Mention} wins!**",
			Outcome.OpponentWins   => $"**{_opponent.Mention} wins!**",
			Outcome.NeitherWins    => "**Both wrong!**",
			Outcome.Tie            => "**Tied!**",
			_ => throw new InvalidOperationException("Unrecognized outcome."),
		};
		if (_opponent == Client.CurrentUser) {
			result += outcome switch {
				Outcome.ChallengerWins => "   :triumph:",
				Outcome.OpponentWins => "   :innocent:",
				Outcome.NeitherWins => "",
				Outcome.Tie => " :hushed:",
				_ => "",
			};
		}
		response.Add(result);

		// Add note that games against Irene don't count for score.
		if (_opponent == Client.CurrentUser)
			response.Add("*Games against Irene do not count towards your record.*");

		// Add disabled game buttons if tie.
		// (Affordance that game will continue.)
		DiscordMessageBuilder message = new ();
		if (outcome == Outcome.Tie) {
			message = message
				.AddComponents(GetButtonsGuess(false))
				.AddComponents(GetButtonsChoice(false));
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
					GetRecord(_challenger.Id, Game.Morra);
			Record record_opponent =
					GetRecord(_opponent.Id, Game.Morra);

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
				Game.Morra,
				record_challenger
			);
			UpdateRecord(
				_opponent.Id,
				Game.Morra,
				record_opponent
			);
		}

		// If Irene was challenged, record her record.
		if (_opponent == Client.CurrentUser) {
			Record record_irene =
					GetRecord(Client.CurrentUser.Id, Game.Morra);
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
				Game.Morra,
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
	private static Component[] GetButtonsChoice(bool isEnabled=true) =>
		new Component[] {
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonBlankL,
				_en,
				disabled: true
			),
			new DiscordButtonComponent(
				ButtonStyle.Primary,
				_idButtonChoice1,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiHand1)
			),
			new DiscordButtonComponent(
				ButtonStyle.Primary,
				_idButtonChoice2,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiHand2)
			),
			new DiscordButtonComponent(
				ButtonStyle.Primary,
				_idButtonChoice3,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiHand3)
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonBlankR,
				_en,
				disabled: true
			),
		};
	private static Component[] GetButtonsGuess(bool isEnabled=true) =>
		new Component[] {
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonGuess2,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiNumber2)
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonGuess3,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiNumber3)
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonGuess4,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiNumber4)
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonGuess5,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiNumber5)
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				_idButtonGuess6,
				"",
				disabled: !isEnabled,
				emoji: new (_emojiNumber6)
			),
		};
}
