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
	public static DateOnly Closest(this DateOnly dateRef, DateOnly dateA, DateOnly dateB) {
		TimeSpan deltaA = Subtract(dateRef, dateA).Duration();
		TimeSpan deltaB = Subtract(dateRef, dateB).Duration();
		return (deltaA > deltaB) ? dateB : dateA;
	}

	// Returns the next/previous day of week.
	// When called without `isInclusive`, does not return the input date;
	// returns a week out from the input date instead.
	public static DateOnly NextDayOfWeek(this DateOnly dateIn, DayOfWeek day, bool isInclusive=false) {
		DayOfWeek dayIn = dateIn.DayOfWeek;
		int daysDelta = (int)day - (int)dayIn;
		daysDelta = (daysDelta + 7) % 7;	// ensure result falls in [0,6]
		if (!isInclusive && dayIn == day)
			daysDelta += 7;
		return dateIn.AddDays(daysDelta);
	}
	public static DateOnly PreviousDayOfWeek(this DateOnly dateIn, DayOfWeek day, bool isInclusive=false) {
		DayOfWeek dayIn = dateIn.DayOfWeek;
		int daysDelta = (int)dayIn - (int)day;
		daysDelta = (daysDelta + 7) % 7;	// ensure result falls in [0,6]
		if (!isInclusive && dayIn == day)
			daysDelta += 7;
		return dateIn.AddDays(-daysDelta);
	}

	// Returns the next/previous date of year.
	// When called without `isInclusive`, does not return the input date;
	// returns a year out from the input date instead.
	public static DateOnly NextDateOfYear(this DateOnly dateIn, int month, int day, bool isInclusive = false) {
		int year = dateIn.Year;
		DateOnly date = new (year, month, day);
		if (!isInclusive && date <= dateIn)
			date = new DateOnly(year+1, month, day);
		return date;
	}
	public static DateOnly PreviousDateOfYear(this DateOnly dateIn, int month, int day, bool isInclusive = false) {
		int year = dateIn.Year;
		DateOnly date = new (year, month, day);
		if (!isInclusive && date >= dateIn)
			date = new DateOnly(year-1, month, day);
		return date;
	}

	// Returns the next/previous date of year.
	// When called without `isInclusive`, does not return the input date;
	// returns a year out from the input date instead.
	public static DateOnly NextLunarPhase(this DateOnly date_in, RecurringEvent.LunarPhase lunarPhase) =>
		throw new NotImplementedException("Lunar phase calculations unimplemented.");
	public static DateOnly PreviousLunarPhase(this DateOnly date_in, RecurringEvent.LunarPhase lunarPhase) =>
		throw new NotImplementedException("Lunar phase calculations unimplemented.");
}
