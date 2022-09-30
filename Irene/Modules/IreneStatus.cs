using System.Globalization; // CultureInfo
using System.Timers; // ElapsedEventArgs

namespace Irene.Modules;

class IreneStatus {
	public record class Status {
		public readonly ActivityType Type;
		public readonly string Description;

		public Status(ActivityType type, string description) {
			Type = type;
			Description = description;
		}

		public string AsStatusText() =>
			AsActivity(false).AsStatusText();
		public DiscordActivity AsActivity(bool isDebug=false) => isDebug
			? new ($"{Description} - [DEBUG]", Type)
			: new (Description, Type);

		private const string _separator = "=";
		public static Status FromString(string input) {
			string[] split = input.Split(_separator, 2);
			if (split.Length < 2)
				throw new FormatException("Invalid status format: no separator found.");

			if (split[1] == "")
				throw new FormatException("Invalid status format: no status string found.");

			try {
				ActivityType type = Enum.Parse<ActivityType>(split[0]);
				return new Status(type, split[1]);
			} catch (ArgumentException e) {
				throw new FormatException("Invalid status format: invalid status type.", e);
			}
		}
		public override string ToString() =>
			string.Join(_separator, Type, Description);
	}

	// --------
	// Properties and fields:
	// --------

	public static Status? CurrentStatus { get; private set; } = null;
	public static DateTimeOffset? NextRefresh { get; private set; } = null;

	private static LongTimer _timerRefresh;
	private static readonly TimeSpan
		_refreshInterval = new (22,  0,  0),
		_refreshVariance = new ( 2, 30,  0);
	private static TaskQueue
		_queueStatuses = new (),
		_queueCurrent  = new ();
	private const string
		_pathStatuses = @"data/irenestatus-statuses.txt",
		_pathCurrent  = @"data/irenestatus-current.txt";
	private const string _separator = "<<<";
	private const string _formatDateTime = "u";
	private static readonly CultureInfo _formatCulture =
		CultureInfo.InvariantCulture;

	static IreneStatus() {
		Util.CreateIfMissing(_pathStatuses);
		Util.CreateIfMissing(_pathCurrent);

		// The timer duration is set in `InitializeCurrent()`.
		_timerRefresh = LongTimer.Create(_refreshInterval);
		_timerRefresh.Disable();
		_timerRefresh.Elapsed += async (timer, e) => {
			await RefreshHandler(timer, e);
		};

		_ = InitializeCurrent();

		// Ensure the status gets set again after reconnecting.
		static Task connectHandler(DiscordClient client, ReadyEventArgs e) {
			_ = Task.Run(InitializeCurrent);
			return Task.CompletedTask;
		}
		Client.Ready += connectHandler;
		Client.Resumed += connectHandler;
	}


	// --------
	// Public interface methods:
	// --------

	// Wrapper function for the private `ReadStatusesFromFile()`.
	public static async Task<IList<Status>> GetAll() =>
		await ReadStatusesFromFile();

	public static async Task SetAndAdd(Status status, DateTimeOffset end) {
		await Set(status, end);
		await Add(status);
	}

	// Returns false if there were no saved statuses to choose from.
	public static async Task<bool> SetRandom() {
		DateTimeOffset refresh = DateTimeOffset.UtcNow + GetRandomInterval();

		Status status;
		try {
			status = await GetRandomStatus();
		} catch (InvalidOperationException) {
			return false;
		}

		await Set(status, refresh);
		return true;
	}


	// --------
	// Internal (queued) file I/O methods:
	// --------

	// Methods for R/W the entire list of possible statuses. These methods
	// sort the statuses after/before R/W, respectively.
	// Statuses are de-duplicated before being written to file.
	private static async Task WriteStatusesToFile(IList<Status> statuses) {
		HashSet<string> statusSet = new ();
		foreach (Status status in statuses)
			statusSet.Add(status.ToString());

		List<string> lines = new (statusSet);
		lines.Sort();
		await _queueStatuses.Run(new Task<Task>(async () => {
			await File.WriteAllLinesAsync(_pathStatuses, lines);
		}));
	}
	private static async Task<IList<Status>> ReadStatusesFromFile() {
		List<string> lines = await _queueStatuses.Run(
			new Task<Task<List<string>>>(async () => {
				return new List<string>(await File.ReadAllLinesAsync(_pathStatuses));
			})
		);
		lines.Sort();

		List<Status> statuses = new ();
		foreach (string line in lines) {
			try {
				Status status = Status.FromString(line);
				statuses.Add(status);
			} catch (FormatException) { }
		}

		return statuses;
	}

	// Methods for R/W the (supposed-to-be) current status.
	// This method gets its data from the current state of `IreneStatus`.
	private static async Task WriteCurrentToFile() {
		if (CurrentStatus is null || NextRefresh is null)
			return;

		string output = string.Join(
			_separator,
			NextRefresh.Value.ToString(_formatDateTime, _formatCulture),
			CurrentStatus.ToString()
		);
		await _queueCurrent.Run(new Task<Task>(async () => {
			await File.WriteAllTextAsync(_pathCurrent, output);
		}));
	}
	// This method does NOT update the internal state of `IreneStatus`.
	private static async Task<(DateTimeOffset?, Status?)> ReadCurrentFromFile() {
		DateTimeOffset? refresh = null;
		Status? status = null;

		string line = await _queueCurrent.Run(
			new Task<Task<string>>(async () => {
				using StreamReader file = File.OpenText(_pathCurrent);
				return await file.ReadLineAsync() ?? "";
			})
		);

		string[] split = line.Split(_separator, 2);
		if (split.Length < 2)
			return (null, null);

		try {
			refresh = DateTimeOffset.ParseExact(
				split[0],
				_formatDateTime,
				_formatCulture
			);
			status = Status.FromString(split[1]);
		} catch (FormatException) { }

		return (refresh, status);
	}


	// --------
	// Other internal helper/component methods:
	// --------

	// Populates fields and sets a status starting from an uninitialized
	// starting state.
	private static async Task InitializeCurrent() {
		(DateTimeOffset? refresh, Status? status) = await
			ReadCurrentFromFile();

		if (refresh is null || refresh < DateTimeOffset.Now)
			refresh = DateTimeOffset.UtcNow + GetRandomInterval();

		if (status is null)
			status = await GetRandomStatus();

		await Set(status, refresh.Value);
	}

	// Inserts a given status to the list of saved statuses.
	private static async Task Add(Status status) {
		IList<Status> statuses = await ReadStatusesFromFile();
		statuses.Add(status);
		await WriteStatusesToFile(statuses);
	}

	// Set a current status (including updating `IreneStatus` fields),
	// and sets up the regular status changing rotation.
	private static async Task Set(Status status, DateTimeOffset end) {
		// Update saved file with changed current status.
		CurrentStatus = status;
		NextRefresh = end;
		Task taskFile = WriteCurrentToFile();

		// Set the connection status.
		DiscordActivity activity = status.AsActivity(IsDebug);
		UserStatus userStatus = IsDebug
			? UserStatus.DoNotDisturb
			: UserStatus.Online;
		Task taskDiscord = Client.UpdateStatusAsync(activity, userStatus);

		await Task.WhenAll(taskFile, taskDiscord);

		_timerRefresh.Disable();
		_timerRefresh.SetAndEnable(end);
	}

	// Note: This only needs to be attached once; there is no need for
	// multiple instances (which would then need to be disposed of).
	// Usage:
	//     _timerRefresh.Elapsed += async (timer, e) => {
	//         await RefreshHandler(timer, e);
	//     };
	private static async Task RefreshHandler(object? timer, ElapsedEventArgs e) {
		_timerRefresh.Disable();

		Log.Warning("bOoP");

		// `SetRandom()` takes care of updating the timer for us.
		await SetRandom();
	}

	// Get a random `TimeSpan` within the range of:
	// `_refreshInterval` +/- `_refreshVariance`.
	private static TimeSpan GetRandomInterval() {
		double variance = System.Random.Shared.NextDouble();
		variance = 2 * variance - 1; // [-1.0, 1.0)
		return _refreshInterval + _refreshVariance * variance;
	}

	// Read in all saved statuses, and pick a random one from the list.
	private static async Task<Status> GetRandomStatus() {
		IList<Status> statuses = await ReadStatusesFromFile();
		if (statuses.Count == 0)
			throw new InvalidOperationException("No saved statuses found.");
		int i = System.Random.Shared.Next(0, statuses.Count);
		return statuses[i];
	}
}
