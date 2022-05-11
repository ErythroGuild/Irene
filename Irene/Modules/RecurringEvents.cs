using System.Globalization;

using static Irene.RecurringEvent;

using EventAction = System.Func<System.DateTimeOffset, System.Threading.Tasks.Task>;

namespace Irene.Modules;

static partial class RecurringEvents {
	private class Event {
		public string Id { get; init; }
		public RecurringEvent Data { get; init; }
		public EventAction Action { get; init; }

		private readonly LongTimer _timer;

		public static async Task<Event?> Create(
			string id,
			RecurPattern pattern,
			RecurResult result_init,
			EventAction action,
			TimeSpan? time_retroactive=null
		) {
			time_retroactive ??= TimeSpan.Zero;

			// Fetch previous fired time from saved data.
			RecurResult result_prev = FetchLastExecuted(id) ??
				result_init;
			RecurringEvent data = new (pattern, result_prev);

			// Retroactively execute task as many times as needed.
			// (If so, also update last executed date.)
			DateTimeOffset dateTime_now = DateTimeOffset.Now;
			DateTimeOffset dateTime_discard =
				dateTime_now - time_retroactive.Value;

			DateTimeOffset? dateTime_next = data.PeekNext();
			while (dateTime_next is not null &&
				dateTime_next < dateTime_now
			) {
				dateTime_next = data.GetNext()!.Value; // call Get() to update state
				DateTimeOffset dateTime_timestamp =
					dateTime_next.Value.ToLocalTime();
				if (dateTime_next > dateTime_discard) {
					Log.Information("Event triggered retroactively: {EventName}", id);
					Log.Debug("  Original scheduled time: {Time:u}", dateTime_timestamp);
					await action.Invoke(dateTime_next.Value);
				} else {
					Log.Debug("Skipping retroactive event: {EventName}", id);
					Log.Debug("  Original scheduled time: {Time:u}", dateTime_timestamp);
				}
				dateTime_next = data.PeekNext();
				dateTime_now = DateTimeOffset.Now; // update every time
			}
			UpdateLastExecuted(id, data.Previous);

			// Calculate initial value for timer.
			if (data.PeekNext() is null)
				return null;
			dateTime_now = DateTimeOffset.Now;
			dateTime_next = data.GetNext()!.Value;
			TimeSpan delta = dateTime_next.Value - dateTime_now;

			// Set up timer.
			LongTimer timer = new (delta.TotalMilliseconds);
			timer.Elapsed += async (t, e) => {
				timer.Stop();
				DateTimeOffset dateTime_now = DateTimeOffset.Now;

				// Run the action and update records of execution.
				Log.Information("Event triggered: {EventId}", id);
				Stopwatch stopwatch = Stopwatch.StartNew();
				await action.Invoke(dateTime_now);
				UpdateLastExecuted(id, data.Previous);
				stopwatch.LogMsecDebug("  Event finished executing in {Time} msec.");

				// Check for valid next timepoint.
				if (data.PeekNext() is null) {
					Log.Error("Event entered an invalid state.");
					Log.Error("This event will no longer be triggered.");
					Log.Debug("  ID: {EventId}", id);
					Log.Debug("  Last executed on: {Time}", data.Previous);
					return;
				}

				// Set up the next occurrence.
				DateTimeOffset dateTime_next = data.GetNext()!.Value;
				TimeSpan delta = dateTime_next - dateTime_now;
				timer.Interval = (decimal)delta.TotalMilliseconds;
				timer.Start();
				Log.Debug("  Recurrence scheduled for {Time:u}", dateTime_next);
			};
			
			// Return the constructed object.
			Event @event = new (id, data, action, timer);
			@event._timer.Start();
			return @event;
		}

		// Private constructor.
		// To get an instance, call Event.Create() instead.
		private Event(
			string id,
			RecurringEvent data,
			EventAction action,
			LongTimer timer
		) {
			Id = id;
			Data = data;
			Action = action;
			_timer = timer;
		}
	}

	private static readonly List<Event> _events = new ();
	private static readonly object
		_lock = new (),
		_lockMemes = new (),
		_lockDirData = new (),
		_lockDirLogs = new ();

	private const string
		_pathData  = @"data/events.txt",
		_pathTemp  = @"data/events-temp.txt",
		_pathMemes = @"data/memes.txt",
		_pathMemeHistory = @"data/memes-history.txt",
		_pathDirData = @"config/dir-data.txt",
		_pathDirLogs = @"config/dir-logs.txt";
	private const string _delim = "|||";

	private const string _formatDateTime = "u";
	private static readonly CultureInfo _cultureFormat =
		CultureInfo.InvariantCulture;

	private const string _t = "\u2003";

	// Force static initializer to run.
	public static void Init() { }
	static RecurringEvents() {
		_ = InitAsync();
	}
	private static async Task InitAsync() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		Util.CreateIfMissing(_pathData, _lock);
		Util.CreateIfMissing(_pathMemes, _lockMemes);
		Util.CreateIfMissing(_pathMemeHistory, _lockMemes);

		List<Task<List<Event>>> tasks = new () {
			GetEvents_Raid(),
			GetEvents_Server(),
			GetEvents_Maintenance(),
		};

		// Wait for all tasks to complete.
		await Task.WhenAll(tasks);
		foreach (Task<List<Event>> task in tasks)
			_events.AddRange(task.Result);

		Log.Information("  Initialized module: RecurringEvent");
		string events_count = _events.Count switch {
			1 => "event",
			_ => "events", // includes 0
		};
		Log.Debug($"    Registered {{EventCount}} {events_count}.", _events.Count);
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	private static async Task<List<Event>> InitEventListAsync(List<Task<Event?>> tasks) {
		// Wait for all tasks to complete.
		await Task.WhenAll(tasks);

		// Fetch results.
		List<Event> events = new ();
		foreach (Task<Event?> task in tasks) {
			Event? @event = task.Result;
			if (@event is not null)
				events.Add(@event);
			else
				Log.Warning("  Failed to initialize event.");
		}
		return events;
	}

	// Read the last time the event was executed.
	private static RecurResult? FetchLastExecuted(string id) {
		string? line_entry = null;

		// Look for the corresponding data.
		lock (_lock) {
			using StreamReader file = File.OpenText(_pathData);
			while (!file.EndOfStream) {
				string line = file.ReadLine() ?? "";
				if (line.StartsWith(id + _delim)) {
					line_entry = line;
					break;
				}
			}
		}

		// Return null if data not found.
		if (line_entry is null)
			return null;

		// Parse the data and reconstruct the original object.
		string[] split = line_entry.Split(_delim, 3);
		string data_output = split[1];
		string data_cycle = split[2];
		try {
			DateTimeOffset output = DateTimeOffset.ParseExact(
				data_output,
				_formatDateTime,
				_cultureFormat
			);
			DateOnly cycle = DateOnly.ParseExact(
				data_cycle,
				Format_IsoDate,
				_cultureFormat
			);
			return new RecurResult(output, cycle);
		} catch (FormatException) {
			return null;
		}
	}

	// Write the current id-time pair to the datafile.
	private static void UpdateLastExecuted(string id, RecurResult last_executed) {
		// Serialize the last execution time.
		string data_output = last_executed
			.OutputDateTime.ToString(_formatDateTime, _cultureFormat);
		string data_cycle = last_executed
			.CycleDate.ToString(Format_IsoDate, _cultureFormat);
		string line_entry =
			string.Join(_delim, id, data_output, data_cycle);

		// Read in all current data; replacing appropriate line.
		List<string> lines = new ();
		bool was_written = false;
		lock (_lock) {
			using StreamReader data = File.OpenText(_pathData);
			while (!data.EndOfStream) {
				string? line = data.ReadLine();
				if (line is null)
					continue;
				if (line.StartsWith(id + _delim)) {
					lines.Add(line_entry);
					was_written = true;
				} else {
					lines.Add(line);
				}
			}
		}
		if (!was_written)
			lines.Add(line_entry);

		// Update files.
		lock (_lock) {
			File.WriteAllLines(_pathTemp, lines);
			File.Delete(_pathData);
			File.Move(_pathTemp, _pathData);
		}
	}
}
