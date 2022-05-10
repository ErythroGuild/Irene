namespace Irene.Commands;

class Cap : ICommand {
	private static readonly DateOnly
		_date_s3CapRemoved = new (2022,  5, 10);

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
			), DeferAsync, RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }
	public static List<AutoCompleteHandler> AutoComplete   { get => new (); }

	public static async Task DeferAsync(TimedInteraction interaction) {
		DeferrerHandlerFunc function =
			GetDeferrerHandler(interaction);
		await function(new (interaction, true));
	}
	public static async Task RunAsync(TimedInteraction interaction) {
		DeferrerHandlerFunc function =
			GetDeferrerHandler(interaction);
		await function(new (interaction, false));
	}
	private static DeferrerHandlerFunc GetDeferrerHandler(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string type = (string)args[0].Value;
		return type switch {
			_optValor    => CapValor,
			_optConquest => CapConquest,
			_optRenown   => CapRenown,
			_optTorghast => CapTorghast,
			_ => throw new ArgumentException("Invalid slash command parameter."),
		};
	}

	private static async Task CapValor(DeferrerHandler handler) {
		DateTime now = DateTime.Now;
		const int weekly_valor = 750;

		// Pre-9.0.5 launch.
		if (now < Date_Patch905.UtcResetTime()) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"You cannot earn Valor until Patch 9.0.5.",
				"Queried valor cap pre-9.0.5.",
				LogLevel.Information,
				"Valor cap: unavailable".AsLazy()
			);
			return;
		}

		// Deferrer is non-ephemeral for the rest.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, false);
			return;
		}

		// Season 1 (post-9.0.5).
		if (now < Date_Season2.UtcResetTime()) {
			TimeSpan duration = now - Date_Patch905.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 5000 + week * weekly_valor;
			await SendResponseAsync("Valor", week + 1, cap, handler);
			return;
		}

		// Season 2, pre-9.1.5.
		if (now < Date_Patch915.UtcResetTime()) {
			TimeSpan duration = now - Date_Season2.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 750 + week * weekly_valor;
			await SendResponseAsync("Valor", week + 1, cap, handler);
			return;
		}

		// Season 2, post-9.1.5.
		if (now < Date_Season3.UtcResetTime()) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Valor is **uncapped** for the rest of season 2.",
				"Queried valor cap post-9.1.5.",
				LogLevel.Debug,
				"Valor cap: uncapped".AsLazy()
			);
			return;
		}

		// Season 3, pre-cap removal.
		if (now < _date_s3CapRemoved.UtcResetTime()) {
			TimeSpan duration = now - Date_Season3.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 750 + week * weekly_valor;
			await SendResponseAsync("Valor", week + 1, cap, handler);
			return;
		}
		
		// Season 3, post-cap removal.
		if (now >= _date_s3CapRemoved.UtcResetTime()) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Valor is **uncapped** for the rest of season 3.",
				"Queried valor cap in 9.2 post-removal.",
				LogLevel.Debug,
				"Valor cap: uncapped".AsLazy()
			);
			return;
		}
	}

	private static async Task CapConquest(DeferrerHandler handler) {
		DateTime now = DateTime.Now;
		const int
			weekly_conquest_old = 550,
			weekly_conquest_new = 500;

		// Pre-Shadowlands launch.
		if (now < Date_Patch902.UtcResetTime()) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Sorry, Conquest calculations are not supported until Patch 9.0.2.",
				"Queried conquest cap pre-9.0.2.",
				LogLevel.Information,
				"Conquest cap: unavailable".AsLazy()
			);
			return;
		}

		// Deferrer is non-ephemeral for the rest.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, false);
			return;
		}

		// Season 1 preseason (9.0.2).
		if (now < Date_Season1.UtcResetTime()) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Current Conquest cap: **0** (pre-season)",
				"Queried conquest cap during 9.0 pre-season.",
				LogLevel.Debug,
				"Conquest cap: 0".AsLazy()
			);
			return;
		}

		// Season 1 (9.0.2).
		if (now < Date_Season2.UtcResetTime()) {
			TimeSpan duration = now - Date_Season1.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 550 + week * weekly_conquest_old;
			await SendResponseAsync("Conquest", week + 1, cap, handler);
			return;
		}

		// Season 2, pre-9.1.5.
		if (now < Date_Patch915.UtcResetTime()) {
			TimeSpan duration = now - Date_Season2.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 1000 + week * weekly_conquest_old;
			await SendResponseAsync("Conquest", week + 1, cap, handler);
			return;
		}

		// Season 2, post-9.1.5.
		if (now < Date_Season3.UtcResetTime()) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Conquest is **uncapped** for the rest of season 2.",
				"Queried conquest cap post-9.1.5.",
				LogLevel.Debug,
				"Conquest cap: uncapped".AsLazy()
			);
			return;
		}

		// Season 3, pre-cap removal.
		if (now < _date_s3CapRemoved.UtcResetTime()) {
			TimeSpan duration = now - Date_Season3.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = 1000 + week * weekly_conquest_new;
			await SendResponseAsync("Conquest", week + 1, cap, handler);
			return;
		}

		// Season 3, post-cap removal.
		if (now >= _date_s3CapRemoved.UtcResetTime()) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Conquest is **uncapped** for the rest of season 3.",
				"Queried conquest cap in 9.2 post-removal.",
				LogLevel.Debug,
				"Conquest cap: uncapped".AsLazy()
			);
			return;
		}
	}

	private static async Task CapRenown(DeferrerHandler handler) {
		DateTime now = DateTime.Now;

		// Pre-Shadowlands launch.
		if (now < Date_Patch902.UtcResetTime()) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"You cannot earn Renown until Patch 9.0.2.",
				"Queried renown cap pre-9.0.2.",
				LogLevel.Information,
				"Renown cap: unavailable".AsLazy()
			);
			return;
		}

		// Deferrer is non-ephemeral for the rest.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, false);
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
			await SendResponseAsync("Renown", week + 1, cap, handler);
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
			await SendResponseAsync("Renown", week + 1, cap, handler);
			return;
		}

		// Patch 9.1.5.
		if (now >= Date_Patch915.UtcResetTime()) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Current Renown cap: **80** (max)",
				"Queried conquest cap post-9.1.5.",
				LogLevel.Debug,
				"Renown cap: 80".AsLazy()
			);
			return;
		}
	}

	private static async Task CapTorghast(DeferrerHandler handler) {
		DateTime now = DateTime.Now;

		// Pre-Shadowlands launch.
		if (now < Date_Patch910.UtcResetTime()) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"You cannot earn Tower Knowledge until Patch 9.1.0.",
				"Queried tower knowledge cap pre-9.1.0.",
				LogLevel.Information,
				"Tower knowledge cap: unavailable".AsLazy()
			);
			return;
		}

		// Deferrer is non-ephemeral for the rest.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, false);
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
			await SendResponseAsync("Tower Knowledge", week + 1, cap, handler);
			return;
		}

		// Patch 9.1.5.
		if (now >= Date_Patch915.UtcResetTime()) {
			await Command.SubmitResponseAsync(
				handler.Interaction,
				"Current Tower Knowledge cap: **3510** (max)",
				"Queried tower knowledge cap post-9.1.5.",
				LogLevel.Debug,
				"Tower knowledge cap: 3510".AsLazy()
			);
			return;
		}
	}

	// Convenience method for formatting/logging/sending cap data.
	private static async Task SendResponseAsync(
		string name,
		int week,
		int cap,
		DeferrerHandler handler
	) =>
		await Command.SubmitResponseAsync(
			handler.Interaction,
			$"Current {name} cap: **{cap}** (week {week})",
			"Sending resource cap.",
			LogLevel.Debug,
			new Lazy<string>(() =>
				$"Week {week} {name.ToLower()} cap: {cap}"
			)
		);
}
