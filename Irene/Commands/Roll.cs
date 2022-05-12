using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using CRNG = System.Security.Cryptography.RandomNumberGenerator;

namespace Irene.Commands;

class Roll : AbstractCommand {
	private static readonly CRNG _crng = CRNG.Create();
	private static readonly object _lock = new ();

	// Timeout if this many attempts at generating a random number
	// were made.
	private const int _maxTries = 8;
	// Limit range to values smaller than int (which will be smaller
	// than the ulong that the raw CRNG will generate).
	// Making the minimum value positive allows use of unsigned math
	// for the number generation intermediary steps.
	// int: -2,147,483,648 ~ 2,147,483,647
	private const int _minRange = 1,
		_maxRange = 1000000000; // 10^9

	private static readonly ReadOnlyCollection<string> _predictions =
		new (new List<string>() {
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
		});

	public override List<string> HelpPages =>
		new () { string.Join("\n", new List<string> {
			@"`/roll` generates a number between `1` and `100`,",
			@"`/roll <max>` generates a number between `1` and `max`,",
			@"`/roll <min> <max> generates a number between `min` and `max`.",
			"All ranges are inclusive (e.g. `[1, 100]`).",
			@"`/8-ball <question> [keep-private]` forecasts the answer to a yes/no question."
		} ) };

	public override List<InteractionCommand> SlashCommands =>
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
			), Command.DeferVisibleAsync, RunRollAsync ),
			new ( new (
				"8-ball",
				@"Forecast the answer to a yes/no question.",
				new List<CommandOption>() {
					new (
						"question",
						@"A yes/no question to answer.",
						ApplicationCommandOptionType.String,
						required: true
					),
					new (
						"keep-private",
						"Keep response visible only to self.",
						ApplicationCommandOptionType.Boolean,
						required: false
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Defer8BallAsync, Run8BallAsync )
		};

	public static async Task RunRollAsync(TimedInteraction interaction) {
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
		await Command.SubmitResponseAsync(
			interaction,
			$"  {output}",
			"Sending random number.",
			LogLevel.Debug,
			"Random number sent: {X}.".AsLazy(),
			x
		);

		// If fallback generation was used, calculate the probability
		// that the fallback was needed.
		if (didFail) {
			ulong range = (ulong)(high - low + 1);
			ulong max = ulong.MaxValue;
			decimal p = max % range / (decimal) max;
			p = (decimal)Math.Pow((double)p, _maxTries);

			Log.Information("    Failed to generate secure number {Attempts} times.", _maxTries);
			Log.Debug("    Probability of this occuring: {Probability:p}%.", p);
		}
	}

	public static async Task Defer8BallAsync(TimedInteraction interaction) {
		// Check for "private" response option.
		bool doHide = false;
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		if (args.Count > 1)
			doHide = (bool)args[1].Value;
		await Command.DeferAsync(interaction, doHide);
	}
	public static async Task Run8BallAsync(TimedInteraction interaction) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();

		// Strip input argument (lower case, remove punctuation).
		// Newlines are converted to spaces.
		// (Format question for output.)
		string arg_original = (string)args[0].Value;
		arg_original = arg_original.Replace('\n', ' ');
		string arg_stripped = arg_original.ToLower();
		arg_stripped = Regex.Replace(arg_stripped, @"[^a-zA-Z0-9]", "");
		arg_stripped += DateTime.Now.ToString(Format_IsoDate);
		
		// (MD5) Hash input.
		byte[] arg_raw = Encoding.ASCII.GetBytes(arg_stripped);
		byte[] hash_raw = MD5.Create().ComputeHash(arg_raw);

		// Calculate result from hash.
		// This is technically NOT a uniform sample, but it is more
		// than close enough and not worth implementing a fallback
		// (which would also need to be deterministic).
		ulong hash = BitConverter.ToUInt64(hash_raw);
		int index = (int)(hash % (ulong)_predictions.Count);

		// Respond.
		string prediction = $"> {arg_original}\n{_predictions[index]}";
		await Command.SubmitResponseAsync(
			interaction,
			prediction,
			"Sending prediction.",
			LogLevel.Debug,
			"Prediction sent: \"{Prediction}\".".AsLazy(),
			_predictions[index]
		);
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
		lock (_lock) { _crng.GetBytes(raw); }

		return BitConverter.ToUInt64(raw);
	}
}
