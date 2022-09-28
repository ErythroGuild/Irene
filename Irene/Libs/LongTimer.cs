using System.Timers;

namespace Irene;

// An extended timer class based on `DateTimeOffset`.
// An inner `System.Timers.Timer` loops at max interval length, until
// the remaining interval is small enough to be within the inner timer's
// capacity.
class LongTimer : IDisposable {

	// --------
	// Properties, fields, and constants:
	// --------

	public TimeSpan Interval { get; private set; }
	public DateTimeOffset End { get; private set; }
	public TimeSpan Remaining => End - DateTimeOffset.UtcNow;
	public bool IsEnabled { get; private set; } = true;
	public bool AutoReset { get; set; }
	// A small threshold for triggering the elapsed event.
	public static readonly TimeSpan InternalAccuracy =
		TimeSpan.FromMilliseconds(20);

	public event ElapsedEventHandler? Elapsed;
	protected virtual void OnElapsed(ElapsedEventArgs e) {
		if (!IsEnabled)
			return;

		ElapsedEventHandler? handler = Elapsed;
		handler?.Invoke(this, e);
	}

	// According to documentation, the max interval value (in msec) for
	// `System.Timers.Timer` is `<= Int32.MaxValue`. Here it's set smaller
	// to be conservative.
	// This value can be reduced to test if `LongTimer` is working.
	private static readonly TimeSpan _internalPeriodMax =
		TimeSpan.FromMilliseconds(int.MaxValue * 0.5);
	private Timer _internalTimer;
	private bool _isDisposed = false;


	// --------
	// Constructors:
	// --------

	// The private constructor doesn't create an initialized instance.
	// Use the public factory methods to create instances instead.
	private LongTimer(TimeSpan interval, DateTimeOffset end, bool autoReset) {
		Interval = interval;
		End = end;
		AutoReset = autoReset;
		_internalTimer = new (); // Needs to be overwritten in `Initialize()`.
	}

	// When constructing via a point in time, it doesn't make sense for
	// the timer to auto-reset (there's no directly-defined period).
	public static LongTimer Create(DateTimeOffset end) =>
		Create(end - DateTimeOffset.UtcNow, end, false);
	public static LongTimer Create(TimeSpan interval, bool autoReset=false) =>
		Create(interval, DateTimeOffset.UtcNow + interval, autoReset);
	public static LongTimer Create(TimeSpan interval, DateTimeOffset end, bool autoReset=false) {
		LongTimer instance = new (interval, end, autoReset);
		instance.InitializeTimer();
		return instance;
	}


	// --------
	// Methods for manipulating the timer:
	// --------

	public void Enable() => IsEnabled = true;
	public void Disable() => IsEnabled = false;

	public void SetAndEnable(DateTimeOffset end) {
		Initialize(end - DateTimeOffset.UtcNow, end, false);
	}
	public void SetAndEnable(TimeSpan interval, bool autoReset=false) {
		Initialize(interval, DateTimeOffset.UtcNow + interval, autoReset);
	}
	public void SetAndEnable(TimeSpan interval, DateTimeOffset end, bool autoReset=false) {
		Initialize(interval, end, autoReset);
	}


	// --------
	// `IDisposable` implementation:
	// --------

	// See the official documentation for guidelines on how to implement
	// `IDisposable`.

	// The standard implementation of `IDisposable.Dispose()`.
	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
	// An overload that is called based on where the disposal originates.
	// If `disposing` is true, the call comes from `Dispose()`.
	// If `disposing` is false, the call comes from a finalizer.
	protected virtual void Dispose(bool disposing) {
		if (_isDisposed)
			return;

		// Dispose managed members if called from `Dispose()`.
		if (disposing)
			_internalTimer.Dispose();

		_isDisposed = true;
	}


	// --------
	// Private helper methods:
	// --------

	private TimeSpan NextInternalPeriod =>
		(Remaining > _internalPeriodMax)
			? _internalPeriodMax
			: Remaining;

	private void Initialize(TimeSpan interval, DateTimeOffset end, bool autoReset) {
		Interval = interval;
		End = end;
		IsEnabled = true;
		AutoReset = autoReset;
		InitializeTimer();
	}
	private void InitializeTimer() {
		_internalTimer = Util.CreateTimer(NextInternalPeriod, false);
		_internalTimer.Elapsed += SetNextTimer;
		_internalTimer.Start();
	}
	private void SetNextTimer(object? timer, ElapsedEventArgs e) {
		_internalTimer.Stop();

		// Check that we didn't overshoot the end point in between when
		// the internal timer triggered, and this delegate is processed.
		// No absolute value needed--negative values should have triggered.
		if (Remaining < InternalAccuracy) {
			// Fire registered event.
			OnElapsed(e);

			// Terminate if not resetting.
			if (!AutoReset)
				return;

			// Else, set up for the next iteration.
			End = DateTimeOffset.UtcNow + Interval;
		}

		_internalTimer.Interval = NextInternalPeriod.TotalMilliseconds;
		_internalTimer.Start();
	}
}
