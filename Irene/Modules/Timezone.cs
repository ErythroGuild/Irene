namespace Irene.Modules;

class Timezone {
	public static class Const {
		public static readonly TimeZoneInfo
			PT, PST, PDT,
			MT, MST, MDT,
			CT, CST, CDT,
			ET, EST, EDT;

		static Const() {
			PT  = Get(@"America/Los_Angeles")
				?? throw new ImpossibleException();
			PST = Get(new TimeSpan(-8, 0, 0));
			PDT = Get(new TimeSpan(-7, 0, 0));

			MT  = Get(@"America/Denver")
				?? throw new ImpossibleException();
			MST = Get(new TimeSpan(-7, 0, 0));
			MDT = Get(new TimeSpan(-6, 0, 0));

			CT  = Get(@"America/Chicago")
				?? throw new ImpossibleException();
			CST = Get(new TimeSpan(-6, 0, 0));
			CDT = Get(new TimeSpan(-5, 0, 0));

			ET  = Get(@"America/New_York")
				?? throw new ImpossibleException();
			EST = Get(new TimeSpan(-5, 0, 0));
			EDT = Get(new TimeSpan(-4, 0, 0));
		}
	}

	private static readonly ConcurrentDictionary<TimeSpan, TimeZoneInfo> _listByOffset;
	private static readonly IReadOnlyDictionary <string  , TimeZoneInfo> _listByIanaId;

	// Initialize timezone cache.
	static Timezone() {
		_listByOffset = new ();

		ConcurrentDictionary<string, TimeZoneInfo> timezones = new ();

		// Add system timezones.
		// These may need to be converted first, if not in IANA format.
		foreach (TimeZoneInfo timezone in TimeZoneInfo.GetSystemTimeZones()) {
			if (timezone.HasIanaId) {
				timezones.TryAdd(timezone.Id, timezone);
			} else {
				TimeZoneInfo.TryConvertWindowsIdToIanaId(timezone.Id, out string? id);
				if (id is not null)
					timezones.TryAdd(id, timezone);
			}
		}

		_listByIanaId = timezones;
	}

	public static TimeZoneInfo Get(TimeSpan offset) {
		return null!;
	}

	public static TimeZoneInfo? Get(string ianaId) =>
		_listByIanaId.TryGetValue(ianaId, out TimeZoneInfo? timezone)
			? timezone
			: null;

	public static Task<TimeZoneInfo?> Get(DiscordUser user) =>
		Get(user.Id);
	public static async Task<TimeZoneInfo?> Get(ulong userId) {
		return TimeZone_Server;
	}

	public static Task Set(DiscordUser user, TimeZoneInfo timezone) =>
		Set(user.Id, timezone);
	public static async Task Set(ulong userId, TimeZoneInfo timezone) {
		;
	}

	public static Task Clear(DiscordUser user) => Clear(user.Id);
	public static async Task Clear(ulong userId) {

	}
}
