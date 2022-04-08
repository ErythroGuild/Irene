namespace Irene.Commands;

class Cap : ICommand {
	private const string
		_optValor    = "valor",
		_optConquest = "conquest",
		_optRenown   = "renown",
		_optTorghast = "tower-knowledge";

	public static List<string> HelpPages { get =>
		new() { string.Join("\n", new List<string> {
			@"`/cap <type>` displays the current cap of the resource <type>,",
			"e.g. renown or valor."
		}) };
	}

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"cap",
				"Display the current cap of a resource.",
				new List<CommandOption> { new (
					"resource",
					"The type of resource to view the cap of.",
					ApplicationCommandOptionType.String,
					required: true,
					new List<CommandOptionEnum> {
						new ("Valor"   , _optValor   ),
						new ("Conquest", _optConquest),
						new ("Renown"  , _optRenown  ),
						new ("Tower Knowledge", _optTorghast),
					} ) },
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }
	public static List<AutoCompleteHandler> AutoComplete   { get => new (); }

	public static async Task RunAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
		// Select the correct invite to return.
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string type = (string)args[0].Value;
		InteractionHandler handler = type switch {
			_optValor    => CapValor,
			_optConquest => CapConquest,
			_optRenown   => CapRenown,
			_optTorghast => CapTorghast,
			_ => throw new ArgumentException("Invalid slash command parameter."),
		};

		// Delegate calculations to handlers.
		await handler(interaction, stopwatch);
	}

	private static async Task CapValor(DiscordInteraction interaction, Stopwatch stopwatch) {
		DateTime now = DateTime.Now;
		const int weekly_valor = 750;

		// Pre-9.0.5 launch.
		if (now < Date_Patch905.UtcResetTime()) {
			Log.Information("  Queried valor cap pre-9.0.5.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("You cannot earn Valor until Patch 9.0.5.");
			Log.Information("  Response sent.");
			return;
		}

		// Season 1 (post-9.0.5).
		if (now < Date_Season2.UtcResetTime()) {
			TimeSpan duration = now - Date_Patch905.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 5000 + week * weekly_valor;
			await SendResponseAsync("Valor", week + 1, cap, interaction, stopwatch);
			return;
		}

		// Season 2, pre-9.1.5.
		if (now < Date_Patch915.UtcResetTime()) {
			TimeSpan duration = now - Date_Season2.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 750 + week * weekly_valor;
			await SendResponseAsync("Valor", week + 1, cap, interaction, stopwatch);
			return;
		}

		// Season 2, post-9.1.5.
		if (now < Date_Season3.UtcResetTime()) {
			Log.Debug("  Queried valor cap post-9.1.5.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("Valor is **uncapped** from 9.1.5 ~ 9.2.");
			Log.Information("  Response sent.");
			return;
		}

		// Season 3.
		if (now >= Date_Season3.UtcResetTime()) {
			TimeSpan duration = now - Date_Season3.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 750 + week * weekly_valor;
			await SendResponseAsync("Valor", week + 1, cap, interaction, stopwatch);
			return;
		}
	}

	private static async Task CapConquest(DiscordInteraction interaction, Stopwatch stopwatch) {
		DateTime now = DateTime.Now;
		const int
			weekly_conquest_old = 550,
			weekly_conquest_new = 500;

		// Pre-Shadowlands launch.
		if (now < Date_Patch902.UtcResetTime()) {
			Log.Warning("  Queried conquest cap pre-9.0.2.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("Sorry, Conquest calculations are not supported until Patch 9.0.2.");
			Log.Information("  Response sent.");
			return;
		}

		// Season 1 preseason (9.0.2).
		if (now < Date_Season1.UtcResetTime()) {
			Log.Information("  Current conquest cap: 0 (pre-season).");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("Current Conquest cap: **0** (pre-season)");
			Log.Information("  Response sent.");
			return;
		}

		// Season 1 (9.0.2).
		if (now < Date_Season2.UtcResetTime()) {
			TimeSpan duration = now - Date_Season1.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 550 + week * weekly_conquest_old;
			await SendResponseAsync("Conquest", week + 1, cap, interaction, stopwatch);
			return;
		}

		// Season 2, pre-9.1.5.
		if (now < Date_Patch915.UtcResetTime()) {
			TimeSpan duration = now - Date_Season2.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 1000 + week * weekly_conquest_old;
			await SendResponseAsync("Conquest", week + 1, cap, interaction, stopwatch);
			return;
		}

		// Season 2, post-9.1.5.
		if (now < Date_Season3.UtcResetTime()) {
			Log.Debug("  Queried conquest cap post-9.1.5.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("Conquest is **uncapped** from 9.1.5 ~ 9.2.");
			Log.Information("  Response sent.");
			return;
		}

		// Season 3 (9.2.0).
		if (now >= Date_Season3.UtcResetTime()) {
			TimeSpan duration = now - Date_Season3.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 1000 + week * weekly_conquest_new;
			await SendResponseAsync("Conquest", week + 1, cap, interaction, stopwatch);
			return;
		}
	}

	private static async Task CapRenown(DiscordInteraction interaction, Stopwatch stopwatch) {
		DateTime now = DateTime.Now;

		// Pre-Shadowlands launch.
		if (now < Date_Patch902.UtcResetTime()) {
			Log.Information("  Queried renown cap pre-9.0.2.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("You cannot earn Renown until Patch 9.0.2.");
			Log.Information("  Response sent.");
			return;
		}

		// Patch 9.0.
		if (now < Date_Patch910.UtcResetTime()) {
			TimeSpan duration = now - Date_Patch902.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = week switch {
				<  8 => 3 + 3 * week,
				< 16 => 26 + 2 * (week - 8),
				_ => 40,
			};
			await SendResponseAsync("Renown", week + 1, cap, interaction, stopwatch);
			return;
		}

		// Patch 9.1.
		if (now < Date_Patch915.UtcResetTime()) {
			TimeSpan duration = now - Date_Patch910.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = week switch {
				<  1 => 42,
				<  9 => 45 + 3 * (week - 1),
				< 16 => 66 + 2 * (week - 9),
				_ => 80,
			};
			await SendResponseAsync("Renown", week + 1, cap, interaction, stopwatch);
			return;
		}

		// Patch 9.1.5.
		if (now >= Date_Patch915.UtcResetTime()) {
			Log.Information("  Queried renown cap post-9.1.5.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("Current Renown cap: **80** (max)");
			Log.Information("  Response sent.");
			return;
		}
	}

	private static async Task CapTorghast(DiscordInteraction interaction, Stopwatch stopwatch) {
		DateTime now = DateTime.Now;

		// Pre-Shadowlands launch.
		if (now < Date_Patch910.UtcResetTime()) {
			Log.Information("  Queried tower knowledge cap pre-9.1.0.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("You cannot earn Tower Knowledge until Patch 9.1.0.");
			Log.Information("  Response sent.");
			return;
		}

		// Patch 9.1.
		if (now < Date_Patch915.UtcResetTime()) {
			TimeSpan duration = now - Date_Patch910.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = week switch {
				<  1 => 180, // 90x2
				<  2 => 400, // 90x2 + 110x2
				<  3 => 700, // 90x2 + 110x2 + 125x2 ... + 50 ???
				< 10 => 1060 + 360 * (week - 3),
				_ => 3510,
			};
			await SendResponseAsync("Tower Knowledge", week + 1, cap, interaction, stopwatch);
			return;
		}

		// Patch 9.1.5.
		if (now >= Date_Patch915.UtcResetTime()) {
			Log.Information("  Queried tower knowledge cap post-9.1.5.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await interaction.RespondMessageAsync("Current Tower Knowledge cap: **3510** (max)");
			Log.Information("  Response sent.");
			return;
		}
	}

	// Convenience method for formatting/logging/sending cap data.
	private static async Task SendResponseAsync(
		string name,
		int week,
		int cap,
		DiscordInteraction interaction,
		Stopwatch stopwatch
	) {
		Log.Debug("  Sending resource cap.");
		stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
		await interaction.RespondMessageAsync($"Current {name} cap: **{cap}** (week {week})");
		Log.Information("  Response sent.");
		Log.Debug("    Sending week {WeekNum} {Resource} cap: {Cap}", week, name.ToLower(), cap);
		return;
	}
}
