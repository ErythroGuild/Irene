namespace Irene;

static class TimeZones {
	private static readonly ReadOnlyDictionary<string, TimeZoneInfo> _tableCompiled;

	public static void Init() { }
	static TimeZones() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		ConcurrentDictionary<string, TimeZoneInfo> tableCompiled = new ();
		foreach (TimeZoneInfo timeZone in TimeZoneInfo.GetSystemTimeZones())
			tableCompiled.TryAdd(timeZone.DisplayName, timeZone);
		_tableCompiled = new (tableCompiled);

		Log.Information("  Initialized module: TimeZones");
		Log.Debug("    TimeZone cache initialized.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public static List<string> GetTimeZoneDisplayStrings() =>
		new (_tableCompiled.Keys);
	public static TimeZoneInfo TimeZoneFromDisplayString(string displayName) =>
		_tableCompiled[displayName];
}
