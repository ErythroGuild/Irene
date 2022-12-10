namespace Irene.Autocompleters;

class StringCompleter : Completer {
	// --------
	// Definitions and data structures:
	// --------

	public delegate IList<string> ListHandler(ParsedArgs args);

	private enum MatchType {
		Start,
		WordStart,
		Contains,
		Fuzzy,
	}

	// Incursions only count alpha characters skipped when fuzzy-matching.
	// Non-alpha characters skipped by "Contains" are not incursions.
	private readonly record struct MatchData(
		bool IsMatch,
		int Incursions,
		int Index
	);
	// Used as a convenient way to track each list of results, without
	// having to check the length of the list every time.
	private record class MatchList {
		public bool IsFull { get; set; }
		public List<string> List { get; }

		public MatchList(int listSize) {
			IsFull = false;
			List = new (listSize);
		}
	}


	// --------
	// Fields, constants, and constructor:
	// --------

	private ListHandler _getListAll;
	private ListHandler _getListDefault;

	private static readonly StringComparison _stringCompare =
		StringComparison.InvariantCultureIgnoreCase;
	private const int _i_start = (int)MatchType.Start;

	// The base constructor is only temporarily set to dummy lambdas.
	// The actual constructor body sets the properties to the correct
	// lambdas (which require initialization of the instance).
	public StringCompleter(
		ListHandler getListAll,
		ListHandler listDefault,
		int maxOptions=MaxOptionsDefault
	) : base (null!, null, maxOptions) {
		_getListAll = getListAll;
		_getListDefault = listDefault;

		GetOptionsAll = MatchOptionsAsync;
		GetOptionsDefault = GetDefaultList;
	}


	// --------
	// Top-level delegates for inherited handlers:
	// --------

	private Task<IList<(string, string)>> MatchOptionsAsync(string arg, ParsedArgs args) =>
		Task.Run<IList<(string, string)>>(() => MatchOptions(arg, args));
	private IList<(string, string)> MatchOptions(string arg, ParsedArgs args) {
		arg = arg.Trim().ToLower();
		IList<string> options = _getListAll.Invoke(args);
		List<MatchList> matches = new () {
			new (MaxOptions),
			new (MaxOptions),
			new (MaxOptions),
			new (MaxOptions),
		};

		// Search through all options.
		MatchType typeSearching = MatchType.Fuzzy;
		foreach (string option_i in options) {
			string option = option_i.ToLower();

			// Search for a match.
			MatchType? match =
				FindStrictestMatch(arg, option, typeSearching);
			if (match is null)
				continue;

			// Add the (non-normalized!) result to the lists.
			bool isDone = false;
			for (int i = (int)match.Value; i<=(int)MatchType.Fuzzy; i++) {
				// A match will also count as a less-strict match.
				if (matches[i].IsFull)
					continue;

				// Add the *non-normalized* result.
				matches[i].List.Add(option_i);
				// Since this is the only list we care about filling,
				// once it's full we can stop searching.
				if (i == _i_start) {
					if (matches[i].List.Count == MaxOptions) {
						isDone = true;
						break;
					}
				}

				// Update list fullness and search type.
				if (matches[i].List.Count == MaxOptions) {
					matches[i].IsFull = true;
					for (int j = (int)MatchType.Fuzzy; j>=_i_start; j--) {
						if (!matches[j].IsFull) {
							typeSearching = (MatchType)j;
							break;
						}
					}
				}

				break;
			}

			if (isDone)
				break;
		}

		// Sort and collate results.
		// The strictest results come first, then less strict results.
		// (This is due to the ascending enum values, starting at 0.)
		// Results are sorted separately within each result type.
		List<string> results = new (2*MaxOptions);

		foreach (MatchList matchList in matches) {
			List<string> list = matchList.List;
			list.Sort((a, b) => string.Compare(a, b, _stringCompare));
			results.AddRange(list);
			if (results.Count > MaxOptions)
				break;
		}

		return ListToPairList(results);
	}

	private IList<(string, string)> GetDefaultList(ParsedArgs args) =>
		ListToPairList(_getListDefault.Invoke(args));
	

	// --------
	// Helper methods for searching through options:
	// --------

	// These methods all assume their arguments have been normalized
	// already (trimmed and lowercased).

	// For the given inputs, try to find the strictest possible match
	// type.
	// Returns null when no match (with the specified minimum strictness)
	// is possible.
	private static MatchType? FindStrictestMatch(
		string input,
		string option,
		MatchType typeLoosest
	) {
		MatchType? typeStrictest = null;
		for (int i=(int)typeLoosest; i>=_i_start; i--) {
			MatchData match = (MatchType)i switch {
				MatchType.Start     => IsMatchStart    (input, option),
				MatchType.WordStart => IsMatchWordStart(input, option),
				MatchType.Contains  => IsMatchContains (input, option),
				MatchType.Fuzzy     => IsMatchFuzzy    (input, option),
				_ => throw new UnclosedEnumException(typeof(MatchType), (MatchType)i),
			};

			// Stop searching and return the best match found.
			if (!match.IsMatch)
				break;

			// Bring the indexing type down as low as possible, but be
			// careful not to increase it.
			MatchType type = InferTypeFromData(option, match);
			if ((int)type < i)
				i = (int)type;
			
			// Update strictest result found so far.
			typeStrictest = (MatchType)i;
		}

		return typeStrictest;
	}

	// Given the output of a match check, attempt to narrow the result
	// type as much as possible (without additional searching).
	private static MatchType InferTypeFromData(string option, MatchData data) {
		if (data.Incursions > 0)
			return MatchType.Fuzzy;
		if (data.Index == 0)
			return MatchType.Start;
		return !IsAlpha(option[data.Index-1])
			? MatchType.WordStart
			: MatchType.Contains;
	}


	// --------
	// Methods to directly check a match type:
	// --------

	// These methods all assume their arguments have been normalized
	// already (trimmed and lowercased).

	// Check for a match of the desired type.
	private static MatchData IsMatchStart(string input, string option) =>
		new (option.StartsWith(input), 0, 0);
	private static MatchData IsMatchWordStart(string input, string option) {
		char? charOption = null;
		bool isBoundary;
		int i_option = 0;
		int i_input = 0;
		int i_match = 0;
		int i_candidate = 0;
		for (; i_option<option.Length; i_option++) {
			// Check boundary condition.
			isBoundary = !IsAlpha(charOption); // previous char
			charOption = option[i_option];

			// Character matched.
			if (charOption == input[i_input]) {
				// Register a new match if starting on boundary.
				if (i_input == 0) {
					if (isBoundary)
						i_match = i_option;
					else
						continue;
				}
				// Match complete.
				if (i_input == input.Length-1)
					return new (true, 0, i_match);
				// Potential candidate index for next match.
				if (isBoundary && charOption == input[0]) {
					// This potential match doesn't have a potential
					// next candidate yet.
					if (i_candidate < i_match)
						i_candidate = i_option;
				}
				i_input++;
				continue;
			}

			// Reset input index.
			i_input = 0;

			// If a candidate exists, start from there.
			if (i_candidate > i_match) {
				// Next iteration increments this immediately.
				i_option = i_candidate - 1;
				continue;
			}

			// If the current character can restart a new match, just
			// move the pointer back and retry (input index was reset
			// earlier).
			// This will only be true if a match had just failed.
			if (isBoundary && charOption == input[0])
				i_option--;
		}

		return new (false, 0, 0);
	}
	private static MatchData IsMatchContains(string input, string option) {
		int i_option = 0;
		int i_input = 0;
		int i_match = 0;
		int i_candidate = 0;
		for (; i_option<option.Length; i_option++) {
			char charOption = option[i_option];
			char charInput = input[i_input];

			// Character matched.
			if (charOption == charInput) {
				// Register a new match.
				if (i_input == 0)
					i_match = i_option;
				// Match complete.
				if (i_input == input.Length-1)
					return new (true, 0, i_match);
				// Potential candidate index for next match.
				if (charOption == input[0]) {
					// This potential match doesn't have a potential
					// next candidate yet.
					if (i_candidate < i_match)
						i_candidate = i_option;
				}
				i_input++;
				continue;
			}

			// Skip ahead in `option`.
			if (!IsAlpha(charOption) && IsAlpha(charInput)) {
				// Still check for potential candidates.
				if (charOption == input[0]) {
					// This potential match doesn't have a potential
					// next candidate yet.
					if (i_candidate < i_match)
						i_candidate = i_option;
				}
				continue;
			}

			// Reset input index.
			i_input = 0;

			// If a candidate exists, start from there.
			if (i_candidate > i_match) {
				// Next iteration increments this immediately.
				i_option = i_candidate - 1;
				continue;
			}

			// If the current character can restart a new match, just
			// move the pointer back and retry (input index was reset
			// earlier).
			// This will only be true if a match had just failed.
			if (charOption == input[0])
				i_option--;
		}

		return new (false, 0, 0);
	}
	private static MatchData IsMatchFuzzy(string input, string option) {
		int incursions = 0;
		int i_option = 0;
		int i_input = 0;
		int i_match = 0;
		for (; i_option<option.Length; i_option++) {
			char charOption = option[i_option];
			char charInput = input[i_input];

			// Character matched.
			if (option[i_option] == input[i_input]) {
				// Register a new match.
				if (i_input == 0)
					i_match = i_option;
				// Match complete.
				if (i_input == input.Length-1)
					return new (true, incursions, i_match);
				// Else, matched a regular character.
				i_input++;
				continue;
			}

			// Skip ahead in `option`.
			if (!IsAlpha(charOption) && IsAlpha(charInput))
				continue;

			// Otherwise, we have an unignorable mismatch.
			if (i_input > 0)
				incursions++;
		}

		return new (false, 0, 0);
	}
	

	// --------
	// Basic utility helper methods:
	// --------

	// Check if a character is an alphabetical character.
	// Returns false if the character is null.
	private static bool IsAlpha(char? c) =>
		c is not null and ((>='a' and <='z') or (>='A' and <='Z'));
	// Expand a list to a list of pairs by duplicating each entry.
	private static IList<(string, string)> ListToPairList(IList<string> list) {
		List<(string, string)> tupleList = new ();
		foreach (string item in list)
			tupleList.Add(new (item, item));
		return tupleList;
	}
}
