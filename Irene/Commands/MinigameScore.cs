using Irene.Components;

using static Irene.Modules.Minigame;

namespace Irene.Commands;

class MinigameScore : AbstractCommand {
	private record class MemberRecord(DiscordMember Member, Record Record);

	// Confirmation messages, indexed by the ID of the user who is
	// accessing them.
	private static readonly ConcurrentDictionary<ulong, Confirm> _confirms = new ();

	private const int _gamesSortByRate = 5;
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
		List<MemberRecord> games_members = new ();
		foreach (ulong id in games.Keys) {
			DiscordMember member;
			try {
				member = await Guild.GetMemberAsync(id);
			} catch {
				continue;
			}
			games_members.Add(new (member, games[id]));
		}
		games_members.Sort(LeaderboardSort);

		// Collate response.
		string leaderboard = PrintLeaderboard(game, games_members);

		// Send response.
		// Ensure nobody is @mentioned by passing an empty list.
		await Command.SubmitResponseAsync(
			handler.Interaction,
			new DiscordWebhookBuilder()
				.WithContent(leaderboard)
				.AddMentions(new List<IMention>()),
			"Sending leaderboard.",
			LogLevel.Debug,
			"{Game} leaderboard: {Count} entries".AsLazy(),
			DisplayName(game), games.Count
		);
	}
	private static int LeaderboardSort(MemberRecord x, MemberRecord y) {
		// Irene is always listed last.
		ulong id_irene = Client.CurrentUser.Id;
		if (x.Member == y.Member)
			return 0;
		else if (x.Member.Id == id_irene)
			return 1;
		else if (y.Member.Id == id_irene)
			return -1;

		// Memoize useful values.
		// Slightly less efficient but much more readable.
		int x_count = x.Record.Total;
		int y_count = y.Record.Total;
		int x_score = x.Record.Wins - x.Record.Losses;
		int y_score = y.Record.Wins - y.Record.Losses;
		int x_percent = (int)Math.Round(x.Record.Winrate);
		int y_percent = (int)Math.Round(y.Record.Winrate);

		// Above threshold count is always listed before
		// below threshold count.
		if (x_count < _gamesSortByRate && y_count < _gamesSortByRate) {
			// If scores match, sort by total games.
			if (x_score == y_score)
				return y_count - x_count;
			else
				return y_score - x_score;
		} else if (x_count < _gamesSortByRate) {
			return 1;
		} else if (y_count < _gamesSortByRate) {
			return -1;
		}

		// When above threshold, sort by winrate.
		// If winrates match (within 1%), sort by total games.
		if (x_percent == y_percent) {
			return y_count - x_count;
		} else {
			return y_percent - x_percent;
		}
	}
	private static string PrintLeaderboard(Game game, List<MemberRecord> records) {
		// Leaderboard title.
		const string tada = ":trophy:";
		List<string> leaderboard = new () {
			$"{tada}   **{DisplayName(game)}**   {tada}",
			"",
		};

		// Find highest win- and loss-counts.
		// This determines how much records need to be padded.
		int max_wins = 0;
		int max_losses = 0;
		foreach (MemberRecord record in records) {
			if (record.Record.Wins > max_wins)
				max_wins = record.Record.Wins;
			if (record.Record.Losses > max_losses)
				max_losses = record.Record.Losses;
		}
		int digits_wins = 1 + (max_wins / 10); // int division
		int digits_losses = 1 + (max_losses / 10); // int division

		// Using a regular for-loop because we care about the index.
		bool do_annotate = false;
		for (int i=0; i<records.Count; i++) {
			Record record = records[i].Record;
			DiscordMember member = records[i].Member;

			// Rank indicator.
			string rank = i switch {
				0 => ":first_place:",
				1 => ":second_place:",
				2 => ":third_place:",
				_ => $"`#{i+1}`",
			};
			if (member.Id == Client.CurrentUser.Id)
				rank = ":robot:";
			string line = $"{rank}    ";

			// Record.
			string wins = string.Format(
				string.Format("{{0,{0}}}", digits_wins),
				record.Wins
			);
			string losses = string.Format(
				string.Format("{{0,-{0}}}", digits_losses),
				record.Losses
			);
			const string dash = "\u2014";
			line += $"**`{wins}-{losses}`**   {dash} ";

			// Winrate.
			string rate = $"`{record.Winrate,4:p0}`";
			if (record.Total < _gamesSortByRate) {
				do_annotate = true;
				rate = rate.Italicize();
			}

			line += $"{rate}    {member.Mention}";
			leaderboard.Add(line);
		}

		// Annotate.
		if (do_annotate) {
			leaderboard.AddRange(new List<string> {
				"",
				$"*\\*Players with fewer than {_gamesSortByRate} games __not__ sorted by winrate.*",
			});
		}

		return leaderboard.ToLines();
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
			response += "\n" + $"""
				**{DisplayName(game)}**
				{indent}{record.Print().Bold()}  {dash} {record.Winrate:P0}
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
		string record_string = record.Print().Bold();
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
