using System;
using System.Collections.Generic;

using DSharpPlus.Entities;

namespace Irene.Utils;

static class DiscordFormat {
	// Adjusts to 12-/24-hour clock based on user's client locale.
	// Default style (no format specifier) is equivalent to ":f"
	// (DateTimeShort).
	public enum TimestampStyle {
		Relative,  // "2 months ago", "in an hour"
		TimeShort, //  h:mm tt       |  HH:mm
		TimeLong,  //  h:mm:ss tt    |  HH:mm:ss
		DateShort, //  MM/dd/yyyy    |  dd/MM/yyyy
		DateLong,  //  MMMM d, yyyy  |  dd MMMM yyyy
		DateTimeShort, //  MMMM d, yyyy h:mm tt        |  dd MMMM yyyy HH:mm
		DateTimeLong,  //  dddd, MMMM d, yyyy h:mm tt  |  dddd, dd MMMM yyyy HH:mm
	};
	// Returns a formatted timestamp from a given DateTimeOffset.
	// Valid format strings are currently undocumented.
	public static string Timestamp(this DateTimeOffset time, TimestampStyle style=TimestampStyle.DateTimeShort) =>
		$"<t:{time.ToUnixTimeSeconds()}:{_timestampTable[style]}>";
	public static string Timestamp(this DateTimeOffset time, string format="f") =>
		$"<t:{time.ToUnixTimeSeconds()}:{format}>";
	private readonly static Dictionary<TimestampStyle, string> _timestampTable = new () {
		{ TimestampStyle.Relative,  "R" },
		{ TimestampStyle.TimeShort, "t" },
		{ TimestampStyle.TimeLong,  "T" },
		{ TimestampStyle.DateShort, "d" },
		{ TimestampStyle.DateLong,  "D" },
		{ TimestampStyle.DateTimeShort, "f" },
		{ TimestampStyle.DateTimeLong,  "F" },
	};

	// Prints the "user#tag" of a user.
	public static string Tag(this DiscordUser user) =>
		$"{user.Username}#{user.Discriminator}";
	public static string Tag(this DiscordMember user) =>
		$"{user.Username}#{user.Discriminator}";

	// Prints a DiscordColor in "#RRGGBB" format.
	public static string HexCode(this DiscordColor color) =>
		$"#{color:X6}";
}
