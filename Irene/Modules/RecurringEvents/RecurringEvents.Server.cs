using static Irene.RecurringEvent;

namespace Irene.Modules;

static partial class RecurringEvents {
	// Used in module initialization.
	// For timezone conversion, see:
	// https://www.timeanddate.com/worldclock/converter.html?p1=234
	private static async Task<List<Event>> GetEvents_Server() {
		TimeSpan t0 = TimeSpan.Zero;
		List<Task<Event?>> event_tasks = new () {
			Event.Create(
				"Weekly meme channel name update",
				RecurPattern.FromWeekly(
					new (new (6, 0), TimeZone_PT),
					DayOfWeek.Monday
				),
				new (
					new (2022, 2, 28, 14, 0, 0, t0),
					new (2022, 2, 28)
				),
				Event_WeeklyMemeChannelNameUpdate,
				TimeSpan.FromDays(3.5) // half the week
			),
		};

		return await InitEventListAsync(event_tasks);
	}

	private const int _memeHistorySize = 20;
	private static async Task Event_WeeklyMemeChannelNameUpdate(DateTimeOffset time_trigger) {
		await AwaitGuildInitAsync();

		// Read in all non-empty meme names.
		List<string> names = new ();
		lock (_lockMemes) {
			names = new (File.ReadAllLines(_pathMemes));
		}
		foreach (string line in names) {
			if (line == "")
				names.Remove(line);
		}

		// Read in all non-empty meme history.
		List<string> names_old = new ();
		lock (_lockMemes) {
			names_old = new (File.ReadAllLines(_pathMemeHistory));
		}
		foreach (string line in names_old) {
			if (line == "")
				names_old.Remove(line);
		}

		// Randomly select a name.
		// Creating a new PRNG each time is suboptimal, but for our
		// needs here it suffices.
		// If the name was in history, keep checking the next name
		// until a fresh one is found.
		System.Random rng = new ();
		int i = rng.Next(names.Count);
		string name = names[i];
		if (names.Count > _memeHistorySize) {
			while (names_old.Contains(name)) {
				i = (i + 1) % names.Count;
				name = names[i];
			}
		}

		// Update channel name.
		await Channels[id_ch.memes].ModifyAsync(ch => ch.Name = name);

		// Update history file.
		names_old.Add(name);
		if (names_old.Count > _memeHistorySize)
			names_old.RemoveRange(0, names_old.Count - _memeHistorySize);
		lock (_lockMemes) {
			File.WriteAllLines(_pathMemeHistory, names_old);
		}
	}
}
