using DeepL;
using DeepL.Model;

using DeepLLanguage = DeepL.Model.Language;

namespace Irene.Modules;

class Translate {
	public enum LanguageType { Source, Target }

	// Source/target languages are given as names instead of codes.
	public record struct Result(
		string Text,
		string LanguageSource,
		string LanguageTarget
	);
	// Contains all fields needed for displaying a language.
	public readonly record struct Language {
		public readonly string Code;
		public readonly string Name;
		public readonly string NativeName;

		public Language(string code, string name, string nativeName) {
			Code = code;
			Name = name;
			NativeName = nativeName;
		}

		// Equality operators need to be separately defined, or sometimes
		// actual matching languages will have differing `.Name` values
		// and fail to match.
		public readonly bool Equals(Language other) =>
			Code == other.Code;
		public override readonly int GetHashCode() =>
			Code.GetHashCode();
	}

	public const string Language_EnglishUS = LanguageCode.EnglishAmerican;
	public const string
		Label_Detect = "Auto-Detect",
		Id_Detect = "autodetect";

	// DeepL client that wraps all API calls.
	private static readonly Translator _translator;

	// The master table of all languages, indexed by language code.
	private static readonly IReadOnlyDictionary<string, Language> _languages;
	// The sets of supported source and target languages.
	private static readonly IReadOnlySet<Language> _languagesSource, _languagesTarget;
	// List of autocomplete options to search through - (Label, Value).
	private static readonly IReadOnlyList<(string, string)> _optionsSource, _optionsTarget;

	// Display configuration properties.
	private static readonly DiscordColor _colorDeepL = new ("#0F2B46");
	private const string
		_footerText = "translated by DeepL",
		_footerIcon = @"https://i.imgur.com/dQ1sXFW.png";
	private const string _arrow = "\u21D2";

	private const int _maxOptions = 20;
	private const string _pathKey = @"config/deepl.txt";

	static Translate() {
		// Load DeepL authentication key.
		StreamReader file = File.OpenText(_pathKey);
		string? key = file.ReadLine();
		file.Close();

		if (key is null)
			throw new InvalidOperationException("DeepL API token missing.");

		_translator = new (key);

		// Fetch language lists.
		// These `.Result` calls to async methods will block, but since
		// it happens in a static constructor it's sequential and only
		// results in a slightly longer startup time.
		DeepLLanguage[] supportedSources = _translator.GetSourceLanguagesAsync().Result;
		DeepLLanguage[] supportedTargets = _translator.GetTargetLanguagesAsync().Result;

		// Populate source and target language container caches.
		Dictionary<string, Language> languages = new ();
		HashSet<Language> languagesSource =
			CompileLanguageCache(supportedSources, ref languages);
		HashSet<Language> languagesTarget = 
			CompileLanguageCache(supportedTargets, ref languages);

		// Alphabetize supported language sets into lists.
		List<Language> languagesSourceList = new (languagesSource);
		List<Language> languagesTargetList = new (languagesTarget);
		languagesSourceList.Sort((a, b) => string.Compare(a.Name, b.Name, true));
		languagesTargetList.Sort((a, b) => string.Compare(a.Name, b.Name, true));

		// Set static fields.
		_languages = languages;
		_languagesSource = languagesSource;
		_languagesTarget = languagesTarget;

		// Create option lists.
		List<(string, string)> optionsSource = new ();
		List<(string, string)> optionsTarget = new ();

		foreach (Language language in languagesSourceList)
			optionsSource.Add(new (language.Name, language.Code));
		foreach (Language language in languagesTargetList)
			optionsTarget.Add(new (language.Name, language.Code));

		_optionsSource = optionsSource;
		_optionsTarget = optionsTarget;
	}

	// If `languageSource` is null, the language will be auto-detected.
	// Unsupported languages will throw.
	public static async Task<Result> TranslateText(
		string input,
		Language? languageSource,
		Language languageTarget
	) {
		// Ensure source and target languages are supported.
		if (languageSource is not null &&
			!_languagesSource.Contains(languageSource.Value)
		) {
			throw new ArgumentException("Unsupported source language.", nameof(languageSource));
		}
		if (!_languagesTarget.Contains(languageTarget))
			throw new ArgumentException("Unsupported target language.", nameof(languageTarget));

		// Fetch results from API.
		TextResult result = await _translator.TranslateTextAsync(
			input,
			languageSource?.Code ?? null,
			languageTarget.Code
		);
		Language? sourceLanguage =
			ParseLanguageCode(result.DetectedSourceLanguageCode);
		if (sourceLanguage is null)
			throw new ArgumentException("Invalid source language.");

		return new (
			result.Text,
			sourceLanguage.Value.Name,
			languageTarget.Name
		);
	}

	// Takes an already translated `Result` and display it in an embed.
	public static DiscordEmbed RenderResult(string input, Result result) {
		string title = $"{result.LanguageSource} {_arrow} {result.LanguageTarget}";

		string content =
			$"""
			> {result.Text}

			*Original*

			> {input}
			""";

		DiscordEmbedBuilder embed =
			new DiscordEmbedBuilder()
			.WithTitle(title)
			.WithColor(_colorDeepL)
			.WithDescription(content)
			.WithFooter(_footerText, _footerIcon)
			.WithTimestamp(DateTimeOffset.UtcNow);
		return embed.Build();
	}

	// Both source and target languages are combined in a single call.
	// Generally the sets are identical. Any invalid cases are handled
	// in the main `TranslateText()` method.
	// "Auto-Detect" won't be in the table, and correctly returns null.
	public static Language? ParseLanguageCode(string? code) =>
		(code is not null && _languages.ContainsKey(code))
			? _languages[code]
			: null;

	// Returns a list of `(string Label, string Value)` options.
	public static IList<(string, string)> Autocomplete(bool isSource, string arg) {
		arg = arg.Trim().ToLower();

		List<(string, string)> results = new ();
		if (isSource)
			results.Add((Label_Detect, Id_Detect));

		// Compile all valid options.
		IReadOnlyList<(string, string)> optionsTotal = isSource switch {
			true  => _optionsSource,
			false => _optionsTarget,
		};
		foreach ((string, string) option in optionsTotal)
			results.Add(option);

		// Filter down matching options.
		List<(string, string)> resultsFiltered = new ();
		foreach ((string, string id) option in results) {
			// Null case isn't an actual "Language" record, just using
			// it to conveniently hold the data to filter on.
			Language language = ParseLanguageCode(option.id)
				?? new (Id_Detect, Label_Detect, Label_Detect);
			if (language.Code.ToLower().Contains(arg) ||
				language.Name.ToLower().Contains(arg) ||
				language.NativeName.ToLower().Contains(arg)
			) {
				resultsFiltered.Add(option);
			}
		}

		// Cap the number of results.
		if (resultsFiltered.Count > _maxOptions)
			resultsFiltered = resultsFiltered.GetRange(0, _maxOptions);

		return resultsFiltered;
	}

	// Helper method for initializing static caches from API data.
	private static HashSet<Language> CompileLanguageCache(
		DeepLLanguage[] languagesSupported,
		ref Dictionary<string, Language> languages
	) {
		HashSet<Language> languageSet = new ();
		foreach (DeepLLanguage language_i in languagesSupported) {
			string id = language_i.Code;
			Language language = new (
				id,
				language_i.Name,
				language_i.CultureInfo.NativeName
			);
			languageSet.Add(language);
			if (!languages.ContainsKey(id))
				languages.TryAdd(id, language);
		}
		return languageSet;
	}
}
