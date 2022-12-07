namespace Irene.Utils;

using System.Text.RegularExpressions;

static partial class Util {
	// Converts strings to/from single-line, easily parseable text.
	// Escape codes are defined in `Const`.
	public static string Escape(this string input) {
		// Especially because the codepoints to be escaped are single
		// characters, it is approximately the same number of comparisons
		// to loop through each codepoint (and check if the input contains
		// it), vs. to loop through each character (and check if it can
		// be escaped).
		// Using a `StringBuilder` avoids unnecessary (immutable) string
		// allocations (`compared to string.Replace()`).
		StringBuilder output = new (input);
		foreach (string code in EscapeCodes.KeysForward()) {
			string codepoint = EscapeCodes.GetForward(code);
			output = output.Replace(codepoint, code);
		}
		return output.ToString();
	}
	public static string Unescape(this string input) {
		// Escape codes either start with '\' or are enclosed in ':'.
		// This allows for parsing the escaped string with a regex and
		// enormously speeds up the process.
		Regex regexCode = new (@"(?::[^:\s]+:)|(?:\\\S)");

		StringBuilder output = new ();
		int i = 0;
		while (i < input.Length) {
			Match match = regexCode.Match(input, i);
			
			if (match.Success) {
				string code = match.Value;
				int i_match = match.Index;
				// Read in any text since the last match.
				output.Append(input[i..i_match]);

				// Check to see that the match is actually for an escape
				// code. If not, we need to check again starting after
				// the first character of the false match, since there
				// could be an overlapping real match.
				if (EscapeCodes.ContainsFirst(code)) {
					// Replace the code with the actual codepoint.
					output.Append(EscapeCodes.GetForward(code));
					i = i_match + code.Length;
				} else {
					// Read in a single character.
					output.Append(input[i_match]);
					i = i_match + 1;
				}
				continue;
			}

			// Else there was no match, and we simply append the remainder
			// of the input string.
			output.Append(input[i..]);
			break;
		}

		return output.ToString();
	}

	// Converts any textual representations of the chosen Discord objects
	// into their underlying token representations.
	// Example (rendering only emojis):
	// `text.RenderDiscordObjects(Erythro, renderEmojis: true);`
	public static string RenderDiscordObjects(
		this string input,
		GuildData erythro,
		bool renderChannels=false,
		bool renderRoles=false,
		bool renderEmojis=false
	) {
		string output = input;
		// Output is processed for replacements in separate passes.
		// Since there is a fixed number of passes (and some may not
		// even be selected), the time complexity is still linear.

		if (renderChannels) {
			Dictionary<string, DiscordChannel> channels = new ();
			foreach (DiscordChannel channel in erythro.Guild.Channels.Values) {
				if (channel.Type == ChannelType.Text)
					channels.Add(channel.Name, channel);
			}

			MatchEvaluator FindChannel = new (match => {
				string name = match.Groups[1].Value;
				return channels.ContainsKey(name)
					? channels[name].Mention
					: match.Value;
			});

			output = Regex.Replace(output, @"#([^#\s]+)", FindChannel);
		}
		
		if (renderRoles) {
			Dictionary<string, DiscordRole> roles = new ();
			foreach (DiscordRole role in erythro.Guild.Roles.Values) 
				roles.Add(role.Name, role);

			// This will not mention any roles with spaces in the name,
			// since the regex won't even match it as a candidate.
			MatchEvaluator FindRole = new (match => {
				string name = match.Groups[1].Value;
				return roles.ContainsKey(name)
					? roles[name].Mention
					: match.Value;
			});

			output = Regex.Replace(output, @"@([^@\s]+)", FindRole);
		}

		if (renderEmojis) {
			MatchEvaluator FindEmoji = new ((match) => {
				// Regex does not include ending ":" (allows the regex
				// to use that ":" in the following match), so we need
				// to append it back on.
				bool isEmoji = DiscordEmoji.TryFromName(
					erythro.Client,
					match.Value + ":",
					out DiscordEmoji emoji
				);

				return isEmoji
					? emoji.ToString()
					: match.Value;
			});

			Regex.Replace(output, @":([^:\s]+)", FindEmoji);
		}

		return output;
	}

	// Syntax sugar for passing a string as a Lazy<string>.
	public static Lazy<string> AsLazy(this string s) => new (s);

	// Returns the filename with "-temp" inserted at the end of the name,
	// before the file extension.
	// Throws if the filename doesn't end with a file extension.
	public static string Temp(this string filename) {
		Regex regexFilename = new (@".\.\w+$");
		if (!regexFilename.IsMatch(filename))
			throw new ArgumentException("Original filename must have a file extension.", nameof(filename));

		int i = filename.LastIndexOf('.');
		return filename.Insert(i, "-temp");
	}

	// Returns all of the string up to the first newline if one exists,
	// and returns the entire string otherwise.
	public static string ElideFirstLine(this string input) {
		if (input.Contains('\n')) {
			int i_newline = input.IndexOf("\n");
			return input[..i_newline] + " [...]";
		} else {
			return input;
		}
	}

	// Print a List<string> as concatenated lines.
	public static string ToLines(this List<string> lines) =>
		string.Join("\n", lines);

	// Create a blank file at the given path, if it doesn't exist.
	// Returns true if file was created, false otherwise.
	public static bool CreateIfMissing(string path) {
		bool didCreate = false;
		if (!File.Exists(path)) {
			File.Create(path).Close();
			didCreate = true;
		}
		return didCreate;
	}
}
