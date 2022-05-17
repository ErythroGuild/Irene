using Irene.Components;

using static Irene.Modules.Minigame;

namespace Irene.Commands;

class MinigameScore : AbstractCommand {
	// Confirmation messages, indexed by the ID of the user who is
	// accessing them.
	private static readonly ConcurrentDictionary<ulong, Confirm> _confirms = new ();

	private const string
		_commandLeaderboard = "leaderboard",
		_commandPersonal    = "personal",
		_commandReset       = "reset";

	public override List<string> HelpPages =>
		new () { new List<string> {
			@"`/minigame-score leaderboard <game>` displays the leaderboard for a game.",
			@"`/minigame-score personal [share]` displays your personal records.",
			@"`/minigame-score reset <game>` resets your personal record for a game.",
			@"See also: `/help minigame`.",
		}.ToLines() };

	public override List<InteractionCommand> SlashCommands =>
		new () {
			new ( new (
				"minigame-score",
				"Check cumulative win-loss records of minigames.",
				options: new List<CommandOption> {
					new (
						_commandLeaderboard,
						"Display a game leaderboard.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"game",
							"The type of game to view.",
							ApplicationCommandOptionType.String,
							required: true,
							Minigame.GameOptions
						), }
					),
					new (
						_commandPersonal,
						"Display personal records.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"share",
							"Whether to make visible to everyone.",
							ApplicationCommandOptionType.Boolean,
							required: false
						), }
					),
					new (
						_commandReset,
						"Reset personal records.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"game",
							"The game to reset records for.",
							ApplicationCommandOptionType.String,
							required: true,
							Minigame.GameOptions
						), }
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), DeferAsync, RunAsync )
		};

	public static async Task DeferAsync(TimedInteraction interaction) {
		DeferrerHandler handler = new (interaction, true);
		DeferrerHandlerFunc function = GetDeferrerHandler(handler);
		await function(handler);
	}
	public static async Task RunAsync(TimedInteraction interaction) {
		DeferrerHandler handler = new (interaction, false);
		DeferrerHandlerFunc function = GetDeferrerHandler(handler);
		await function(handler);
	}
	private static DeferrerHandlerFunc GetDeferrerHandler(DeferrerHandler handler) {
		List<DiscordInteractionDataOption> args =
			handler.GetArgs();
		return args[0].Name switch {
			_commandLeaderboard => ViewLeaderboardAsync,
			_commandPersonal    => ViewPersonalAsync,
			_commandReset       => ResetPersonalAsync,
			_ => throw new ArgumentException("Unrecognized subcommand.", nameof(handler)),
		};
	}

	private static async Task ViewLeaderboardAsync(DeferrerHandler handler) {
		await AwaitGuildInitAsync();

		// Leaderboard is always non-ephemeral.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, false);
			return;
		}

		// Parse requested game.
		List<DiscordInteractionDataOption> args =
			handler.GetArgs()[0].GetArgs();
		Game game = Minigame.OptionToGame((string)args[0].Value);

		Dictionary<ulong, Record> games = new (GetRecords(game));

		// Handle empty leaderboard.
		if (games.Count == 0) {
			string response_empty = $"""
				The leaderboard for {DisplayName(game).Bold()} is empty. :leaves:
				Start a game with `/minigame play`?
				""";
			await Command.SubmitResponseAsync(
				handler.Interaction,
				response_empty,
				"Requested leaderboard is empty.",
				LogLevel.Debug,
				"Empty leaderboard sent: {Game}".AsLazy(),
				DisplayName(game)
			);
			return;
		}

		// Hydrate and sort data.
		List<(Record, DiscordMember)> games_members = new ();
		foreach (ulong id in games.Keys) {
			DiscordMember member;
			try {
				member = await Guild.GetMemberAsync(id);
			} catch {
				continue;
			}
			games_members.Add((games[id], member));
		}
		games_members.Sort(
			((Record, DiscordMember) x, (Record, DiscordMember) y) => {
				// Irene is always listed last.
				ulong id_irene = Client.CurrentUser.Id;
				if (x.Item2 == y.Item2)
					return 0;
				else if (x.Item2.Id == id_irene)
					return 1;
				else if (y.Item2.Id == id_irene)
					return -1;

				// Above threshold count is always listed before
				// below threshold count.
				// When under threshold, sort by score.
				const int threshold = 5;
				int x_count = x.Item1.Wins + x.Item1.Losses;
				int y_count = y.Item1.Wins + y.Item1.Losses;
				if (x_count < threshold && y_count < threshold) {
					int x_val = x.Item1.Wins - x.Item1.Losses;
					int y_val = y.Item1.Wins - y.Item1.Losses;
					return y_val - x_val;
				} else if (x_count < threshold) {
					return -1;
				} else if (y_count < threshold) {
					return 1;
				}

				// When above threshold, sort by rate.
				float x_rate = (float)x.Item1.Wins / x_count;
				float y_rate = (float)y.Item1.Wins / y_count;
				return Math.Sign(y_rate - x_rate);
			}
		);

		// Collate response.
		const string dash = "\u2014";
		List<string> response = new ();
		int i = 0;
		foreach ((Record, DiscordMember) entry in games_members) {
			(Record record, DiscordMember member) = entry;
			i++;
			string line = $"`#{i}`  ";
			line += record.Serialize().Bold();
			line += $"  {dash} ";
			float rate = (float)record.Wins
				/ (record.Wins + record.Losses);
			line += (rate * 100).ToString("F0"); // 0 decimal places
			line += $"%    {member.Mention}";
			response.Add(line);
		}

		// Send response.
		// Ensure nobody is @mentioned by passing an empty list.
		await Command.SubmitResponseAsync(
			handler.Interaction,
			new DiscordWebhookBuilder()
				.WithContent(response.ToLines())
				.AddMentions(new List<IMention>()),
			"Sending leaderboard.",
			LogLevel.Debug,
			"{Game} leaderboard: {Count} entries".AsLazy(),
			DisplayName(game),
			response.Count
		);
	}

	private static async Task ViewPersonalAsync(DeferrerHandler handler) {
		// Parse relevant data.
		ulong id = handler.Interaction.Interaction.User.Id;
		List<DiscordInteractionDataOption> args =
			handler.GetArgs()[0].GetArgs();
		bool do_share = (args.Count > 0)
			? (bool)args[0].Value
			: false;

		// Defer according to `do_share` argument.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, !do_share);
			return;
		}

		Dictionary<Game, Record> records = new (GetRecords(id));

		// Special case if records are empty.
		if (records.Count == 0) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"""
				You have no records yet. :leaves:
				Start a game with `/minigame play`?
				""",
				"No personal records to display.",
				LogLevel.Debug,
				"No records sent.".AsLazy()
			);
			return;
		}

		// Construct response.
		const string
			indent = "\u2003",
			dash   = "\u2014";
		string response = "*Your personal records:*";
		foreach (Game game in records.Keys) {
			Record record = records[game];
			float winrate = (float)record.Wins
				/ (record.Wins + record.Losses);
			winrate *= 100;
			response += "\n" + $"""
				**{DisplayName(game)}**
				{indent}{record.Serialize().Bold()}  {dash} {winrate:F0}%
				""";
		}

		// Send response.
		await Command.SubmitResponseAsync(
			handler.Interaction,
			response,
			"Personal records sent.",
			LogLevel.Debug,
			"Records sent: {Count}".AsLazy(),
			records.Count
		);
	}

	private static async Task ResetPersonalAsync(DeferrerHandler handler) {
		// Records reset is always ephemeral.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, true);
			return;
		}

		// Parse relevant data.
		ulong id = handler.Interaction.Interaction.User.Id;
		List<DiscordInteractionDataOption> args =
			handler.GetArgs()[0].GetArgs();
		Game game = Minigame.OptionToGame((string)args[0].Value);

		// Check if record needs to be reset.
		Record record = GetRecord(id, game);
		if (record == Record.Empty) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				$"You have no records for {DisplayName(game)} yet.\nNo changes made.",
				"No minigame score record reset needed.",
				LogLevel.Debug,
				"Record for {Game} already empty.".AsLazy(),
				DisplayName(game)
			);
			return;
		}

		// Send confirmation for record reset.
		MessagePromise message_promise = new ();
		string game_name = DisplayName(game).Bold();
		string record_string = record.Serialize().Bold();
		Confirm confirm = Confirm.Create(
			handler.Interaction.Interaction,
			ResetRecord,
			message_promise.Task,
			$"Are you sure you want to reset your current record for {game_name}? ({record_string})",
			$"Your {game_name} record was reset.",
			$"Your {game_name} record was not reset.",
			$"Reset this record", "Nevermind"
		);

		// Disable any confirms already in-flight.
		if (_confirms.ContainsKey(id)) {
			await _confirms[id].Discard();
			_confirms.TryRemove(id, out _);
		}
		_confirms.TryAdd(id, confirm);

		// Record removal callback.
		Task ResetRecord(bool doContinue, ComponentInteractionCreateEventArgs e) {
			// Remove confirm from table.
			_confirms.TryRemove(e.User.Id, out _);

			if (!doContinue) {
				Log.Debug("  Record not changed (request canceled).");
				return Task.CompletedTask;
			}

			Modules.Minigame.ResetRecord(id, game);

			Log.Information("  Record reset (request confirmed).");
			return Task.CompletedTask;
		}

		// Respond.
		DiscordMessage message = await Command.SubmitResponseAsync(
			handler.Interaction,
			confirm.WebhookBuilder,
			"Record reset confirmation requested.",
			LogLevel.Information,
			"Game: {Game}".AsLazy(),
			DisplayName(game)
		);
		message_promise.SetResult(message);
	}
}
