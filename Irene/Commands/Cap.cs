using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using static Irene.Program;

namespace Irene.Commands {
	using id_r = RoleIDs;
	using id_ch = ChannelIDs;
	using id_e = EmojiIDs;

	class Cap : ICommands {
		enum Type {
			Renown, Valor, Conquest,
		}

		static readonly Dictionary<string, Type> dict_type = new () {
			{ "renown"  , Type.Renown   },
			{ "valor"   , Type.Valor    },
			{ "conquest", Type.Conquest },
			{ "honor"   , Type.Conquest },
		};
		static readonly Dictionary<Type, Action<DateTime, Command>> dict_func = new () {
			{ Type.Renown  , cap_renown   },
			{ Type.Valor   , cap_valor    },
			{ Type.Conquest, cap_conquest },
		};

		// Weekly increases
		static readonly int weekly_valor = 750;
		static readonly int weekly_conquest = 550;

		// Patch release days
		// 7:00 PST = 8:00 PDT = 15:00 UTC
		static readonly DateTime date_patch_902 = new DateTime(2020, 11, 17, 15, 0, 0, DateTimeKind.Utc);
		static readonly DateTime date_season_1  = new DateTime(2020, 12,  8, 15, 0, 0, DateTimeKind.Utc);
		static readonly DateTime date_patch_905 = new DateTime(2021,  3,  9, 15, 0, 0, DateTimeKind.Utc);
		static readonly DateTime date_patch_910 = new DateTime(2021,  6, 29, 15, 0, 0, DateTimeKind.Utc);
		static readonly DateTime date_season_2  = new DateTime(2021,  7,  6, 15, 0, 0, DateTimeKind.Utc);

		public static string help() {
			StringWriter text = new ();

			text.WriteLine("`@Irene -cap <type>` shows the current cap of the given resource.");
			text.WriteLine("E.g.: `renown`, `valor`, `conquest`.");

			text.Flush();
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
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());
				return;
			}

			// Notify if command not recognized.
			if (!dict_type.ContainsKey(arg)) {
				log.info($"  Cap type not recognized: {arg}");
				StringWriter text = new ();
				text.WriteLine("Could not recognize the requested info type.");
				text.WriteLine("See `@Irene -help cap` for more help.");
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());
				return;
			}

			// Dispatch the correct type of cap function.
			dict_func[dict_type[arg]](DateTime.UtcNow, cmd);
		}

		static void cap_renown(DateTime date, Command cmd) {
			// Pre-Shadowlands launch.
			if (date < date_patch_902) {
				log.warning("  Attempt to query renown cap pre-9.0.2.");
				_ = cmd.msg.RespondAsync("Renown did not take effect until Patch 9.0.2.");
				return;
			}

			// Patch 9.0.
			if (date < date_patch_910) {
				TimeSpan duration = date - date_patch_902;
				int week = duration.Days / 7;
				int cap;
				if (week < 8) {
					cap = 3 + week * 3;
				} else if (week < 16) {
					cap = 26 + (week - 8) * 2;
				} else {
					cap = 40;
				}

				log.info($"  Current renowon cap: {cap}, week {week + 1}");
				_ = cmd.msg.RespondAsync($"Current Renown cap: **{cap}** (week {week + 1})");
				return;
			}

			// Patch 9.1.
			if (date > date_patch_910) {
				TimeSpan duration = date - date_patch_910;
				int week = duration.Days / 7;
				int cap;
				if (week < 8) {
					cap = 43 + week * 3;
				} else if (week < 16) {
					cap = 66 + (week - 8) * 2;
				} else {
					cap = 80;
				}

				log.info($"  Current renowon cap: {cap}, week {week + 1}");
				_ = cmd.msg.RespondAsync($"Current Renown cap: **{cap}** (week {week + 1})");
				return;
			}
		}

		static void cap_valor(DateTime date, Command cmd) {
			// Pre-9.0.5 launch.
			if (date < date_patch_905) {
				log.warning("  Attempt to query valor cap pre-9.0.5.");
				_ = cmd.msg.RespondAsync("Valor did not take effect until Patch 9.0.5.");
				return;
			}

			// Season 1 (post-9.0.5).
			if (date < date_patch_910) {
				TimeSpan duration = date - date_patch_905;
				int week = duration.Days / 7;  // int division!
				int cap = 5000 + week * weekly_valor;

				log.info($"  Current valor cap: {cap}, week {week + 1}");
				_ = cmd.msg.RespondAsync($"Current Valor cap: **{cap}** (week {week + 1})");
				return;
			}

			// Season 2 preseason (9.1.0).
			if (date < date_season_2) {
				log.info("  Current valor cap: 0 (pre-season)");
				_ = cmd.msg.RespondAsync("Current Valor cap: **0** (pre-season)");
				return;
			}

			// Season 2 (9.1.0).
			if (date > date_season_2) {
				TimeSpan duration = date - date_season_2;
				int week = duration.Days / 7;  // int division!
				int cap = 750 + week * weekly_valor;

				log.info($"  Current valor cap: {cap}, week {week + 1}");
				_ = cmd.msg.RespondAsync($"Current Valor cap: **{cap}** (week {week + 1})");
				return;
			}
		}

		static void cap_conquest(DateTime date, Command cmd) {
			// Pre-Shadowlands launch.
			if (date < date_patch_902) {
				log.warning("  Attempt to query conquest cap pre-9.0.2.");
				_ = cmd.msg.RespondAsync("Shadowlands did not start until Patch 9.0.2.");
				return;
			}

			// Season 1 preseason (9.0.2).
			if (date < date_season_1) {
				log.info("  Current conquest cap: 0 (pre-season)");
				_ = cmd.msg.RespondAsync("Current Conquest cap: **0** (pre-season)");
				return;
			}

			// Season 1 (9.0.2).
			if (date < date_patch_910) {
				TimeSpan duration = date - date_season_1;
				int week = duration.Days / 7;  // int division!
				int cap = 550 + week * weekly_conquest;

				log.info($"  Current conquest cap: {cap}, week {week + 1}");
				_ = cmd.msg.RespondAsync($"Current Conquest cap: **{cap}** (week {week + 1})");
				return;
			}

			// Season 2 preseason (9.1.0).
			if (date < date_season_2) {
				log.info("  Current conquest cap: 0 (pre-season)");
				_ = cmd.msg.RespondAsync("Current Conquest cap: **0** (pre-season)");
				return;
			}

			// Season 2 (9.1.0).
			if (date > date_season_2) {
				TimeSpan duration = date - date_season_2;
				int week = duration.Days / 7;  // int division!
				int cap = 550 + week * weekly_conquest;

				log.info($"  Current conquest cap: {cap}, week {week + 1}");
				_ = cmd.msg.RespondAsync($"Current Conquest cap: **{cap}** (week {week + 1})");
				return;
			}
		}
	}
}
