namespace Irene.Utils;

static partial class Util {
	// Directly returns the TimeOnly from a TimeSpan of a DateTime(Offset).
	public static TimeOnly TimeOnly(this DateTimeOffset dateTime) =>
		System.TimeOnly.FromTimeSpan(dateTime.TimeOfDay);
	public static TimeOnly TimeOnly(this DateTime dateTime) =>
		System.TimeOnly.FromTimeSpan(dateTime.TimeOfDay);

	// Returns a TimeSpan of the number of days between the dates.
	// Can be negative. DST or time zones are not accounted for.
	public static TimeSpan Subtract(DateOnly a, DateOnly b) {
		int days = a.DayNumber - b.DayNumber;
		return TimeSpan.FromDays(days);
	}

	// Returns the closest date of the two to the reference date.
	public static DateOnly Closest(this DateOnly date_ref, DateOnly date_A, DateOnly date_B) {
		TimeSpan days_A = Subtract(date_ref, date_A).Duration();
		TimeSpan days_B = Subtract(date_ref, date_B).Duration();
		return (days_A > days_B) ? date_B : date_A;
	}

	// Returns the next/previous day of week.
	// When called without `isInclusive`, does not return the input date;
	// returns a week out from the input date instead.
	public static DateOnly NextDayOfWeek(this DateOnly date_in, DayOfWeek day, bool isInclusive=false) {
		DayOfWeek day_in = date_in.DayOfWeek;
		int days_delta = (int)day - (int)day_in;
		days_delta = (days_delta + 7) % 7;	// ensure result falls in [0,6]
		if (!isInclusive && day_in == day)
			days_delta += 7;
		return date_in.AddDays(days_delta);
	}
	public static DateOnly PreviousDayOfWeek(this DateOnly date_in, DayOfWeek day, bool isInclusive=false) {
		DayOfWeek day_in = date_in.DayOfWeek;
		int days_delta = (int)day_in - (int)day;
		days_delta = (days_delta + 7) % 7;	// ensure result falls in [0,6]
		if (!isInclusive && day_in == day)
			days_delta += 7;
		return date_in.AddDays(-days_delta);
	}

	// Returns the next/previous date of year.
	// When called without `isInclusive`, does not return the input date;
	// returns a year out from the input date instead.
	public static DateOnly NextDateOfYear(this DateOnly date_in, int month, int day, bool isInclusive = false) {
		int year = date_in.Year;
		DateOnly date = new (year, month, day);
		if (!isInclusive && date <= date_in)
			date = new DateOnly(year+1, month, day);
		return date;
	}
	public static DateOnly PreviousDateOfYear(this DateOnly date_in, int month, int day, bool isInclusive = false) {
		int year = date_in.Year;
		DateOnly date = new (year, month, day);
		if (!isInclusive && date >= date_in)
			date = new DateOnly(year-1, month, day);
		return date;
	}

	// Returns the next/previous date of year.
	// When called without `isInclusive`, does not return the input date;
	// returns a year out from the input date instead.
	public static DateOnly NextLunarPhase(this DateOnly date_in, RecurringEvent.LunarPhase lunarPhase) {
		throw new Exception("Unimplemented feature: lunar phase calculation.");
	}
	public static DateOnly PreviousLunarPhase(this DateOnly date_in, RecurringEvent.LunarPhase lunarPhase) {
		throw new Exception("Unimplemented feature: lunar phase calculation.");
	}
}
