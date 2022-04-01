using System.Security.Cryptography;

namespace Irene.Commands;

using CRNG = RandomNumberGenerator;

class Roll : ICommand {
	private static readonly CRNG _crng = CRNG.Create();
	private static object _lock = new ();

	// Timeout if this many attempts at generating a random number
	// were made.
	private const int _maxTries = 8;
	// Limit range to values smaller than int (which will be smaller
	// than the ulong that the raw CRNG will generate).
	// Making the minimum value positive allows use of unsigned math
	// for the number generation intermediary steps.
	// int: -2,147,483,648 ~ 2,147,483,647
	private const int _minRange = 1,
		_maxRange = 1000000000;	// 10^9

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@"`/roll` generates a number between `1` and `100`,",
			@"`/roll <max>` generates a number between `1` and `max`,",
			@"`/roll <min> <max> generates a number between `min` and `max`.",
			"All ranges are inclusive (e.g. `[1, 100]`)."
		} ) };
	}

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"roll",
				"Generate a number in a given range (inclusive).",
				new List<CommandOption> {
					new (
						"min",
						"Smallest number to generate.",
						ApplicationCommandOptionType.Integer,
						required: false,
						minValue: _minRange,
						maxValue: _maxRange
					),
					new (
						"max",
						"Biggest number to generate.",
						ApplicationCommandOptionType.Integer,
						required: false,
						minValue: _minRange,
						maxValue: _maxRange
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }

	public static async Task RunAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		int low, high;

		// Convert an argument from object -> long -> int.
		// (If a direct cast is tried, an InvalidCastException will be
		// thrown.)
		static int ArgToInt(DiscordInteractionDataOption arg) =>
			(int)(long)arg.Value;

		// Assign range based on args.
		(low, high) = args.Count switch {
			0 => (1, 100),
			1 => (1, ArgToInt(args[0])),
			2 => (ArgToInt(args[0]), ArgToInt(args[1])),
			_ => throw new ArgumentException("Too many arguments provided."),
		};

		// Make sure the higher number is actually higher.
		// (System.Random also expects this.)
		if (low > high)
			(low, high) = (high, low);

		Log.Debug("  Generating number in the range [{Min}, {Max}].", low, high);

		// Generate a random number, falling back to non-cryptographic
		// method if cryptographic method fails.
		int? x = Random(low, high);
		bool didFail = false;
		if (x is null) {
			didFail = true;
			Log.Warning("  Could not generate a secure number. Falling back to alternate method.");
			x = RandomFallback(low, high);
		}

		// Stringify result.
		// "n" - number:
		//     Integral and decimal digits, group separators, and a
		//     decimal separator with optional negative sign.
		string output = ((int)x).ToString("n0");
		output = output.Bold();
		if ((low, high) != (1, 100))
			output += $"    ({low}-{high})";

		// Return data.
		Log.Debug("  Sending random number: {X}", x);
		stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
		await interaction.RespondMessageAsync($"  {output}");
		Log.Information("  Random number sent.");

		// If fallback generation was used, calculate the probability
		// that the fallback was needed.
		if (didFail) {
			ulong range = (ulong)(high - low + 1);
			ulong max = ulong.MaxValue;
			decimal p = max % range / (decimal) max;
			p = (decimal)Math.Pow((double)p, _maxTries);

			Log.Debug("    Failed to generate secure number {Attempts} times.", _maxTries);
			Log.Debug("    Probability of this occuring: {Probability:p}%.", p);
		}
	}

	// Returns a thread-safe, cryptographically-secure
	// pseudorandom number, between low and high, inclusive.
	// Returns null if timed out.
	private static int? Random(int low, int high) {
		// Set up needed variables.
		ulong range = (ulong)(high - low + 1);
		ulong max = ulong.MaxValue;
		ulong cutoff = max - max % range;
		ulong output;

		// Attempt to generate a number within the cutoff range.
		for (int i=0; i<_maxTries; i++) {
			ulong raw = GetRawBytes();
			if (raw < cutoff) {
				Log.Debug("    Generated in {Attempts} attempt(s).", i+1);
				output = (ulong)low + raw % range;
				return (int)output;
			}
		}
		// Return null if we hit max attempts.
		return null;
	}

	// Returns a thread-safe, but not cryptographically-secure
	// pseudorandom number, between low and high, inclusive.
	private static int RandomFallback(int low, int high) =>
		System.Random.Shared.Next(low, high + 1);

	// Stitch a ulong from raw bytes.
	private static ulong GetRawBytes() {
		byte[] raw = new byte[sizeof(ulong)];

		// Make sure generation is thread-safe.
		lock (_lock) {
			_crng.GetBytes(raw);
		}

		return BitConverter.ToUInt64(raw);
	}
}
