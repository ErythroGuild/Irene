namespace Irene.Modules;

class IreneStatus {
	private static readonly LongTimer _timerRotate;
	private const double
		_timerDaysBase = 4.0,
		_timerDaysRange = 3.0;

	private static readonly object _lock = new ();
	private const string
		_pathStatus = @"data/irene-status.txt",
		_pathTemp = @"data/irene-status-temp.txt";
	private const string _separator = "=";

	public static void Init() { }
	static IreneStatus() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		Util.CreateIfMissing(_pathStatus, _lock);
		
		// Add handler for setting status.
		_timerRotate = new (GetRandomDurationMsec(), false);
		_timerRotate.Elapsed += async (obj, e) => {
			_timerRotate.Stop();

			bool didSet = await SetRandom();
			if (didSet)
				Log.Information("Changed bot status.");
			else
				Log.Information("Attempted to change bot status; none available.");

			double duration_msec = GetRandomDurationMsec();
			TimeSpan duration =
				TimeSpan.FromMilliseconds(duration_msec);
			DateTimeOffset time_next =
				DateTimeOffset.Now + duration;

			_timerRotate.Interval = (decimal)duration_msec;
			_timerRotate.Start();

			Log.Debug("Next change attempt: {DateNext:u}", time_next);
		};
		_timerRotate.Start();

		// Set current status.
		_ = SetRandom();

		Log.Information("  Initialized module: IreneStatus");
		Log.Debug("    Status set and rotation started.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public static async Task SetAndAdd(DiscordActivity status) {
		await Set(status);
		Add(status);
	}

	// Returns false if no statuses were available to set.
	public static async Task<bool> SetRandom() {
		List<DiscordActivity> statuses = new (GetAll());

		// If no statuses are available, set blank status.
		// Return false as indication.
		if (statuses.Count == 0) {
			await Set(new ());
			return false;
		}

		// Set a random status.
		int i = Random.Shared.Next(0, statuses.Count);
		await Set(statuses[i]);
		return true;
	}

	// Set a status, appending an indicator for debug mode.
	// Debug mode also sets to "busy" instead of "online".
	public static async Task Set(DiscordActivity status) {
		UserStatus status_kind = UserStatus.Online;
		if (IsDebug) {
			status.Name = $"{status.Name} - [DEBUG]";
			status_kind = UserStatus.DoNotDisturb;
		}
		await Client.UpdateStatusAsync(status, status_kind);

		// Reset interval for random rotation.
		_timerRotate.Stop();
		_timerRotate.Interval = (decimal)GetRandomDurationMsec();
		_timerRotate.Start();
	}

	// Add a status to the saved list (if there's no duplicate).
	public static void Add(DiscordActivity status) {
		HashSet<string> lines = new ();
		lock (_lock) {
			lines = new (File.ReadAllLines(_pathStatus));
		}

		string status_string = string.Join(
			_separator,
			status.ActivityType,
			status.Name
		);

		lines.Add(status_string);

		lock (_lock) {
			File.WriteAllLines(_pathTemp, lines);
			File.Delete(_pathStatus);
			File.Move(_pathTemp, _pathStatus);
		}
	}

	// Parse the saved statuses into DiscordActivity objects.
	public static ISet<DiscordActivity> GetAll() {
		List<string> lines = new ();
		lock (_lock) {
			lines = new (File.ReadAllLines(_pathStatus));
		}

		HashSet<DiscordActivity> statuses = new ();
		foreach (string line in lines) {
			string[] split = line.Split(_separator, 2);
			ActivityType type = Enum.Parse<ActivityType>(split[0]);
			string content = split[1];
			statuses.Add(new (content, type));
		}

		return statuses;
	}

	// Returns a random duration for changing status.
	// Parameters are const fields of IreneStatus.
	private static double GetRandomDurationMsec() {
		double random = Random.Shared.NextDouble();
		random = 2 * random - 1; // -> [-1.0, 1.0)
		double days = _timerDaysBase + random * _timerDaysRange;
		return TimeSpan.FromDays(days).TotalMilliseconds;
	}
}
