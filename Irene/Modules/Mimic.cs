namespace Irene.Modules;

using System.Text;
using System.Text.RegularExpressions;

class Mimic {
	private record class Wordlist(
		string Speakers,
		int MinLength,
		int MaxLength,
		bool IsMonospaced,
		IReadOnlyList<IReadOnlyList<string>> Words
	);

	// Master table of wordlists, indexed by (regularly-capitalized)
	// language names, and holding lists of strings indexed by the lengths
	// of the strings in the lists.
	private static readonly IReadOnlyDictionary<string, Wordlist> _wordlists;

	// Regex used for parsing through input text, and extracting words
	// one-by-one for translation.
	private static readonly Regex _regexWord = new (
		$@"(?<{_capturePreword}>[^A-Za-z0-9\-']*?)(?<{_captureWord}>[A-Za-z0-9\-']+)",
		RegexOptions.Compiled
	);
	private const string
		_capturePreword = "preword",
		_captureWord = "word";

	// Parser configuration settings.
	private const string
		_prefixComment = "#",
		_prefixMonospaced = "`";
	private const string _separator = " >>> ";
	private const string _indent = "\t";

	private const int _maxOptions = 20;
	private const string _pathWordlist = @"data/mimic-wordlist.txt";

	static Mimic() {
		_wordlists = ParseWordlists();
	}

	// Translate a string of text into the specified language.
	// Monospaced languages will be properly formatted.
	// Note: `language` must match the datafile definition exactly.
	// The parameter should be valid, since it comes from a slash
	// command autocomplete.
	public static string Translate(string language, string text) {
		if (!_wordlists.ContainsKey(language))
			throw new ImpossibleArgException(Commands.Mimic.ArgLanguage, language);
		Wordlist wordlist = _wordlists[language];

		int i = 0;
		StringBuilder translated = new ();
		while (i < text.Length) {
			Match match = _regexWord.Match(text, i);
			if (match.Success) {
				string preword = match.Groups.ContainsKey(_capturePreword)
					? match.Groups[_capturePreword].Value
					: "";
				string word = match.Groups.ContainsKey(_captureWord)
					? match.Groups[_captureWord].Value
					: "";
				translated.Append(preword);
				translated.Append(TranslateWord(wordlist, word));
				i += preword.Length + word.Length;
			} else {
				translated.Append(text[i..]);
				break;
			}
		}

		string output = translated.ToString();
		if (wordlist.IsMonospaced)
			output = output.Monospace();
		return output;
	}

	// Deterministically translate a single word using the given wordlist.
	// Note: `word` is not checked for being a valid word, and the string
	// is used as-is.
	private static string TranslateWord(Wordlist wordlist, string word) {
		int length = word.Length;
		string seed = word;

		// Trim / pad the seed word to fit within wordlist lengths.
		if (length <= wordlist.MinLength)
			seed = word + new string(' ', wordlist.MinLength - length);
		if (length >= wordlist.MaxLength)
			seed = word[..wordlist.MaxLength];
		length = seed.Length;

		// If needed, further trim seed word until there's a word in
		// the wordlist of matching length.
		if (wordlist.Words[length].Count == 0) {
			while (length > wordlist.MinLength) {
				length--;
				if (wordlist.Words[length].Count > 0)
					break;
			}
			seed = seed[..length];
		}

		// Use a hash instead of a PRNG to ensure determinism.
		int hash = seed.ToLower().GetHashCode();
		int i = Math.Abs(hash) % wordlist.Words[length].Count;
		string translated = wordlist.Words[length][i].ToLower();

		// Capitalize word properly.
		StringBuilder output = new (translated);
		for (int j=0; j < length; j++) {
			if (char.IsUpper(seed[j]))
				output[j] = char.ToUpper(translated[j]);
		}

		return output.ToString();
	}

	// Return a list of valid language options matching the input.
	public static List<(string, string)> AutocompleteLanguage(string input) {
		input = input.Trim().ToLower();

		List<(string, string)> options = new ();
		foreach (string language in _wordlists.Keys) {
			if (language.ToLower().Contains(input))
				options.Add((language, language));
		}

		if (options.Count > _maxOptions)
			options = options.GetRange(0, _maxOptions);

		options.Sort();
		return options;
	}

	// Helper method for parsing the datafile into a cached master table.
	// The file format must match exactly.
	private static ConcurrentDictionary<string, Wordlist> ParseWordlists() {
		ConcurrentDictionary<string, Wordlist> wordlists = new ();

		List<string> lines = new (File.ReadAllLines(_pathWordlist));

		for (int i=0; i < lines.Count; i++) {
			string line = lines[i];

			// Skip comment lines and empty lines.
			if (line.StartsWith(_prefixComment) || line.Trim() == "")
				continue;

			// Parse language data.
			string[] split = line.Trim().Split(_separator, 2);
			string language = split[0];
			string speakers = (split.Length > 1)
				? split[1]
				: "";

			// Detect monospaced languages.
			// Trim indicator if needed.
			bool isMonospaced = language.StartsWith(_prefixMonospaced);
			if (isMonospaced)
				language = language[_prefixMonospaced.Length..];

			// Parse the wordlist for a single language.
			int? minLength = null;
			int? maxLength = null;
			Dictionary<int, List<string>> wordlistSet = new ();
			while (i+1 < lines.Count && lines[i+1].StartsWith(_indent)) {
				i++;
				line = lines[i].Trim().ToLower();

				// Skip comment lines and empty lines.
				if (line.StartsWith(_prefixComment) || line.Trim() == "")
					continue;

				// Update min/max lengths.
				int length = line.Length;
				if (minLength is null || length < minLength)
					minLength = length;
				if (maxLength is null || length > maxLength)
					maxLength = length;
				
				// Ensure the set of lists contains at least an empty
				// list indexed for the current word's length.
				if (!wordlistSet.ContainsKey(length))
					wordlistSet.Add(length, new ());

				// Insert the word in the wordlist of the current word's
				// length.
				wordlistSet[length].Add(line);
			}

			// If min/max lengths aren't populated, this is an invalid
			// language definition.
			if (minLength is null || maxLength is null) {
				Log.Warning("Ill-formed language definition: {Language}", language);
				continue;
			}

			// Populate list of lists using the wordlist set.
			// The item at [0] is just empty and never accessed. In
			// order to fit this extra item, the total size of the list
			// needs to be incremented.
			List<List<string>> wordlist = new (maxLength.Value+1);
			for (int j=0; j < maxLength+1; j++) {
				List<string> words = (wordlistSet.ContainsKey(j))
					? wordlistSet[j]
					: new ();
				wordlist.Add(words);
			}

			wordlists.TryAdd(language, new (
				speakers,
				minLength.Value,
				maxLength.Value,
				isMonospaced,
				wordlist
			));
		}

		return wordlists;
	}
}
