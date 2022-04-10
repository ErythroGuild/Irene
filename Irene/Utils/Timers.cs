using System.Timers;

namespace Irene.Utils;

static partial class Util {
	// Convenience method for "restarting" a timer.
	public static void Restart(this Timer timer) {
		timer.Stop();
		timer.Start();
	}

	// Convenience  methods for stopping a timer and printing the value.
	public static void LogMsecDebug(this Stopwatch stopwatch, string template, bool doStopTimer=true) {
		if (doStopTimer)
			stopwatch.Stop();
		long msec = stopwatch.ElapsedMilliseconds;
		Log.Debug(template, msec);
	}
	public static void LogMsecInformation(this Stopwatch stopwatch, string template, bool doStopTimer=true) {
		if (doStopTimer)
			stopwatch.Stop();
		long msec = stopwatch.ElapsedMilliseconds;
		Log.Information(template, msec);
	}
	public static void LogMsecWarning(this Stopwatch stopwatch, string template, bool doStopTimer=true) {
		if (doStopTimer)
			stopwatch.Stop();
		long msec = stopwatch.ElapsedMilliseconds;
		Log.Warning(template, msec);
	}
}
