namespace Irene.Utils;

class TimeZones {
	private static readonly ConcurrentDictionary<string, TimeZoneInfo> _tableCompiled;

	// Force static initializer to run.
	public static void Init() { return; }
	static TimeZones() {
		_tableCompiled = new ();
		foreach (TimeZoneInfo timeZone in TimeZoneInfo.GetSystemTimeZones())
			_tableCompiled.TryAdd(timeZone.DisplayName, timeZone);
	}

	public static List<string> GetTimeZoneDisplayStrings() =>
		new (_tableCompiled.Keys);
	public static TimeZoneInfo TimeZoneFromDisplayString(string displayName) =>
		_tableCompiled[displayName];
}
