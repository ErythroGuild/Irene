using System.Timers;

namespace Irene;

class LongTimer {
	public bool AutoReset { get; set; }
	public bool Enabled { get => _timer.Enabled; set => _timer.Enabled = value; }
	public decimal Interval { get; private init; }
	public decimal Remaining { get; private set; }

	public event ElapsedEventHandler? Elapsed;
	protected virtual void OnElapsed(ElapsedEventArgs e) {
		ElapsedEventHandler? handler = Elapsed;
		handler?.Invoke(this, e);
	}

	private readonly Timer _timer;
	private decimal _period;

	private const decimal _maxPeriod = int.MaxValue - 1;
	private const decimal _accuracy = 20; // msec

	public LongTimer(decimal totalMilliseconds, bool autoReset=false) {
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
			_timer.Interval = (double)_period;
			_timer.Start();
		}

		// Set up sub-timer.
		_period = NextPeriod();
		_timer = new Timer((double)_period)
			{ AutoReset = autoReset };
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

	private decimal NextPeriod() =>
		(Remaining > _maxPeriod)
			? Interval
			: _maxPeriod;
}
