using System.Timers;

namespace Irene;

class LongTimer {
	public bool AutoReset { get; set; }
	public bool Enabled { get => _timer.Enabled; set => _timer.Enabled = value; }
	public double Interval { get; private init; }
	public double Remaining { get; private set; }

	public event ElapsedEventHandler? Elapsed;
	protected virtual void OnElapsed(ElapsedEventArgs e) {
		ElapsedEventHandler? handler = Elapsed;
		handler?.Invoke(this, e);
	}

	private readonly Timer _timer;
	private double _period;

	private const double _maxPeriod = int.MaxValue - 1;
	private const double _accuracy = 20; // msec

	public LongTimer(double totalMilliseconds, bool autoReset=false) {
		Interval = totalMilliseconds;
		Remaining = Interval;
		Elapsed = null;

		// Event handler for sub-timer elapsed.
		void SetNextTimer(object? t, ElapsedEventArgs e) {
			_timer.Stop();
			Remaining -= _period;

			if (Math.Abs(_period) < _accuracy) {
				// Fire elapsed event.
				OnElapsed(e);

				// Continue or terminate based on AutoReset.
				if (AutoReset)
					Remaining = Interval;
				else
					return;
			}

			// If continuing, set next timer iteration and resume.
			_period = NextPeriod();
			_timer.Interval = _period;
			_timer.Start();
		}

		// Set up sub-timer.
		_period = NextPeriod();
		_timer = new Timer(_period) { AutoReset = autoReset };
		_timer.Elapsed += SetNextTimer;
	}

	// Resets the remaining time to the full interval, and then
	// starts the timer over again.
	public void Restart() {
		_timer.Stop();
		Remaining = Interval;
		_period = NextPeriod();
		_timer.Interval = _period; // this resets timer count
		_timer.Start();
	}
	public void Start() => _timer.Start();
	public void Stop() => _timer.Stop();
	public void Close() => _timer.Close();

	private double NextPeriod() =>
		(Remaining > _maxPeriod)
			? Interval
			: _maxPeriod;
}
