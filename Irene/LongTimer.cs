using System.Timers;

namespace Irene;

class LongTimer {
	public bool AutoReset { get; set; }
	public bool Enabled {
		get => _timer.Enabled;
		set => _timer.Enabled = value;
	}
	public decimal Interval {
		get => _interval;
		set {
			_interval = value;
			// Expected behavior for Timer.Interval:
			// Set the interval, then restart the timer if the timer
			// is already running.
			Remaining = value;
			_period = NextPeriod();
			_timer.Interval = _period;
			if (_timer is not null && _timer.Enabled) {
				Stop();
				Restart();
			}
		}
	}
	public decimal Remaining { get; private set; }

	public event ElapsedEventHandler? Elapsed;
	protected virtual void OnElapsed(ElapsedEventArgs e) {
		ElapsedEventHandler? handler = Elapsed;
		handler?.Invoke(this, e);
	}

	private readonly Timer _timer;
	private decimal _interval;
	private double _period;

	private const double _maxPeriod = int.MaxValue - 1;
	private const decimal _accuracy = 20; // msec

	public LongTimer(double totalMilliseconds, bool autoReset=false)
		: this ((decimal)totalMilliseconds, autoReset) { }
	public LongTimer(decimal totalMilliseconds, bool autoReset=false) {
		Interval = totalMilliseconds;
		Remaining = Interval;
		Elapsed = null;

		// Event handler for sub-timer elapsed.
		void SetNextTimer(object? t, ElapsedEventArgs e) {
			_timer.Stop();
			Remaining -= (decimal)_period;

			// No absolute value on check--any negative values
			// count as having triggered the timer.
			if (Remaining < _accuracy) {
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
		_timer = Util.CreateTimer(_period, autoReset);
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
		(Remaining > (decimal)_maxPeriod)
			? _maxPeriod
			: (double)Remaining;
}
