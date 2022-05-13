using System.Timers;

namespace Irene.Commands;

class Slowmode : AbstractCommand, IInit {
	private record struct Data {
		public Timer? Timer;
		public readonly ulong ChannelId;
		public readonly int? LimitPrevious;
		public readonly DateTimeOffset TimeEnd;

		private const string _separator = "|||";

		public Data(
			Timer? timer,
			ulong channelId,
			int? limitPrevious,
			DateTimeOffset timeEnd
		) {
			Timer = timer;
			ChannelId = channelId;
			LimitPrevious = limitPrevious;
			TimeEnd = timeEnd;
		}

		public string Serialize() =>
			string.Join(_separator, new object[] {
				ChannelId,
				LimitPrevious?.ToString() ?? "null",
				TimeEnd.ToString("u")
			} );
		public static Data Deserialize(string data) {
			string[] split = data.Split(_separator, 3);
			return new Data(
				null,
				ulong.Parse(split[0]),
				(split[1] != "null")
					? int.Parse(split[1])
					: null,
				DateTimeOffset.ParseExact(split[2], "u", null)
			);
		}
	}

	private static readonly object _lock = new ();
	private const string
		_pathSlowmode = @"data/slowmode.txt",
		_pathTemp = @"data/slowmode-temp.txt";
	private const string
		_time15sec = "15 sec",
		_time1min = "1 min",
		_time2min = "2 min",
		_time5min = "5 min",
		_time15min = "15 min",
		_time30min = "30 min",
		_time1hrs = "1 hrs",
		_time2hrs = "2 hrs";

	private static readonly ConcurrentDictionary<ulong, Data> _channelTimers = new ();

	private static readonly ReadOnlyDictionary<string, TimeSpan> _tableTimes =
		new (new ConcurrentDictionary<string, TimeSpan>() {
			[_time15sec] = TimeSpan.FromSeconds(15),
			[_time1min ] = TimeSpan.FromMinutes(1),
			[_time2min ] = TimeSpan.FromMinutes(2),
			[_time5min ] = TimeSpan.FromMinutes(5),
			[_time15min] = TimeSpan.FromMinutes(15),
			[_time30min] = TimeSpan.FromMinutes(30),
			[_time1hrs ] = TimeSpan.FromHours(1),
			[_time2hrs ] = TimeSpan.FromHours(2),
		} );

	private static readonly ReadOnlyCollection<CommandOptionEnum> _optionsDuration =
		new (new List<CommandOptionEnum> {
			new ("15 minutes", _time15min),
			new ("30 minutes", _time30min),
			new ("1 hour", _time1hrs),
			new ("2 hours", _time2hrs),
		} );
	private static readonly ReadOnlyCollection<CommandOptionEnum> _optionsInterval =
		new (new List<CommandOptionEnum> {
			new ("15 seconds", _time15sec),
			new ("1 minute", _time1min),
			new ("2 minutes", _time2min),
			new ("5 minutes", _time5min),
		} );

	public static void Init() { }
	static Slowmode() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		Util.CreateIfMissing(_pathSlowmode, _lock);

		// Read in previous data.
		List<Data> data = ReadAllData();
		foreach (Data data_i in data) {
			DateTimeOffset now = DateTimeOffset.UtcNow;
			DiscordChannel channel = 
				Guild!.GetChannel(data_i.ChannelId);
			bool is_canceled =
				(channel.PerUserRateLimit ?? 0) ==
				(data_i.LimitPrevious ?? 0);
			if (is_canceled)
				continue;

			if (now < data_i.TimeEnd) {
				// Start new timer if end time is in the future.
				TimeSpan duration = data_i.TimeEnd - now;
				Timer timer = Util.CreateTimer(duration, false);
				timer.Elapsed += async (obj, e) => {
					await channel.ModifyAsync((channel) => {
						channel.PerUserRateLimit = data_i.LimitPrevious;
					});
					_channelTimers.TryRemove(channel.Id, out _);
					UpdateSavedData();
					Log.Information("Slowmode turned off for #{Channel}.", channel.Name);
				};
				_channelTimers.TryAdd(channel.Id, new (
					timer,
					channel.Id,
					data_i.LimitPrevious,
					data_i.TimeEnd
				) );
				timer.Start();
			} else {
				// Immediately end slowmode if end time has passed.
				channel.ModifyAsync((channel) => {
					channel.PerUserRateLimit = data_i.LimitPrevious;
				});
				Log.Information("Slowmode turned off for #{Channel}.", channel.Name);
			}
		}
		UpdateSavedData();

		Log.Information("  Initialized command: /slowmode");
		Log.Debug("    Existing slowmode settings processed.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public override List<string> HelpPages =>
		new () { new List<string> {
			@"`:lock: /slowmode <channel> <duration> <interval>` turns on slow mode.",
			"A few common increments are listed as options for convenience.",
			"If these are insufficient, channels can be directly edited as well.",
		}.ToLines() };

	public override List<InteractionCommand> SlashCommands =>
		new () {
			new ( new (
				"slowmode",
				"Limit the rate of messages sent in a channel.",
				new List<CommandOption> {
					new (
						"channel",
						"The channel to limit.",
						ApplicationCommandOptionType.Channel,
						required: true,
						channelTypes: new List<ChannelType>
							{ ChannelType.Text }
					),
					new (
						"duration",
						"The length of time to limit message for.",
						ApplicationCommandOptionType.String,
						required: true,
						choices: _optionsDuration
					),
					new (
						"interval",
						"The length of time between messages.",
						ApplicationCommandOptionType.String,
						required: true,
						choices: _optionsInterval
					),
				},
				defaultPermission: true,
				type: ApplicationCommandType.SlashCommand
			), DeferAsync, RunAsync )
		};

	public static async Task DeferAsync(TimedInteraction interaction) {
		DeferrerHandler handler = new (interaction, true);
		await SetSlowmodeAsync(handler);
	}
	public static async Task RunAsync(TimedInteraction interaction) {
		DeferrerHandler handler = new (interaction, false);
		await SetSlowmodeAsync(handler);
	}

	public static async Task SetSlowmodeAsync(DeferrerHandler handler) {
		// Check for permissions.
		bool doContinue = await handler.Interaction
			.CheckAccessAsync(false, AccessLevel.Officer);
		if (!doContinue)
			return;

		List<DiscordInteractionDataOption> args =
			handler.GetArgs();

		// Get resolved channel.
		DiscordChannel channel =
			handler.Interaction.Interaction.GetTargetChannel();
		int? limit_previous = channel.PerUserRateLimit;

		// If channel is already in slowmode, error out.
		if (_channelTimers.ContainsKey(channel.Id)) {
			if (handler.IsDeferrer) {
				await Command.DeferAsync(handler, true);
				return;
			}
			await Command.SubmitResponseAsync(
				handler.Interaction,
				$":stopwatch: Slowmode was already active for {channel.Mention}. No changes made.",
				$"#{channel.Name} already in slowmode (no changes made).",
				LogLevel.Information,
				"rate: 1 message / {Limit} sec".AsLazy(),
				channel.PerUserRateLimit ?? 0
			);
			return;
		}

		// Setting a channel to slowmode should always be visible.
		if (handler.IsDeferrer) {
			await Command.DeferAsync(handler, false);
			return;
		}

		// Convert parameters.
		TimeSpan duration = _tableTimes[(string)args[1].Value];
		DateTimeOffset time_end = DateTimeOffset.UtcNow + duration;
		TimeSpan limit_timespan =
			_tableTimes[(string)args[2].Value];
		int limit =
			(int)Math.Round(limit_timespan.TotalSeconds);

		// Initialize data object.
		Data data = new (
			Util.CreateTimer(duration, false),
			channel.Id,
			limit_previous,
			time_end
		);
		data.Timer!.Elapsed += async (obj, e) => {
			await channel.ModifyAsync((channel) => {
				channel.PerUserRateLimit = data.LimitPrevious;
			});
			_channelTimers.TryRemove(channel.Id, out _);
			UpdateSavedData();
			Log.Information("Slowmode turned off for #{Channel}.", channel.Name);
		};
		_channelTimers.TryAdd(channel.Id, data);
		UpdateSavedData();

		// Update channel & start timer.
		await channel.ModifyAsync((channel) => {
			channel.PerUserRateLimit = limit;
		});
		data.Timer!.Start();

		// Respond.
		string response =
			$"Slowmode activated for {channel.Mention}, until " +
			$"{time_end.Timestamp(Util.TimestampStyle.TimeShort)}." +
			" :stopwatch:";
		await Command.SubmitResponseAsync(
			handler.Interaction,
			response,
			$"Slowmode activated for #{channel.Name}.",
			LogLevel.Debug,
			"rate: 1 message / {Limit} sec".AsLazy(),
			limit
		);
	}

	// Read in any saved data and deserialize to objects.
	private static List<Data> ReadAllData() {
		List<string> lines = new ();
		lock (_lock) {
			lines.AddRange(File.ReadAllLines(_pathSlowmode));
		};

		List<Data> data = new ();
		foreach (string line in lines)
			data.Add(Data.Deserialize(line));

		return data;
	}
	// Write out all the saved data in serialized form.
	private static void WriteAllData(List<Data> data) {
		List<string> lines = new ();
		foreach (Data data_i in data)
			lines.Add(data_i.Serialize());

		lock (_lock) {
			File.WriteAllLines(_pathTemp, lines);
			File.Delete(_pathSlowmode);
			File.Move(_pathTemp, _pathSlowmode);
		}
	}

	// Write out the data in the current cache.
	private static void UpdateSavedData() {
		WriteAllData(new List<Data>(_channelTimers.Values));
	}
}
