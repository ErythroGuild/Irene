using System;
using System.Collections.Generic;
using System.IO;

using DSharpPlus.Entities;

using static Irene.Const;
using static Irene.Program;

namespace Irene.Modules {
	using id_r = RoleIDs;
	using id_ch = ChannelIDs;
	using id_e = EmojiIDs;

	partial class WeeklyEvent {
		static Dictionary<Raid.Date, ulong> msgs_raid_forming = new ();

		public static void update_raid_logs(Raid raid) {
			if (!is_guild_loaded) {
				log.warning("    Could not update announcement post: guild not loaded.");
				return;
			}
			if (!msgs_raid_forming.ContainsKey(raid.date)) {
				log.warning("    Could not update announcement post: announcement not posted.");
				return;
			}

			// Prepare data.
			ulong msg_id = msgs_raid_forming[raid.date];
			DiscordMessage msg =
				channels[id_ch.announce]
				.GetMessageAsync(msg_id)
				.Result;
			string content = msg.Content;
			string
				emoji_logs     = emojis[id_e.warcraftlogs].ToString(),
				emoji_wipefest = emojis[id_e.wipefest    ].ToString(),
				emoji_analyzer = emojis[id_e.wowanalyzer ].ToString();

			// Update message content with new log links.
			StringReader text_in = new StringReader(content);
			StringWriter text_out = new ();
			string? line;
			do {
				line = text_in.ReadLine();
				if (line is not null &&
					line != "" &&
					!line.StartsWith(emoji_logs) &&
					!line.StartsWith(emoji_wipefest) &&
					!line.StartsWith(emoji_analyzer)
				) {
					text_out.WriteLine(line);
				}
			} while (line is not null);
			if (raid.log_id is not null) {
				text_out.WriteLine();
				text_out.WriteLine($"{emoji_wipefest} - <{raid.get_link_wipefest()}>");
				text_out.WriteLine($"{emoji_analyzer} - <{raid.get_link_analyzer()}>");
				text_out.WriteLine($"{emoji_logs} - <{raid.get_link_logs()}>");
			}

			// Update message.
			msg.ModifyAsync(text_out.output());
		}

		const string
			path_meme_ch = @"data/memes.txt";
		const string
			em = "\u2003";

		static void e_cycle_meme_ch() {
			if (!is_guild_loaded) {
				log.error("Guild not loaded for event execution.");
				return;
			}

			// Read in all non-empty lines.
			List<string> names = new (File.ReadAllLines(path_meme_ch));
			foreach(string name_i in names) {
				if (name_i == "") {
					names.Remove(name_i);
				}
			}

			// Randomly select a name.
			// Creating a new PRNG each time is suboptimal, but for our
			// needs here it suffices.
			Random rng = new ();
			int i = rng.Next(names.Count);
			string name = names[i];

			channels[id_ch.memes].ModifyAsync(ch => ch.Name = name);
		}

		static void e_weekly_raid_info_announce() {

		}

		static void e_pin_affixes() {

		}

		static void e_raid_day_announce() {

		}

		static async void e_raid_soon_announce() {
			if (!is_guild_loaded) {
				log.warning("Could not announce raid: guild not loaded.");
				return;
			}

			// Find/construct raid object.
			int week = Raid.current_week();
			Raid.Day? day = DateTimeOffset.Now.DayOfWeek switch {
				DayOfWeek.Friday   => Raid.Day.Fri,
				DayOfWeek.Saturday => Raid.Day.Sat,
				_ => null,
			};
			if (day is null) {
				log.error("Raid announcements can only made on Fri/Sat.");
				log.endl();
				return;
			}
			Raid raid = Raid.get(week, (Raid.Day)day)
				?? new Raid(week, (Raid.Day) day);

			// Send raid announcement.
			DateTimeOffset time = DateTimeOffset.Now + t_raid_now_announce;
			StringWriter text = new ();
			text.Write($"{raid.emoji()} {roles[id_r.raid].Mention} - ");
			text.Write($"Forming for raid ~{time.timestamp("R")}.");
			if (raid.summary is not null) {
				text.WriteLine(raid.summary);
			}
			DiscordMessage msg = await
				channels[id_ch.announce]
				.SendMessageAsync(text.output());

			// React to raid announcement.
			await msg.CreateReactionAsync(DiscordEmoji.FromName(irene, raid.emoji()));
		}

		static async void e_raid_now_announce() {
			if (!is_guild_loaded) {
				log.warning("Could not announce raid: guild not loaded.");
				return;
			}

			// Find/construct raid object.
			int week = Raid.current_week();
			Raid.Day? day = DateTimeOffset.Now.DayOfWeek switch {
				DayOfWeek.Friday   => Raid.Day.Fri,
				DayOfWeek.Saturday => Raid.Day.Sat,
				_ => null,
			};
			if (day is null) {
				log.error("Raid announcements can only made on Fri/Sat.");
				return;
			}
			Raid raid = Raid.get(week, (Raid.Day)day)
				?? new Raid(week, (Raid.Day) day);

			// Send raid announcement.
			StringWriter text = new ();
			text.WriteLine($"{raid.emoji()} {roles[id_r.raid]} - Forming now!");
			if (raid.summary is not null) {
				text.WriteLine(raid.summary);
			}
			DiscordMessage msg = await
				channels[id_ch.announce]
				.SendMessageAsync(text.output());
			msgs_raid_forming.Add(raid.date, msg.Id);

			// React to raid announcement.
			await msg.CreateReactionAsync(DiscordEmoji.FromName(irene, raid.emoji()));
			await msg.CreateReactionAsync(emojis[id_e.kyrian]);
			await msg.CreateReactionAsync(emojis[id_e.necrolord]);
			await msg.CreateReactionAsync(emojis[id_e.nightfae]);
			await msg.CreateReactionAsync(emojis[id_e.venthyr]);
		}

		static void e_raid_set_logs_remind() {
			if (!is_guild_loaded) {
				log.error("Guild not loaded for event execution.");
				return;
			}

			StringWriter text = new ();
			text.WriteLine($"{roles[id_r.raidOfficer].Mention} -");
			text.WriteLine($"{em}Reminder to set logs for tonight. :ok_hand: :card_box:");
			text.WriteLine($"{em}`@Irene -logs-set`");

			DiscordChannel ch = channels[id_ch.officerBots];
			_ = ch.SendMessageAsync(text.output());
		}

		static void e_raid_break_remind() {
			if (!is_guild_loaded) {
				log.error("Guild not loaded for event execution.");
				return;
			}

			StringWriter text = new ();
			text.WriteLine($"{roles[id_r.raidOfficer].Mention} -");
			text.WriteLine($"{em}Raid break soon. :slight_smile: :tropical_drink:");

			DiscordChannel ch = channels[id_ch.officer];
			_ = ch.SendMessageAsync(text.output());
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
			text.WriteLine($"{em}Decide on the raid plans for next week (if you haven't already). :ok_hand:");
			text.WriteLine($"{em}`@Irene -raid-set-F`, `@Irene -raid-set-S`");

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
			text.WriteLine($"{em}Go over the 2-week+-trials this week (if there are any). :seedling:");
			text.WriteLine($"{em}`@Irene -trials`");

			DiscordChannel ch = channels[id_ch.officerBots];
			_ = ch.SendMessageAsync(text.output());
		}
	}
}
