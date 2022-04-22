﻿using static Irene.RecurringEvent;
using static Irene.Modules.Raid;

using RaidObj = Irene.Modules.Raid;
using TimestampStyle = Irene.Utils.Util.TimestampStyle;

namespace Irene.Modules;

partial class RecurringEvents {
	// Used in module initialization.
	// For timezone conversion, see: https://www.worldtimebuddy.com/
	private static async Task<List<Event>> GetEvents_Raid() {
		TimeSpan t0 = TimeSpan.Zero;
		List<Task<Event?>> event_tasks = new () {
			Event.Create(
				"Weekly raid plan announcement",
				RecurPattern.FromWeekly(
					new (new (8, 30), TimeZone_PT),
					DayOfWeek.Tuesday
				),
				new (
					new (2022, 3, 1, 16, 30, 0, t0),
					new (2022, 3, 1)
				),
				Event_WeeklyRaidPlanAnnouncement,
				TimeSpan.FromDays(2) // Tue. ~ Thu.
			),

			// Friday raid.
			Event.Create(
				"Friday: morning raid announcement",
				RecurPattern.FromWeekly(
					new (new (7, 30), TimeZone_PT),
					DayOfWeek.Friday
				),
				new (
					new (2022, 3, 4, 15, 30, 0, t0),
					new (2022, 3, 4)
				),
				Event_RaidDayMorningAnnouncement,
				TimeSpan.FromHours(4.5) // until noon PT
			),
			Event.Create(
				"Friday: raid forming soon reminder",
				RecurPattern.FromWeekly(
					new (new (17, 45), TimeZone_PT),
					DayOfWeek.Friday
				),
				new (
					new (2022, 3, 5, 1, 45, 0, t0),
					new (2022, 3, 4)
				),
				Event_RaidFormingSoonReminder,
				TimeSpan.FromMinutes(35) // until 18:20 PT
			),
			Event.Create(
				"Friday: raid forming announcement",
				RecurPattern.FromWeekly(
					new (new (18, 40), TimeZone_PT),
					DayOfWeek.Friday
				),
				new (
					new (2022, 3, 5, 2, 40, 0, t0),
					new (2022, 3, 4)
				),
				Event_RaidFormingAnnouncement,
				TimeSpan.FromMinutes(15) // until 18:55 PT
			),
			Event.Create(
				"Friday: set raid logs reminder",
				RecurPattern.FromWeekly(
					new (new (18, 50), TimeZone_PT),
					DayOfWeek.Friday
				),
				new (
					new (2022, 3, 5, 2, 50, 0, t0),
					new (2022, 3, 4)
				),
				Event_RaidSetLogsReminder,
				TimeSpan.FromHours(3) // until 21:50 PT
			),
			Event.Create(
				"Friday: raid break reminder",
				RecurPattern.FromWeekly(
					new (new (19, 50), TimeZone_PT),
					DayOfWeek.Friday
				),
				new (
					new (2022, 3, 5, 3, 50, 0, t0),
					new (2022, 3, 4)
				),
				Event_RaidBreakReminder,
				TimeSpan.FromMinutes(5) // until 19:55 PT
			),

			// Saturday raid.
			Event.Create(
				"Saturday: morning raid announcement",
				RecurPattern.FromWeekly(
					new (new (7, 30), TimeZone_PT),
					DayOfWeek.Saturday
				),
				new (
					new (2022, 3, 5, 15, 30, 0, t0),
					new (2022, 3, 5)
				),
				Event_RaidDayMorningAnnouncement,
				TimeSpan.FromHours(4.5) // until noon PT
			),
			Event.Create(
				"Saturday: raid forming soon reminder",
				RecurPattern.FromWeekly(
					new (new (17, 45), TimeZone_PT),
					DayOfWeek.Saturday
				),
				new (
					new (2022, 3, 6, 1, 45, 0, t0),
					new (2022, 3, 5)
				),
				Event_RaidFormingSoonReminder,
				TimeSpan.FromMinutes(35) // until 18:20 PT
			),
			Event.Create(
				"Saturday: raid forming announcement",
				RecurPattern.FromWeekly(
					new (new (18, 40), TimeZone_PT),
					DayOfWeek.Saturday
				),
				new (
					new (2022, 3, 6, 2, 40, 0, t0),
					new (2022, 3, 5)
				),
				Event_RaidFormingAnnouncement,
				TimeSpan.FromMinutes(15) // until 18:55 PT
			),
			Event.Create(
				"Saturday: set raid logs reminder",
				RecurPattern.FromWeekly(
					new (new (18, 50), TimeZone_PT),
					DayOfWeek.Saturday
				),
				new (
					new (2022, 3, 6, 2, 50, 0, t0),
					new (2022, 3, 5)
				),
				Event_RaidSetLogsReminder,
				TimeSpan.FromHours(3) // until 21:50 PT
			),
			Event.Create(
				"Saturday: raid break reminder",
				RecurPattern.FromWeekly(
					new (new (19, 50), TimeZone_PT),
					DayOfWeek.Saturday
				),
				new (
					new (2022, 3, 6, 3, 50, 0, t0),
					new (2022, 3, 5)
				),
				Event_RaidBreakReminder,
				TimeSpan.FromMinutes(5) // until 19:55 PT
			),

			// Officer meeting.
			Event.Create(
				"Weekly officer meeting reminder",
				RecurPattern.FromWeekly(
					new (new (21, 5), TimeZone_PT),
					DayOfWeek.Saturday
				),
				new (
					new (2022, 3, 6, 5, 5, 0, t0),
					new (2022, 3, 5)
				),
				Event_OfficerMeetingReminder,
				TimeSpan.FromMinutes(5) // until 21:10 PT
			),
			Event.Create(
				"Weekly set raid plans reminder",
				RecurPattern.FromWeekly(
					new (new (21, 10), TimeZone_PT),
					DayOfWeek.Saturday
				),
				new (
					new (2022, 3, 6, 5, 10, 0, t0),
					new (2022, 3, 5)
				),
				Event_OfficerRaidPlansReminder,
				TimeSpan.FromMinutes(5) // until 21:15 PT
			),
			Event.Create(
				"Weekly promote trials reminder",
				RecurPattern.FromWeekly(
					new (new (21, 15), TimeZone_PT),
					DayOfWeek.Saturday
				),
				new (
					new (2022, 3, 6, 5, 15, 0, t0),
					new (2022, 3, 5)
				),
				Event_OfficerPromoteTrialsReminder,
				TimeSpan.FromMinutes(5) // until 21:20 PT
			),
		};

		return await InitEventListAsync(event_tasks);
	}

	private static async Task Event_WeeklyRaidPlanAnnouncement(DateTimeOffset time_trigger) {
	}

	private static async Task Event_RaidDayMorningAnnouncement(DateTimeOffset time_trigger) {
	}

	private static async Task Event_RaidFormingSoonReminder(DateTimeOffset time_trigger) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		// Find/construct raid object.
		RaidDate date = RaidDate.TryCreate(time_trigger)!.Value;
		RaidObj raid = Fetch(date);

		// Calculate raid time / forming time info.
		DateTime today = TimeZoneInfo.ConvertTime(
			DateTime.UtcNow,
			TimeZone_Server
		);
		today -= today.TimeOfDay;
		DateTime time_raid = today +
			Time_RaidStart.TimeOnly.ToTimeSpan();
		DateTime time_forming = time_raid -
			TimeSpan.FromMinutes(20);

		// Convert times to UTC, then format as timestamps.
		DateTimeOffset dateTime_raid =
			new (TimeZoneInfo.ConvertTime(
				time_raid,
				TimeZone_Server,
				TimeZoneInfo.Utc
			));
		DateTimeOffset dateTime_forming =
			new (TimeZoneInfo.ConvertTime(
				time_forming,
				TimeZone_Server,
				TimeZoneInfo.Utc
			));
		string time_raid_str = dateTime_raid
			.Timestamp(TimestampStyle.TimeShort);
		string time_forming_str = dateTime_forming
			.Timestamp(TimestampStyle.Relative);

		// Compose raid announcement.
		string response =
			$"{raid.Emoji} {Roles[id_r.raid].Mention} - " +
			$"Forming for raid ~{time_forming_str} " +
			$"(pulling at ~{time_raid_str}).";
		if (raid.Summary is not null)
			response += $"\n{raid.Summary}";
		response += "\nIf you aren't sure, check the pinned posts for raid requirements. :thumbsup:";

		// Respond.
		DiscordMessage message = await
			Channels[id_ch.announce] .SendMessageAsync(response);
		DiscordEmoji emoji_raid =
			DiscordEmoji.FromName(Client, raid.Emoji);
		await message.CreateReactionAsync(emoji_raid);
	}

	private static async Task Event_RaidFormingAnnouncement(DateTimeOffset time_trigger) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		// Send a message and then save the message back to the data file.
		RaidDate date = RaidDate.TryCreate(time_trigger)!.Value;
		RaidObj raid = Fetch(date);
		string text = raid.AnnouncementText;
		DiscordMessage message = await
			Channels[id_ch.announce].SendMessageAsync(text);
		raid.MessageId = message.Id;
		raid.UpdateData();

		// Update announcement message with logs (if available).
		await raid.UpdateAnnouncement();

		// Add reactions.
		DiscordEmoji emoji_raid =
			DiscordEmoji.FromName(Client, raid.Emoji);
		await message.CreateReactionAsync(emoji_raid);
		await message.CreateReactionAsync(Emojis[id_e.kyrian]);
		await message.CreateReactionAsync(Emojis[id_e.necrolord]);
		await message.CreateReactionAsync(Emojis[id_e.nightfae]);
		await message.CreateReactionAsync(Emojis[id_e.venthyr]);
	}

	private static async Task Event_RaidSetLogsReminder(DateTimeOffset time_trigger) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		// Skip reminder if logs are already set.
		RaidDate date = RaidDate.TryCreate(time_trigger)!.Value;
		RaidObj raid = Fetch(date);
		foreach (RaidGroup group in raid.Data.Keys) {
			if (raid.Data[group].LogId is not null) {
				Log.Information("  Skipping reminder: logs already set.");
				return;
			}
		}

		// Send reminder.
		string response = string.Join("\n", new List<string>() {
			$"{Roles[id_r.raidOfficer].Mention} -",
			$"{_t}Reminder to set logs for tonight. :ok_hand: :card_box:",
			$"{_t}`/raid set-logs`",
		});
		await Channels[id_ch.officerBots].SendMessageAsync(response);
	}

	private static async Task Event_RaidBreakReminder(DateTimeOffset time_trigger) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		// Send reminder.
		string response = string.Join("\n", new List<string>() {
			$"{Roles[id_r.raidOfficer].Mention} -",
			$"{_t}Raid break soon. :slight_smile: :tropical_drink:",
		});
		await Channels[id_ch.officer].SendMessageAsync(response);
	}

	private static async Task Event_OfficerMeetingReminder(DateTimeOffset _) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		// Send reminder.
		string response = $"Weekly {Roles[id_r.officer].Mention} meeting after raid. :slight_smile:";
		await Channels[id_ch.officer].SendMessageAsync(response);
	}

	private static async Task Event_OfficerRaidPlansReminder(DateTimeOffset time_trigger) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		// Skip reminder if (any) plans are already set.
		RaidDate date = RaidDate.TryCreate(time_trigger)!.Value;
		RaidTier tier = date.Tier;
		int week = date.Week + 1;

		RaidDate date_F = new (tier, week, RaidDay.Friday);
		RaidDate date_S = new (tier, week, RaidDay.Saturday);
		RaidObj raid_F = Fetch(date_F);
		RaidObj raid_S = Fetch(date_S);
		if (raid_F.Summary is not null ||
			raid_S.Summary is not null
		) {
			Log.Information("  Skipping reminder: plans already set.");
			return;
		}

		// Send reminder.
		string response = string.Join("\n", new List<string>() {
			$"{Roles[id_r.raidOfficer].Mention} -",
			$"{_t}Decide on the raid plans for next week (if you haven't already). :ok_hand:",
			$"{_t}`/raid set-plan`",
		});
		await Channels[id_ch.officerBots].SendMessageAsync(response);
	}

	private static async Task Event_OfficerPromoteTrialsReminder(DateTimeOffset _) {
		if (Guild is null) {
			Log.Error("  Guild not loaded yet.");
			return;
		}

		// Send reminder.
		string response = string.Join("\n", new List<string>() {
			$"{Roles[id_r.recruiter].Mention} -",
			$"{_t}Go over the 2-week+-trials this week (if there are any). :seedling:",
			$"{_t}`/rank list-trials`",
		});
		await Channels[id_ch.officerBots].SendMessageAsync(response);
	}
}