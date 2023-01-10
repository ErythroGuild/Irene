namespace Irene.Modules;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using CRNG = System.Security.Cryptography.RandomNumberGenerator;

class Random {
	private static readonly CRNG _crng = CRNG.Create();
	private static readonly object _lock = new ();

	// --------
	// Constants:
	// --------

	// Timeout if this many attempts at generating an unbiased random
	// number were made (via CRNG).
	public const int MaxTries = 8;
	// The bounds (inclusive) of the values the raw CRNG generator can
	// return. The max value is right-shifted by one bit to fit the result
	// inside a signed variable, without biasing the output.
	public const long
		RawMin = 0,
		RawMax = (long)(ulong.MaxValue >> 1);

	// Possible Magic 8-Ball responses, according to Wikipedia.
	private static readonly IReadOnlyList<string> _predictions =
		new List<string> {
			// Positive
			"It is certain.",
			"It is decidedly so.",
			"Without a doubt.",
			"Yes definitely.",
			"You may rely on it.",
			"As I see it, yes.",
			"Most likely.",
			"Outlook good.",
			"Yes.",
			"Signs point to yes.",

			// Negative
			"Don't count on it.",
			"My reply is no.",
			"My sources say no.",
			"Outlook not so good.",
			"Very doubtful.",

			// Non-committal
			"Reply hazy, try again.",
			"Ask again later.",
			"Better not tell you now.",
			"Cannot predict now.",
			"Concentrate and ask again.",
		};

	// Possible "Book of Answers" answers, extracted from the book.
	private static readonly IReadOnlyList<string> _answers;

	private const string _pathAnswers = @"data/random-answers.txt";

	static Random() {
		// Initialize answer list from extracted data.
		// There are some repeats, but these are kept intentionally.
		string[] lines = File.ReadAllLines(_pathAnswers);
		_answers = new List<string>(lines);
		Log.Debug("  Modules.Random initialized successfully.");
	}


	// --------
	// Number generation methods:
	// --------
	// None of these methods attempt to verify that the lower bound is
	// in fact less than the upper bound. It is the caller's responsibility
	// to ensure this doesn't happen.

	// Attempts to return a cryptographically-secure pseudorandom number,
	// between low and high, inclusive; but will automatically fall back
	// to a cryptographically-INsecure number if that fails.
	// Note: Lower bound MUST be non-negative.
	public static long RandomWithFallback(long low, long high) {
		long? output = RandomSecure(low, high);
		if (output is not null)
			return output.Value;

		Log.Warning("CRNG failed, falling back. ({AttemptCount} attempts)", MaxTries);
		return RandomFallback(low, high);
	}

	// Attempts to return a cryptographically-secure pseudorandom number,
	// between low and high, inclusive.
	// Returns null if timed out.
	// Note: Lower bound MUST be non-negative.
	public static long? RandomSecure(long low, long high) {
		// Order of operations is always important, to stay within the
		// valid range of the underlying data type.

		// Set up needed variables.
		long range = high - low + 1; // add 1 for inclusive bounds
		long max = long.MaxValue;
		long cutoff = max - (max % range);
		long output;

		// Attempt to generate a number within the cutoff range.
		for (int i = 0; i<MaxTries; i++) {
			long raw = GenerateRawValue();
			if (raw < cutoff) {
				output = low + (raw % range);
				return output;
			}
		}
		// Return null if we hit max attempts.
		return null;
	}
	// Returns a cryptographically-INsecure pseudorandom number, between
	// low and high, inclusive.
	public static long RandomFallback(long low, long high) =>
		System.Random.Shared.NextInt64(low, high + 1);

	// Stitch a `ulong` from raw bytes, and shift it to fit in the range
	// of a signed `long` (0 to `long.MaxValue`).
	private static long GenerateRawValue() {
		byte[] raw = new byte[sizeof(ulong)];

		// Make sure generation is thread-safe.
		lock (_lock) { _crng.GetBytes(raw); }

		ulong untrimmed = BitConverter.ToUInt64(raw);

		// Right-shifting by one trims off the LSB, and fits the result
		// inside the positive half of a `long`'s range.
		long trimmed = (long)(untrimmed >> 1);

		return trimmed;
	}
	// Calculate the chance that `Random()` will fail.
	public static decimal GenerationFailureChance(long low, long high, int attempts) {
		// Order of operations is always important, to stay within the
		// valid range of the underlying data type.
		long range = high - low + 1; // add 1 for inclusive bounds
		long max = long.MaxValue;
		decimal p = max % range / (decimal)max;
		p = (decimal)Math.Pow((double)p, attempts);
		return p;
	}


	// --------
	// RNG-based recreational methods:
	// --------

	// This method returns a string simulating the behavior of "/roll"
	// in WoW (with slightly prettified output).
	public static string SlashRoll(IReadOnlyList<int> args) {
		// Assign range based on args.
		(int low, int high) = args.Count switch {
			0 => (1, 100),
			1 => (1, args[0]),
			2 => (args[0], args[1]),
			_ => throw new ImpossibleArgException("/roll range", ">2 args"),
		};

		// Make sure the higher number is actually higher.
		// (System.Random also expects this.)
		if (low > high)
			(low, high) = (high, low);

		// Generate random number.
		long x = RandomWithFallback(low, high);

		// Format result as a (pretty) string (with die emoji!).
		string output = "\U0001F3B2  " + x.ToString("n0").Bold();
		if ((low, high) != (1, 100))
			output += $"    ({low}-{high})";
		return output;
	}

	// Since the raw value has an equal chance of being even/odd, directly
	// using the generated result is unbiased (and more efficient).
	public static bool FlipCoin() =>
		(GenerateRawValue() % 2) == 0;

	public enum Suit { Spades, Hearts, Diamonds, Clubs, Joker }
	public record struct PlayingCard(Suit Suit, string? Value);
	public static PlayingCard DrawCard(bool includeJokers=true) {
		int max = includeJokers ? 53 : 51;
		int i = (int)RandomWithFallback(0, max);

		if (i is 52 or 53)
			return new PlayingCard(Suit.Joker, null);

		int suit_i = i / 13; // int division!
		int value_i = i - (suit_i * 13);

		// New deck order :)
		Suit suit = suit_i switch {
			0 => Suit.Spades,
			1 => Suit.Diamonds,
			2 => Suit.Clubs,
			3 => Suit.Hearts,
			_ => throw new ImpossibleException(),
		};

		string value = value_i switch {
			  0 => "A",
			<=9 => (value_i + 1).ToString(),
			 10 => "J",
			 11 => "Q",
			 12 => "K",
			_ => throw new ImpossibleException(),
		};

		return new (suit, value);
	}

	// The input query is normalized and hashed, and then used to select
	// a prediction. The hash is tied to the date of the query.
	public static string Magic8Ball(string query, DateOnly date) {
		// Generate prediction list index.
		int hash = HashQuery(query, date);
		int cutoff = int.MaxValue - (int.MaxValue % _predictions.Count);
		int i = (hash < cutoff)
			? hash % _predictions.Count
			: (int)RandomFallback(0, _predictions.Count - 1);

		// Format the selected response.
		const string emDash = "\u2014";
		return
			$"""
			> {query}
			    {emDash} *{_predictions[i]}*
			""";
	}

	// Very similar to 8-ball command, only difference is
	public static string PickAnswer(string query, DateOnly date) {
		// Generate answer list index.
		int hash = HashQuery(query, date);
		int cutoff = int.MaxValue - (int.MaxValue % _answers.Count);
		int i = (hash < cutoff)
			? hash % _answers.Count
			: (int)RandomFallback(0, _answers.Count - 1);

		// Format the selected response.
		const string emDash = "\u2014";
		return
			$"""
			> {query}
			    {emDash} *{_answers[i]}*
			""";
	}

	// Normalize the query (based on the date). The end result isn't
	// guaranteed to be cryptographically-secure, but it *does* allow
	// the same query to return consistent results, on the same day
	// (but *can* vary based on the wording, which is desirable).
	private static int HashQuery(string query, DateOnly date) {
		// Condense input query (lower case + remove punctuation).
		string queryStripped = query.Replace('\n', ' ');
		queryStripped = queryStripped.ToLower();
		queryStripped = Regex.Replace(queryStripped, @"[^a-zA-Z0-9]", "");
		queryStripped += date.ToString(Format_IsoDate);

		// Hash input with MD5.
		byte[] queryRaw = Encoding.ASCII.GetBytes(queryStripped);
		byte[] hashRaw = MD5.HashData(queryRaw);

		// Convert hash to a list index, falling back to `System.Random`
		// (cryptographically-INsecure) if the result would be biased.
		int hash = BitConverter.ToInt32(hashRaw);
		hash = Math.Abs(hash);

		return hash;
	}
}
