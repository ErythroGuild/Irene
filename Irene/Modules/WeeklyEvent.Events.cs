namespace Irene.Modules;

using TimestampStyle = Util.TimestampStyle;

partial class WeeklyEvent {
	static Dictionary<Raid.Date, ulong> msgs_raid_forming = new ();

	public static void update_raid_logs(Raid raid) {
		if (Guild is null) {
			Log.Warning("    Could not update announcement post: guild not loaded.");
			return;
		}
		if (!msgs_raid_forming.ContainsKey(raid.date)) {
			Log.Warning("    Could not update announcement post: announcement not posted.");
			return;
		}
		if (raid.log_id is null) {
			Log.Debug("  Log ID not set.");
			return;
		}

		// Prepare data.
		ulong msg_id = msgs_raid_forming[raid.date];
		DiscordMessage msg =
			Channels[id_ch.announce]
			.GetMessageAsync(msg_id)
			.Result;
		string content = msg.Content;
		string
			emoji_logs     = Emojis[id_e.warcraftlogs].ToString(),
			emoji_wipefest = Emojis[id_e.wipefest    ].ToString(),
			emoji_analyzer = Emojis[id_e.wowanalyzer ].ToString();

		// Update message content with new log links.
		StringReader text_in = new (content);
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
		text_out.WriteLine();
		text_out.WriteLine($"{emoji_wipefest} - <{raid.get_link_wipefest()}>");
		text_out.WriteLine($"{emoji_analyzer} - <{raid.get_link_analyzer()}>");
		text_out.WriteLine($"{emoji_logs} - <{raid.get_link_logs()}>");

		// Update message.
		msg.ModifyAsync(text_out.ToString());
	}

	const string
		path_meme_ch = @"data/memes.txt";
	const string
		em = "\u2003";

	static void e_cycle_meme_ch() {
		if (Guild is null) {
			Log.Error("  Guild not loaded for event execution.");
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

		Channels[id_ch.memes].ModifyAsync(ch => ch.Name = name);
	}

	static void e_weekly_raid_info_announce() {

	}

	static void e_pin_affixes() {

	}

	static void e_raid_day_announce() {

	}

	static async void e_raid_soon_announce() {
		if (Guild is null) {
			Log.Warning("  Could not announce raid: guild not loaded.");
			return;
		}

		// Find/construct raid object.
		int week = Raid.CurrentWeek;
		Raid.Day? day = DateTimeOffset.Now.DayOfWeek switch {
			DayOfWeek.Friday   => Raid.Day.Fri,
			DayOfWeek.Saturday => Raid.Day.Sat,
			_ => null,
		};
		if (day is null) {
			Log.Error("  Raid announcements can only made on Fri/Sat.");
			return;
		}
		Raid raid = Raid.get(week, (Raid.Day)day)
			?? new Raid(week, (Raid.Day) day);

		// Send raid announcement.
		DateTimeOffset now = DateTimeOffset.Now;
		DateTimeOffset time_forming = now - now.TimeOfDay + t_raid_now_announce;
		DateTimeOffset time_raid = now - now.TimeOfDay + Raid.Time.ToTimeSpan();
		StringWriter text = new ();
		text.Write($"{raid.emoji()} {Roles[id_r.raid].Mention} - ");
		text.Write($"Forming for raid ~{time_forming.Timestamp(TimestampStyle.Relative)}");
		text.WriteLine($" (pulling at ~{time_raid.Timestamp(TimestampStyle.TimeShort)}).");
		if (raid.summary is not null) {
			text.WriteLine(raid.summary);
		}
		text.WriteLine("If you're unsure, check the pinned posts for raid reqs. :thumbsup:");
		DiscordMessage msg = await
			Channels[id_ch.announce]
			.SendMessageAsync(text.ToString());

		// Update post with logs.
		// Called here so code doesn't need to be repeated.
		update_raid_logs(raid);

		// React to raid announcement.
		await msg.CreateReactionAsync(DiscordEmoji.FromName(Client, raid.emoji()));
	}

	static async void e_raid_now_announce() {
		if (Guild is null) {
			Log.Warning("  Could not announce raid: guild not loaded.");
			return;
		}

		// Find/construct raid object.
		int week = Raid.CurrentWeek;
		Raid.Day? day = DateTimeOffset.Now.DayOfWeek switch {
			DayOfWeek.Friday   => Raid.Day.Fri,
			DayOfWeek.Saturday => Raid.Day.Sat,
			_ => null,
		};
		if (day is null) {
			Log.Error("  Raid announcements can only made on Fri/Sat.");
			return;
		}
		Raid raid = Raid.get(week, (Raid.Day)day)
			?? new Raid(week, (Raid.Day) day);

		// Send raid announcement.
		StringWriter text = new ();
		text.WriteLine($"{raid.emoji()} {Roles[id_r.raid].Mention} - Forming now!");
		if (raid.summary is not null) {
			text.WriteLine(raid.summary);
		}
		DiscordMessage msg = await
			Channels[id_ch.announce]
			.SendMessageAsync(text.ToString());
		msgs_raid_forming.Add(raid.date, msg.Id);

		// React to raid announcement.
		await msg.CreateReactionAsync(DiscordEmoji.FromName(Client, raid.emoji()));
		await msg.CreateReactionAsync(Emojis[id_e.kyrian]);
		await msg.CreateReactionAsync(Emojis[id_e.necrolord]);
		await msg.CreateReactionAsync(Emojis[id_e.nightfae]);
		await msg.CreateReactionAsync(Emojis[id_e.venthyr]);
	}

	static void e_raid_set_logs_remind() {
		if (Guild is null) {
			Log.Error("  Guild not loaded for event execution.");
			return;
		}

		StringWriter text = new ();
		text.WriteLine($"{Roles[id_r.raidOfficer].Mention} -");
		text.WriteLine($"{em}Reminder to set logs for tonight. :ok_hand: :card_box:");
		text.WriteLine($"{em}`@Irene -logs-set`");

		DiscordChannel ch = Channels[id_ch.officerBots];
		_ = ch.SendMessageAsync(text.ToString());
	}

	static void e_raid_break_remind() {
		if (Guild is null) {
			Log.Error("  Guild not loaded for event execution.");
			return;
		}

		StringWriter text = new ();
		text.WriteLine($"{Roles[id_r.raidOfficer].Mention} -");
		text.WriteLine($"{em}Raid break soon. :slight_smile: :tropical_drink:");

		DiscordChannel ch = Channels[id_ch.officer];
		_ = ch.SendMessageAsync(text.ToString());
	}

	static void e_weekly_officer_meeting() {
		if (Guild is null) {
			Log.Error("  Guild not loaded for event execution.");
			return;
		}

		StringWriter text = new ();
		text.WriteLine($"Weekly {Roles[id_r.officer].Mention} meeting after raid. :slight_smile:");

		DiscordChannel ch = Channels[id_ch.officer];
		_ = ch.SendMessageAsync(text.ToString());
	}

	static void e_update_raid_plans() {
		if (Guild is null) {
			Log.Error("  Guild not loaded for event execution.");
			return;
		}

		StringWriter text = new ();
		text.WriteLine($"{Roles[id_r.raidOfficer].Mention} -");
		text.WriteLine($"{em}Decide on the raid plans for next week (if you haven't already). :ok_hand:");
		text.WriteLine($"{em}`@Irene -raid-set-F`, `@Irene -raid-set-S`");

		DiscordChannel ch = Channels[id_ch.officerBots];
		_ = ch.SendMessageAsync(text.ToString());
	}

	static void e_promote_remind() {
		if (Guild is null) {
			Log.Error("  Guild not loaded for event execution.");
			return;
		}

		StringWriter text = new ();
		text.WriteLine($"{Roles[id_r.recruiter].Mention} -");
		text.WriteLine($"{em}Go over the 2-week+-trials this week (if there are any). :seedling:");
		text.WriteLine($"{em}`@Irene -trials`");

		DiscordChannel ch = Channels[id_ch.officerBots];
		_ = ch.SendMessageAsync(text.ToString());
	}
}
