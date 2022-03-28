namespace Irene.Commands;

class Cap : ICommands {
	enum Type {
		Renown, Valor, Conquest, TowerKnowledge,
	}

	static readonly Dictionary<string, Type> dict_type = new () {
		{ "renown"         , Type.Renown         },
		{ "valor"          , Type.Valor          },
		{ "conquest"       , Type.Conquest       },
		{ "honor"          , Type.Conquest       },
		{ "tower knowledge", Type.TowerKnowledge },
		{ "towerknowledge" , Type.TowerKnowledge },
		{ "torghast"       , Type.TowerKnowledge },
	};
	static readonly Dictionary<Type, Action<DateTime, Command>> dict_func = new () {
		{ Type.Renown        , cap_renown   },
		{ Type.Valor         , cap_valor    },
		{ Type.Conquest      , cap_conquest },
		{ Type.TowerKnowledge, cap_torghast },
	};

	// Weekly increases
	static readonly int weekly_valor = 750;
	static readonly int weekly_conquest = 550;

	public static string help() {
		StringWriter text = new ();

		text.WriteLine("`@Irene -cap <type>` shows the current cap of the given resource.");
		text.WriteLine("E.g.: `renown`, `valor`, `conquest`.");

		return text.ToString();
	}

	// Parse the argument and dispatch the correct handler.
	public static void run(Command cmd) {
		// Argument is case-insensitive.
		string arg = cmd.args.Trim().ToLower();

		// Notify if no command is specified.
		if (arg == "") {
			log.info("  No cap type specified.");
			StringWriter text = new ();
			text.WriteLine("You must specify the cap you're looking for (e.g. `renown`).");
			text.WriteLine("See `@Irene -help cap` for more help.");
			_ = cmd.msg.RespondAsync(text.ToString());
			return;
		}

		// Notify if command not recognized.
		if (!dict_type.ContainsKey(arg)) {
			log.info($"  Cap type not recognized: {arg}");
			StringWriter text = new ();
			text.WriteLine("Could not recognize the requested info type.");
			text.WriteLine("See `@Irene -help cap` for more help.");
			_ = cmd.msg.RespondAsync(text.ToString());
			return;
		}

		// Dispatch the correct type of cap function.
		dict_func[dict_type[arg]](DateTime.UtcNow, cmd);
	}

	static void cap_renown(DateTime date, Command cmd) {
		// Pre-Shadowlands launch.
		if (date < ServerResetTime(Date_Patch902)) {
			log.warning("  Attempt to query renown cap pre-9.0.2.");
			_ = cmd.msg.RespondAsync("Renown did not take effect until Patch 9.0.2.");
			return;
		}

		// Patch 9.0.
		if (date < ServerResetTime(Date_Patch910)) {
			TimeSpan duration = date - ServerResetTime(Date_Patch902);
			int week = duration.Days / 7;  // int division!
			int cap = week switch {
				<  8 =>  3 + 3 * week,
				< 16 => 26 + 2 * (week - 8),
				_ => 40,
			};

			log.info($"  Current renown cap: {cap}, week {week + 1}");
			_ = cmd.msg.RespondAsync($"Current Renown cap: **{cap}** (week {week + 1})");
			return;
		}

		// Patch 9.1.
		if (date > ServerResetTime(Date_Patch910)) {
			TimeSpan duration = date - ServerResetTime(Date_Patch910);
			int week = duration.Days / 7;  // int division!
			int cap = week switch {
				<  1 => 42,
				<  9 => 45 + 3 * (week - 1),
				< 16 => 66 + 2 * (week - 9),
				_ => 80,
			};

			log.info($"  Current renowon cap: {cap}, week {week + 1}");
			_ = cmd.msg.RespondAsync($"Current Renown cap: **{cap}** (week {week + 1})");
			return;
		}
	}

	static void cap_valor(DateTime date, Command cmd) {
		// Pre-9.0.5 launch.
		if (date < ServerResetTime(Date_Patch905)) {
			log.warning("  Attempt to query valor cap pre-9.0.5.");
			_ = cmd.msg.RespondAsync("Valor did not take effect until Patch 9.0.5.");
			return;
		}

		// Season 1 (post-9.0.5).
		if (date < ServerResetTime(Date_Season2)) {
			TimeSpan duration = date - ServerResetTime(Date_Patch905);
			int week = duration.Days / 7;  // int division!
			int cap = 5000 + week * weekly_valor;

			log.info($"  Current valor cap: {cap}, week {week + 1}");
			_ = cmd.msg.RespondAsync($"Current Valor cap: **{cap}** (week {week + 1})");
			return;
		}

		// Season 2 (9.1.0).
		if (date > ServerResetTime(Date_Season2)) {
			TimeSpan duration = date - ServerResetTime(Date_Season2);
			int week = duration.Days / 7;  // int division!
			int cap = 750 + week * weekly_valor;

			log.info($"  Current valor cap: {cap}, week {week + 1}");
			_ = cmd.msg.RespondAsync($"Current Valor cap: **{cap}** (week {week + 1})");
			return;
		}
	}

	static void cap_conquest(DateTime date, Command cmd) {
		// Pre-Shadowlands launch.
		if (date < ServerResetTime(Date_Patch902)) {
			log.warning("  Attempt to query conquest cap pre-9.0.2.");
			_ = cmd.msg.RespondAsync("Shadowlands did not start until Patch 9.0.2.");
			return;
		}

		// Season 1 preseason (9.0.2).
		if (date < ServerResetTime(Date_Season1)) {
			log.info("  Current conquest cap: 0 (pre-season)");
			_ = cmd.msg.RespondAsync("Current Conquest cap: **0** (pre-season)");
			return;
		}

		// Season 1 (9.0.2).
		if (date < ServerResetTime(Date_Season2)) {
			TimeSpan duration = date - ServerResetTime(Date_Season1);
			int week = duration.Days / 7;  // int division!
			int cap = 550 + week * weekly_conquest;

			log.info($"  Current conquest cap: {cap}, week {week + 1}");
			_ = cmd.msg.RespondAsync($"Current Conquest cap: **{cap}** (week {week + 1})");
			return;
		}

		// Season 2 (9.1.0).
		if (date > ServerResetTime(Date_Season2)) {
			TimeSpan duration = date - ServerResetTime(Date_Season2);
			int week = duration.Days / 7;  // int division!
			int cap = 550 + week * weekly_conquest;

			log.info($"  Current conquest cap: {cap}, week {week + 1}");
			_ = cmd.msg.RespondAsync($"Current Conquest cap: **{cap}** (week {week + 1})");
			return;
		}
	}

	static void cap_torghast(DateTime date, Command cmd) {
		// Pre-Shadowlands launch.
		if (date < ServerResetTime(Date_Patch910)) {
			log.warning("  Attempt to query tower knowledge cap pre-9.1.0.");
			_ = cmd.msg.RespondAsync("Tower Knowledge did not take effect until Patch 9.1.0.");
			return;
		}

		// Patch 9.1.
		if (date > ServerResetTime(Date_Patch910)) {
			TimeSpan duration = date - ServerResetTime(Date_Patch910);
			int week = duration.Days / 7;  // int division!
			int cap = week switch {
				<  1 =>  180, // 90x2
				<  2 =>  400, // 90x2 + 110x2
				<  3 =>  700, // 90x2 + 110x2 + 125x2 ... + 50 ???
				< 10 =>  1060 + 360 * (week - 3),
				_ => 3510,
			};

			log.info($"  Current tower knowledge cap: {cap}, week {week + 1}");
			_ = cmd.msg.RespondAsync($"Current Tower Knowledge cap: **{cap}** (week {week + 1})");
			return;
		}
	}

	static DateTimeOffset ServerResetTime(DateOnly date) =>
		date.ToDateTime(Time_ServerReset.TimeOnly, DateTimeKind.Utc);
}
