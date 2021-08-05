using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Timers;

using static Irene.Program;

namespace Irene.Modules {
	// All times are local time (Pacific Time).
	// (not server time, i.e. Central Time)
	partial class WeeklyEvent {
		public record Time {
			public DayOfWeek day { get; init; }
			public TimeSpan time { get; init; }
			public int week_multiple { get; init; }

			public Time(DayOfWeek day, TimeSpan time, int week_multiple = 1) {
				this.day = day;
				this.time = time;
				this.week_multiple = week_multiple;
			}
		}

		// Static data.
		static readonly List<WeeklyEvent> events;
		static readonly object lock_data = new ();

		const string
			path_data = @"data/weekly.txt",
			path_buffer = @"data/weekly-buf.txt";
		const string delim = "=";
		const string format_time = "u";

		// Event times.
		static readonly TimeSpan
			t_cycle_meme_ch          = new ( 6,  0, 0),
			t_morning_announce       = new ( 8, 30, 0),
			t_pin_affixes            = new (10,  0, 0),
			t_raid_soon_announce     = new (17, 10, 0),
			t_raid_now_announce      = new (18, 10, 0),
			t_set_logs_remind        = new (18, 20, 0),
			t_raid_break_remind      = new (19, 20, 0),
			t_weekly_officer_meeting = new (20, 35, 0),
			t_raid_plans_remind      = new (20, 40, 0),
			t_promote_remind         = new (20, 45, 0);

		// Force static initializer to run.
		public static void init() { return; }
		static WeeklyEvent() {
			events = new () {
				new WeeklyEvent(
					"update meme channel name",
					"Update Meme Channel Name",
					new Time(DayOfWeek.Monday, t_cycle_meme_ch),
					e_cycle_meme_ch,
					true
				),
				new WeeklyEvent(
					"weekly raid info announcement",
					"Weekly Raid Info Announcement",
					new Time(DayOfWeek.Tuesday, t_morning_announce),
					e_weekly_raid_info_announce
				),
				new WeeklyEvent(
					"pin weekly affixes",
					"Pin Weekly Affixes",
					new Time(DayOfWeek.Tuesday, t_pin_affixes),
					e_pin_affixes,
					true
				),
				new WeeklyEvent(
					"raid 1 day announcement",
					"Raid 1 Morning Announcement",
					new Time(DayOfWeek.Friday, t_morning_announce),
					e_raid1_day_announce
				),
				new WeeklyEvent(
					"raid 1 soon announcement",
					"Raid 1 Soon Announcement",
					new Time(DayOfWeek.Friday, t_raid_soon_announce),
					e_raid1_soon_announce
				),
				new WeeklyEvent(
					"raid 1 now announcement",
					"Raid 1 Forming Announcement",
					new Time(DayOfWeek.Friday, t_raid_now_announce),
					e_raid1_now_announce
				),
				new WeeklyEvent(
					"raid 1 set logs reminder",
					"Raid 1 Set Logs Reminder",
					new Time(DayOfWeek.Friday, t_set_logs_remind),
					e_raid_set_logs_remind
				),
				new WeeklyEvent(
					"raid 1 break reminder",
					"Raid 1 Break Reminder",
					new Time(DayOfWeek.Friday, t_raid_break_remind),
					e_raid_break_remind
				),
				new WeeklyEvent(
					"raid 2 day announcement",
					"Raid 2 Morning Announcement",
					new Time(DayOfWeek.Saturday, t_morning_announce),
					e_raid2_day_announce
				),
				new WeeklyEvent(
					"raid 2 soon announcement",
					"Raid 2 Soon Announcement",
					new Time(DayOfWeek.Saturday, t_raid_soon_announce),
					e_raid2_soon_announce
				),
				new WeeklyEvent(
					"raid 2 now announcement",
					"Raid 2 Forming Announcement",
					new Time(DayOfWeek.Saturday, t_raid_now_announce),
					e_raid2_now_announce
				),
				new WeeklyEvent(
					"raid 2 set logs reminder",
					"Raid 2 Set Logs Reminder",
					new Time(DayOfWeek.Saturday, t_set_logs_remind),
					e_raid_set_logs_remind
				),
				new WeeklyEvent(
					"raid 2 break reminder",
					"Raid 2 Break Reminder",
					new Time(DayOfWeek.Saturday, t_raid_break_remind),
					e_raid_break_remind
				),
				new WeeklyEvent(
					"weekly officer meeting",
					"Post-raid Officer Meeting",
					new Time(DayOfWeek.Saturday, t_weekly_officer_meeting),
					e_weekly_officer_meeting
				),
				new WeeklyEvent(
					"raid plans reminder",
					"Weekly Raid Plans Reminder",
					new Time(DayOfWeek.Saturday, t_raid_plans_remind),
					e_update_raid_plans
				),
				new WeeklyEvent(
					"promotion reminder",
					"Member Promotion Reminder",
					new Time(DayOfWeek.Saturday, t_promote_remind),
					e_promote_remind
				),
			};

			log.debug("Initialized module: WeeklyEvent");
			log.endl();
		}

		// Configurable members.
		protected readonly string id;	// for data saving
		protected readonly string name;	// short readable description
		protected readonly Time schedule;
		protected readonly Action action;

		// Generated members.
		protected Timer timer;
		protected DateTimeOffset? last_executed;

		// Constructor for a single event.
		protected WeeklyEvent(
			string id,
			string name,
			Time schedule,
			Action action,
			bool is_retroactive = false ) {
			// Assign basic values.
			this.id = id;
			this.name = name;
			this.schedule = schedule;
			this.action = action;

			// Calculate initial value for timer.
			DateTimeOffset time_now = DateTimeOffset.Now;
			DateTimeOffset time_prev =
				parse_last_executed(id) ?? time_now;
			DateTimeOffset time_next = time_now;
			while (time_next <= time_now) {
				time_next = get_next_time(schedule, time_next);
			}

			// Execute action retroactively, if needed.
			// This happens at most once.
			if (is_retroactive) {
				DateTimeOffset time_retroactive = get_prev_time(schedule, time_next);
				if (time_prev < time_retroactive) {
					_ = Task.Run(() => {
						log.info($"Retroactively scheduled firing event: {name}");
						log.endl();
						action();
						last_executed = time_now;
						update_executed(id, time_now);
					});
				}
			}

			// Set up timer.
			timer = new Timer((time_next - time_now).TotalMilliseconds);
			timer.Elapsed += async (t, e) => {
				// Reset the timer interval to its full value.
				DateTimeOffset time_now = DateTimeOffset.Now;
				DateTimeOffset time_next = get_next_time(schedule, time_now);
				timer.Interval = (time_next - time_now).TotalMilliseconds;

				// Run the scheduled action.
				log.info($"Firing scheduled event: {name}");
				log.endl();
				await Task.Run(action);

				// Update saved values.
				last_executed = time_now;
				update_executed(id, time_now);
			};
			timer.Start();
		}

		// Read the specified id's time from the datafile.
		static DateTimeOffset? parse_last_executed(string id) {
			ensure_datafile_exists();

			lock (lock_data) {
				StreamReader file = new (path_data);
				while (!file.EndOfStream) {
					string line = file.ReadLine() ?? "";
					if (line.StartsWith(id + delim)) {
						file.Close();
						string[] split = line.Split(delim, 2);
						string data = split[1];
						bool can_parse =
							DateTimeOffset.TryParse(data, out DateTimeOffset time);
						if (!can_parse) {
							return null;
						} else {
							time = time.ToLocalTime();
							return time;
						}
					}
				}
				file.Close();
			}
			return null;
		}

		// Write the current id-time pair to the datafile.
		static void update_executed(string id, DateTimeOffset time) {
			ensure_datafile_exists();

			// Read in all current data; replacing appropriate line.
			StringWriter buffer = new ();
			bool was_written = false;
			lock (lock_data) {
				StreamReader file = new (path_data);
				while (!file.EndOfStream) {
					string line = file.ReadLine() ?? "";
					if (line.StartsWith(id + delim)) {
						buffer.WriteLine(id + delim + time.ToString(format_time));
						was_written = true;
					} else {
						buffer.WriteLine(line);
					}
				}
				file.Close();
			}

			// Add line if the entry is new.
			if (!was_written) {
				buffer.WriteLine(id + delim + time.ToString(format_time));
			}

			// Update files.
			lock (lock_data) {
				File.WriteAllText(path_buffer, buffer.output());
				File.Delete(path_data);
				File.Move(path_buffer, path_data);
			}
		}

		// Creates an empty file if none exists at the specified path.
		static void ensure_datafile_exists() {
			lock (lock_data) {
				if (!File.Exists(path_data)) {
					File.Create(path_data).Close();
				}
			}
		}

		// Returns the next scheduled DateTimeOffset after the given time.
		static DateTimeOffset get_next_time(Time time, DateTimeOffset time_from) {
			DateTimeOffset time_next = time_from;
			time_next = time_next.next_weekday(time.day);
			time_next += time.time;
			time_next += (time.week_multiple - 1) * TimeSpan.FromDays(7);

			// If the result falls before the starting timepoint,
			// advance result by another week-multiple.
			// This should only happen if the week-multiple == 1.
			if (time_next < time_from) {
				time_next += time.week_multiple * TimeSpan.FromDays(7);
			}

			return time_next;
		}

		// Returns the scheduled DateTimeOffset immediately prior to the given time.
		static DateTimeOffset get_prev_time(Time time, DateTimeOffset time_from) {
			DateTimeOffset time_prev = time_from;
			time_prev -= time.week_multiple * TimeSpan.FromDays(7);
			return time_prev;
		}
	}
}
