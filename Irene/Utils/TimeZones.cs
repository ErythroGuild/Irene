namespace Irene.Utils;

class TimeZones {
	private static readonly ConcurrentDictionary<string, TimeZoneInfo> _tableCompiled;

	// Force static initializer to run.
	public static void Init() { }
	static TimeZones() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		_tableCompiled = new ();
		foreach (TimeZoneInfo timeZone in TimeZoneInfo.GetSystemTimeZones())
			_tableCompiled.TryAdd(timeZone.DisplayName, timeZone);

		Log.Information("  Initialized util: TimeZones");
		Log.Debug("    TimeZone cache initialized.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public static List<string> GetTimeZoneDisplayStrings() =>
		new (_tableCompiled.Keys);
	public static TimeZoneInfo TimeZoneFromDisplayString(string displayName) =>
		_tableCompiled[displayName];
}
