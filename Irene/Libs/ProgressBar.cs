namespace Irene;

static class ProgressBar {
	private static readonly DiscordEmoji
		_eEmptyL, _eEmptyM, _eEmptyR,
		_eFilledL, _eFilledM, _eFilledR,
		_eFillingL, _eFillingLtR, _eFillingRtL, _eFillingR;

	static ProgressBar() {
		CheckErythroInit();

		_eEmptyL = Erythro.Emoji(id_e.barEmptyL);
		_eEmptyM = Erythro.Emoji(id_e.barEmptyM);
		_eEmptyR = Erythro.Emoji(id_e.barEmptyR);

		_eFilledL = Erythro.Emoji(id_e.barFilledL);
		_eFilledM = Erythro.Emoji(id_e.barFilledM);
		_eFilledR = Erythro.Emoji(id_e.barFilledR);

		_eFillingL   = Erythro.Emoji(id_e.barFillingL  );
		_eFillingLtR = Erythro.Emoji(id_e.barFillingLtR);
		_eFillingRtL = Erythro.Emoji(id_e.barFillingRtL);
		_eFillingR   = Erythro.Emoji(id_e.barFillingR  );
	}
	
	// Construct a new progress bar using emojis. The visualization is
	// slightly inaccurate since the "center" of the "filling" emojis
	// isn't at the border of the emoji, but this is close enough (and
	// makes the math a lot simpler).
	// Note: Bars must be at least size 2.
	public static string Get(double percent, int size, bool isReversed=false) {
		int fill = (int)Math.Round(percent * size);
		fill = Math.Clamp(fill, 0, size);
		return Get(fill, size, isReversed);
	}
	public static string Get(int fill, int size, bool isReversed=false) {
		if (size < 2)
			size = 2;

		if (isReversed)
			return GetReverse(fill, size);

		StringBuilder bar = new ();
		DiscordEmoji emoji;
		for (int i=0; i<size; i++) {
			if (i == 0) {
				emoji = fill switch {
					0 => _eEmptyL,
					1 => _eFillingL,
					_ => _eFilledL,
				};
			} else if (i == size-1) {
				emoji = (fill == size)
					? _eFilledR
					: _eEmptyR;
			} else {
				if (i == fill-1)
					emoji = _eFillingLtR;
				else if (i < fill-1)
					emoji = _eFilledM;
				else
					emoji = _eEmptyM;
			}
			bar.Append(emoji.ToString());
		}
		return bar.ToString();
	}
	// This should only be called from `Get()`, so we know `size` has
	// to be at least 2 already.
	private static string GetReverse(int fill, int size) {
		StringBuilder bar = new ();
		DiscordEmoji emoji;
		for (int i=0; i<size; i++) {
			if (i == 0) {
				emoji = (fill == size)
					? _eFilledL
					: _eEmptyL;
			} else if (i == size-1) {
				emoji = fill switch {
					0 => _eEmptyR,
					1 => _eFillingR,
					_ => _eFilledR,
				};
			} else {
				if (i == size - fill)
					emoji = _eFillingRtL;
				else if (i > size - fill)
					emoji = _eFilledM;
				else
					emoji = _eEmptyM;
			}
			bar.Append(emoji.ToString());
		}
		return bar.ToString();
	}
}
