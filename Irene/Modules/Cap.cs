namespace Irene.Modules;

class Cap {
	public record struct HideableString(string String, bool IsEphemeral);

	private static readonly DateOnly
		_date_s3CapLifted = new (2022,  5, 10);


	// --------
	// Cap calculation methods.
	// --------

	public static HideableString DisplayValor(DateTimeOffset dateTime) {
		string resource = "Valor";
		const int weekly_valor = 750;

		// Pre-9.0.5.
		if (dateTime < Date_Patch905.UtcResetTime())
			return FormatCapUnavailable(resource, "patch 9.0.5");

		// Season 1, post-9.0.5.
		if (dateTime < Date_Season2.UtcResetTime()) {
			int week = WholeWeeksSince(dateTime, Date_Patch905);
			int cap = 5000 + week * weekly_valor;
			return FormatCapWeekly(resource, cap, week + 1);
		}

		// Season 2, pre-9.1.5.
		if (dateTime < Date_Patch915.UtcResetTime()) {
			int week = WholeWeeksSince(dateTime, Date_Season2);
			int cap = 750 + week * weekly_valor;
			return FormatCapWeekly(resource, cap, week + 1);
		}

		// Season 2, post-9.1.5.
		if (dateTime < Date_Season3.UtcResetTime())
			return FormatCapLifted(resource, "season 2");

		// Season 3, pre-cap removal.
		if (dateTime < _date_s3CapLifted.UtcResetTime()) {
			int week = WholeWeeksSince(dateTime, Date_Season3);
			int cap = 750 + week * weekly_valor;
			return FormatCapWeekly(resource, cap, week + 1);
		}

		// Season 3, post-cap removal.
		if (dateTime >= _date_s3CapLifted.UtcResetTime())
			return FormatCapLifted(resource, "season 3");

		throw new InvalidOperationException($"{resource} cap calculation resulted in illegal internal state.");
	}

	public static HideableString DisplayConquest(DateTimeOffset dateTime) {
		string resource = "Conquest";
		const int
			weekly_conquest_old = 550,
			weekly_conquest_new = 500;

		// Pre-Shadowlands launch.
		if (dateTime < Date_Patch902.UtcResetTime())
			return FormatCapUnsupported(resource, "patch 9.0.2");

		// Season 1 preseason (9.0.2).
		if (dateTime < Date_Season1.UtcResetTime())
			return FormatCapRestricted(resource, "pre-season");

		// Season 1 (9.0.2).
		if (dateTime < Date_Season2.UtcResetTime()) {
			int week = WholeWeeksSince(dateTime, Date_Season1);
			int cap = 550 + week * weekly_conquest_old;
			return FormatCapWeekly(resource, cap, week + 1);
		}

		// Season 2, pre-9.1.5.
		if (dateTime < Date_Patch915.UtcResetTime()) {
			int week = WholeWeeksSince(dateTime, Date_Season2);
			int cap = 1000 + week * weekly_conquest_old;
			return FormatCapWeekly(resource, cap, week + 1);
		}

		// Season 2, post-9.1.5.
		if (dateTime < Date_Season3.UtcResetTime())
			return FormatCapLifted(resource, "season 2");

		// Season 3, pre-cap removal.
		if (dateTime < _date_s3CapLifted.UtcResetTime()) {
			int week = WholeWeeksSince(dateTime, Date_Season3);
			int cap = 1000 + week * weekly_conquest_new;
			return FormatCapWeekly(resource, cap, week + 1);
		}

		// Season 3, post-cap removal.
		if (dateTime >= _date_s3CapLifted.UtcResetTime())
			return FormatCapLifted(resource, "season 3");

		throw new InvalidOperationException($"{resource} cap calculation resulted in illegal internal state.");
	}

	public static HideableString DisplayRenown(DateTimeOffset dateTime) {
		string resource = "Renown";

		// Pre-Shadowlands launch.
		if (dateTime < Date_Patch902.UtcResetTime())
			return FormatCapUnavailable(resource, "patch 9.0.2");

		// Patch 9.0.
		if (dateTime < Date_Patch910.UtcResetTime()) {
			int week = WholeWeeksSince(dateTime, Date_Patch902);
			int cap = week switch {
				<  8 => 3 + 3 * week,
				< 16 => 26 + 2 * (week - 8),
				_ => 40,
			};
			// s1 cap was 40.
			// week is 0-indexed and needs to be incremented for display.
			return (cap < 40)
				? FormatCapWeekly(resource, cap, week + 1)
				: FormatCapMaxed(resource, cap);
		}

		// Patch 9.1.
		if (dateTime < Date_Patch915.UtcResetTime()) {
			int week = WholeWeeksSince(dateTime, Date_Patch910);
			int cap = week switch {
				<  1 => 42,
				<  9 => 45 + 3 * (week - 1),
				< 16 => 66 + 2 * (week - 9),
				_ => 80,
			};
			// week is 0-indexed and needs to be incremented for display.
			return (cap < 80)
				? FormatCapWeekly(resource, cap, week + 1)
				: FormatCapMaxed(resource, cap);
		}

		// Patch 9.1.5.
		if (dateTime >= Date_Patch915.UtcResetTime())
			return FormatCapMaxed(resource, 80);

		throw new InvalidOperationException($"{resource} cap calculation resulted in illegal internal state.");
	}

	public static HideableString DisplayTorghast(DateTimeOffset dateTime) {
		string resource = "Tower Knowledge";

		// Pre-Shadowlands launch.
		if (dateTime < Date_Patch910.UtcResetTime())
			return FormatCapUnavailable(resource, "patch 9.1.0");

		// Patch 9.1.
		if (dateTime < Date_Patch915.UtcResetTime()) {
			TimeSpan duration = dateTime - Date_Patch910.UtcResetTime();
			int week = duration.Days / 7;  // int division!
			int cap = week switch {
				<  1 => 180, // 90x2
				<  2 => 400, // 90x2 + 110x2
				<  3 => 700, // 90x2 + 110x2 + 125x2 ... + 50 ???
				< 10 => 1060 + 360 * (week - 3),
				_ => 3510,
			};
			// week is 0-indexed and needs to be incremented for display.
			return (cap < 3510)
				? FormatCapWeekly(resource, cap, week + 1)
				: FormatCapMaxed(resource, cap);
		}

		// Patch 9.1.5.
		if (dateTime >= Date_Patch915.UtcResetTime())
			return FormatCapMaxed(resource, 3510);

		throw new InvalidOperationException($"{resource} cap calculation resulted in illegal internal state.");
	}


	// --------
	// Internal helper methods.
	// --------

	// Returns the 0-indexed week number. (+1 for display)
	private static int WholeWeeksSince(DateTimeOffset dateTime, DateOnly epoch) {
		TimeSpan duration = dateTime - epoch.UtcResetTime();
		// `Days` is the largest property in `TimeSpan`.
		return duration.Days / 7; // int division!
	}

	// When the cap cannot be calculated, the resource name is emphasized.
	// For other scenarios, the actual cap value is emphasized.
	private static HideableString FormatCapUnsupported(string resource, string epoch) =>
		new ($"Sorry, **{resource}** cap is unsupported until {epoch}.", true);
	private static HideableString FormatCapUnavailable(string resource, string epoch) =>
		new ($"**{resource}** cannot be earned until {epoch}.", true);
	private static HideableString FormatCapRestricted(string resource, string epoch) =>
		new ($"Current {resource} cap: **0** ({epoch})", false);
	private static HideableString FormatCapWeekly(string resource, int cap, int week) =>
		new ($"Current {resource} cap: **{cap}** (week {week})", false);
	private static HideableString FormatCapMaxed(string resource, int cap) =>
		new ($"Current {resource} cap: **{cap}** (max)", false);
	private static HideableString FormatCapLifted(string resource, string period) =>
		new ($"{resource} is **uncapped** for the rest of {period}.", false);
}
