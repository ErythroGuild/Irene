namespace Irene.Utils;

using System.Diagnostics;
using System.Timers;

using Irene.Exceptions;

static partial class Util {
	// Convenience method for constructing a timer with AutoReset.
	public static Timer CreateTimer(TimeSpan timeSpan, bool autoReset) =>
		CreateTimer(timeSpan.TotalMilliseconds, autoReset);
	public static Timer CreateTimer(double totalMilliseconds, bool autoReset) =>
		new (totalMilliseconds) { AutoReset = autoReset };

	// Convenience method for "restarting" a timer.
	public static void Restart(this Timer timer) {
		timer.Stop();
		timer.Start();
	}

	// Convenience  methods for stopping a timer and printing the value.
	// The log output is always: "{Time} msec elapsed."
	public static void LogMsec(
		this Stopwatch stopwatch,
		int indentLevel,
		bool doStopTimer=true,
		LogLevel logLevel=LogLevel.Debug
	) {
		// Get time (msec).
		// This should happen ASAP, for result accuracy.
		if (doStopTimer)
			stopwatch.Stop();
		long msec = stopwatch.ElapsedMilliseconds;

		// Construct indent string.
		StringBuilder indent = new ();
		for (int i=0; i<indentLevel; i++)
			indent.Append("  ");

		// Get the correct logging function.
		Action<string, string> logger = logLevel switch {
			LogLevel.Trace       => Log.Verbose,
			LogLevel.Debug       => Log.Debug,
			LogLevel.Information => Log.Information,
			LogLevel.Warning     => Log.Warning,
			LogLevel.Error       => Log.Error,
			LogLevel.Critical    => Log.Fatal,

			LogLevel.None => (_, _) => {},
			_ => throw new UnclosedEnumException(typeof(LogLevel), logLevel),
		};

		// Log.
		logger($"{indent} {{Time}} msec elapsed.", msec.ToString());
	}
}
