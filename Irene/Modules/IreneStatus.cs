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

		public DiscordActivity ToActivity(bool isDebug=false) => isDebug
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

	private static LongTimer? _timerRefresh = null;
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

	static IreneStatus() {
		Util.CreateIfMissing(_pathStatuses);
		Util.CreateIfMissing(_pathCurrent);

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
		await _queueStatuses.Run(
			File.WriteAllLinesAsync(_pathStatuses, lines)
		);
	}
	private static async Task<IList<Status>> ReadStatusesFromFile() {
		List<string> lines = new (await
			_queueStatuses.Run(File.ReadAllLinesAsync(_pathStatuses))
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
			NextRefresh.Value.ToString(_formatDateTime),
			CurrentStatus.ToString()
		);
		await _queueCurrent.Run(
			File.WriteAllTextAsync(_pathCurrent, output)
		);
	}
	// This method does NOT update the internal state of `IreneStatus`.
	private static async Task<(DateTimeOffset?, Status?)> ReadCurrentFromFile() {
		return await _queueCurrent.Run(T());
		static async Task<(DateTimeOffset?, Status?)> T() {
			DateTimeOffset? refresh = null;
			Status? status = null;
			using StreamReader file = File.OpenText(_pathCurrent);

			string line = await file.ReadLineAsync() ?? "";
			string[] split = line.Split(_separator, 2);
			if (split.Length < 2)
				return (null, null);

			try {
				refresh = DateTimeOffset.ParseExact(
					split[0],
					_formatDateTime,
					null
				);
				status = Status.FromString(split[1]);
			} catch (FormatException) { }

			return (refresh, status);
		}
	}


	// --------
	// Other internal helper/component methods:
	// --------

	// Populates fields and sets a status starting from an uninitialized
	// starting state.
	private static async Task InitializeCurrent() {
		(DateTimeOffset? refresh, Status? status) = await
			ReadCurrentFromFile();

		if (refresh is null || status is null) {
			refresh = DateTimeOffset.UtcNow + GetRandomInterval();
			status = await GetRandomStatus();
		}

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
		DiscordActivity activity = status.ToActivity(IsDebug);
		UserStatus userStatus = IsDebug
			? UserStatus.DoNotDisturb
			: UserStatus.Online;
		Task taskDiscord = Client.UpdateStatusAsync(activity, userStatus);

		await Task.WhenAll(taskFile, taskDiscord);

		_timerRefresh?.Cancel();
		_timerRefresh = LongTimer.Create(end);
		_timerRefresh.Elapsed += async (timer, e) => {
			await RefreshHandler(timer, e);
		};
	}

	// Used as:
	//     _timerRefresh.Elapsed += async (timer, e) => {
	//         await RefreshHandler(timer, e);
	//     };
	private static async Task RefreshHandler(object? timer, ElapsedEventArgs e) {
		_timerRefresh?.Cancel();

		await SetRandom();

		// `NextRefresh` is not null after awaiting `Set()`.
		_timerRefresh = LongTimer.Create(NextRefresh!.Value);
		_timerRefresh.Elapsed += async (timer, e) => {
			await RefreshHandler(timer, e);
		};
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
