using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using DSharpPlus.Entities;

using static Irene.Program;

namespace Irene.Modules {
	using id_r = RoleIDs;
	using id_ch = ChannelIDs;
	using id_e = EmojiIDs;

	partial class WeeklyEvent {
		static void e_weekly_raid_info_announce() {

		}

		static void e_pin_affixes() {

		}

		static void e_raid1_day_announce() {

		}

		static void e_raid1_soon_announce() {

		}

		static void e_raid1_now_announce() {

		}

		static void e_raid2_day_announce() {

		}

		static void e_raid2_soon_announce() {

		}

		static void e_raid2_now_announce() {

		}

		static void e_raid_set_logs_remind() {

		}

		static void e_raid_break_remind() {

		}

		static void e_weekly_officer_meeting() {
			if (!is_guild_loaded) {
				log.error("Guild not loaded for event execution.");
				return;
			}

			StringWriter text = new ();
			text.WriteLine($"Weekly {roles[id_r.officer].Mention} meeting after raid. :slight_smile:");

			DiscordChannel ch = channels[id_ch.officer];
			_ = ch.SendMessageAsync(text.output());
		}

		static void e_update_raid_plans() {
			if (!is_guild_loaded) {
				log.error("Guild not loaded for event execution.");
				return;
			}

			StringWriter text = new ();
			text.WriteLine($"{roles[id_r.raidOfficer].Mention} -");
			text.WriteLine("  Decide on the raid plans for next week (if you haven't already). :ok_hand:");
			text.WriteLine("  `@Irene -raid-set`");

			DiscordChannel ch = channels[id_ch.officerBots];
			_ = ch.SendMessageAsync(text.output());
		}

		static void e_promote_remind() {
			if (!is_guild_loaded) {
				log.error("Guild not loaded for event execution.");
				return;
			}

			StringWriter text = new ();
			text.WriteLine($"{roles[id_r.recruiter].Mention} -");
			text.WriteLine("  Go over the 2-week+-trials this week (if there are any). :seedling:");
			text.WriteLine("  `@Irene -trials`");

			DiscordChannel ch = channels[id_ch.officerBots];
			_ = ch.SendMessageAsync(text.output());
		}
	}
}
