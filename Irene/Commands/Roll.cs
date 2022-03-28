using System.Security.Cryptography;
using System.Threading;

namespace Irene.Commands;

using CRNG = RandomNumberGenerator;

class Roll : ICommands {
	// Use a single instance of a PRNG to improve the quality of
	// the results.
	static readonly ThreadLocal<CRNG> rng = new (CRNG.Create);

	// Timeout if this many attempts at generating a random number
	// were made.
	const int max_attempts = 20;

	public static string help() {
		StringWriter text = new ();

		text.WriteLine("Generates a single random integer.");
		text.WriteLine("`@Irene -roll` generates a number between `1` and `100`,");
		text.WriteLine("`@Irene -roll <max>` generates a number between `1` and `max`,");
		text.WriteLine("`@Irene -roll <min> <max>` generates a number between `min` and `max`.");
		text.WriteLine("All ranges are inclusive (e.g. `[1, 100]`).");

		return text.ToString();
	}

	public static void run(Command cmd) {
		ulong low, high;
		string arg = cmd.args.Trim();

		// If no arguments are specified, the interval is [1, 100].
		if (arg == "") {
			low = 1;
			high = 100;
		}
		
		// If only one argument is specified, the interval is [1, N].
		else if (!arg.Contains(' ') && !arg.Contains('-')) {
			low = 1;
			bool did_parse = ulong.TryParse(arg, out high);

			// Both arguments must parse successfully.
			if (!did_parse) {
				log.info("  Could not parse argument.");
				log.debug($"    {arg}");
				log.endl();
				_ = cmd.msg.RespondAsync("Invalid number format. (See `@Irene -help roll`.)");
				return;
			}
		}
		
		// If both arguments are specified, the interval is [N, M].
		else {
			string[] split = arg.Split(new char[] {' ', '-'}, 2);
			string
				str1 = split[0],
				str2 = split[1];
			bool did_parse_1 = ulong.TryParse(str1, out low);
			bool did_parse_2 = ulong.TryParse(str2, out high);

			// Both arguments must parse successfully.
			if (!did_parse_1 || !did_parse_2) {
				log.info("  Could not parse arguments.");
				log.debug($"    {str1}");
				log.debug($"    {str2}");
				log.endl();
				_ = cmd.msg.RespondAsync("Invalid number format. (See `@Irene -help roll`.)");
				return;
			}
		}

		// Make sure the higher number is actually higher.
		// (System.Random also expects this.)
		if (low > high) {
			ulong temp = high;
			high = low;
			low = temp;
		}

		// Get a random number.
		log.info($"  Generating random number from {low} to {high}.");
		try {
			ulong x = random(low, high);
			log.info($"    Result: {x}");
			log.endl();
			_ = cmd.msg.RespondAsync($"`{x}`");
		} catch (TimeoutException) {
			ulong range = high - low;
			ulong max = long.MaxValue;
			decimal p = max % range / (decimal)max;
			p *= 100;

			StringWriter text = new ();
			text.WriteLine("Could not generate an unbiased random number.");
			text.WriteLine($"Attempts: `{max_attempts}`");
			text.WriteLine($"Probability: `{p:g}%`");
			_ = cmd.msg.RespondAsync(text.ToString());
		}
	}

	// Returns a thread-safe, cryptographically-secure
	// pseudorandom number, between low and high, inclusive.
	static ulong random(ulong low, ulong high) {
		// Set up needed variables.
		ulong range = high - low + 1;
		ulong max = ulong.MaxValue;
		ulong cutoff = max - max % range;
		ulong output;

		// Attempt to generate a number within the cutoff range.
		for (int i=0; i<max_attempts; i++) {
			ulong raw = get_ulong();
			if (raw < cutoff) {
				log.debug($"    Generated in {i+1} attempt(s).");
				output = low + raw % range;
				return output;
			}
		}

		// Throw error if could not generate a such a random number.
		log.error($"  Could not generate unbiased number after {max_attempts} attempts.");
		log.endl();
		throw new TimeoutException("Random number generation timed out.");
	}

	// Stitch a ulong from raw bytes.
	static ulong get_ulong() {
		byte[] raw = new byte[sizeof(ulong)];
		rng.Value!.GetBytes(raw);	// static init ensures non-null
		ulong x = BitConverter.ToUInt64(raw);
		return x;
	}
}
