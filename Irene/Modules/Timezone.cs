namespace Irene.Modules;

class Timezone {
	public static readonly TimeZoneInfo
		//TimeZone_PST = TimeZoneInfo.FindSystemTimeZoneById("PST"),
		//TimeZone_PDT = TimeZoneInfo.FindSystemTimeZoneById("Etc/GMT+7"),
		//TimeZone_MST = TimeZoneInfo.FindSystemTimeZoneById("MST"),
		//TimeZone_MDT = TimeZoneInfo.FindSystemTimeZoneById("Etc/GMT+6"),
		//TimeZone_CST = TimeZoneInfo.FindSystemTimeZoneById("CST"),
		//TimeZone_CDT = TimeZoneInfo.FindSystemTimeZoneById("Etc/GMT+5"),
		//TimeZone_EST = TimeZoneInfo.FindSystemTimeZoneById("EST"),
		//TimeZone_EDT = TimeZoneInfo.FindSystemTimeZoneById("Etc/GMT+4");
		TimeZone_PST = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"),
		TimeZone_PDT = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"),
		TimeZone_MST = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"),
		TimeZone_MDT = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"),
		TimeZone_CST = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"),
		TimeZone_CDT = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"),
		TimeZone_EST = TimeZoneInfo.FindSystemTimeZoneById("America/Denver"),
		TimeZone_EDT = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");

	static Timezone() {
		foreach (TimeZoneInfo tz in TimeZoneInfo.GetSystemTimeZones()) {
			Log.Debug("  {Timezone}", tz.Id);
		}
	}

	public static TimeZoneInfo? GetUserLocalTime(DiscordUser user) =>
		GetUserLocalTime(user.Id);
	public static TimeZoneInfo? GetUserLocalTime(ulong userId) {
		return TimeZone_Server;
	}
}
