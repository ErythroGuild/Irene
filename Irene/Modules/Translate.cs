namespace Irene.Modules;

using DeepL;
using DeepL.Model;

using DeepLLanguage = DeepL.Model.Language;

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
		public readonly LanguageType Type;
		public readonly string Code;
		public readonly string Name;
		public readonly string NativeName;
		public readonly string SearchString =>
			string.Join(' ', Name, NativeName, Code);

		public Language(
			LanguageType type,
			string code,
			string name,
			string nativeName
		) {
			Type = type;
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

	// DeepL client that wraps all API calls.
	private static readonly Translator _translator;

	// Default autocomplete options for languages.
	private static readonly List<string> _defaultSources = new () {
		CodeAutoDetect,
		LanguageCode.English,
		// Alphabetize the rest.
		LanguageCode.Chinese,
		LanguageCode.French,
		LanguageCode.German,
		LanguageCode.Japanese,
		LanguageCode.Portuguese,
		LanguageCode.Spanish,
	};
	private static readonly List<string> _defaultTargets = new () {
		CodeEnglishUS,
		// Alphabetize the rest.
		LanguageCode.Chinese,
		LanguageCode.French,
		LanguageCode.Japanese,
		LanguageCode.PortugueseBrazilian,
		LanguageCode.Spanish,
	};

	// Table of all languages, indexed by language code.
	private static readonly IReadOnlyDictionary<string, Language>
		_languagesSource,
		_languagesTarget;
	// Table of all languages, indexed by search string.
	private static readonly IReadOnlyDictionary<string, Language>
		_searchStringsSource,
		_searchStringsTarget;

	// The default language has to be en-us; attempting to translate
	// (*to*) just "English" will fail.
	public const string
		CodeEnglishUS = LanguageCode.EnglishAmerican,
		CodeAutoDetect = "Auto-Detect";

	// Display configuration properties.
	private static readonly DiscordColor _colorDeepL = new ("#0F2B46");
	private const string
		_footerText = "translated by DeepL",
		_footerIcon = @"https://i.imgur.com/dQ1sXFW.png";
	private const string _arrow = "\u21D2";

	private const string _pathKey = @"secrets/deepl.txt";

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
		_languagesSource = CompileLanguages(supportedSources, LanguageType.Source);
		_languagesTarget = CompileLanguages(supportedTargets, LanguageType.Target);
		_searchStringsSource = CacheSearchStrings(_languagesSource);
		_searchStringsTarget = CacheSearchStrings(_languagesTarget);
	}

	// Autocompleters.
	public static readonly Completer CompleterSource = new StringCompleter(
		(_, _) => GetSourceSearchStrings(),
		(_, _) => GetDefaultSourceSearchStrings(),
		8,
		s => SearchStringToOption(s, LanguageType.Source)
	);
	public static readonly Completer CompleterTarget = new StringCompleter(
		(_, _) => GetTargetSearchStrings(),
		(_, _) => GetDefaultTargetSearchStrings(),
		8,
		s => SearchStringToOption(s, LanguageType.Target)
	);
	private static List<string> GetSourceSearchStrings() {
		List<string> list = new (_searchStringsSource.Keys);
		list.Insert(0, CodeAutoDetect);
		return list;
	}
	private static List<string> GetTargetSearchStrings() =>
		new (_searchStringsTarget.Keys);
	private static List<string> GetDefaultSourceSearchStrings() {
		List<string> searchStrings = new ();
		foreach (string code in _defaultSources)
			searchStrings.Add(CodeToSearchString(code, LanguageType.Source));
		return searchStrings;
	}
	private static List<string> GetDefaultTargetSearchStrings() {
		List<string> searchStrings = new ();
		foreach (string code in _defaultTargets)
			searchStrings.Add(CodeToSearchString(code, LanguageType.Target));
		return searchStrings;
	}

	// If `languageSource` is null, the language will be auto-detected.
	// Unsupported languages will throw.
	public static async Task<Result> TranslateText(
		string input,
		Language? languageSource,
		Language languageTarget
	) {
		// Fetch results from API.
		TextResult result = await _translator.TranslateTextAsync(
			input,
			languageSource?.Code ?? null,
			languageTarget.Code
		);
		string detectedLanguageCode = result.DetectedSourceLanguageCode;
		Language sourceLanguage =
			CodeToLanguage(detectedLanguageCode, LanguageType.Source)
			?? throw new ImpossibleArgException(Commands.Translate.ArgSource, detectedLanguageCode);

		return new (
			result.Text,
			sourceLanguage.Name,
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

	// Helper method for converting the ID of an autocomplete option
	// (i.e. its language code) to the `Language` object itself.
	// Returns null for auto-detect.
	public static Language? CodeToLanguage(
		string languageCode,
		LanguageType type
	) =>
		(languageCode == CodeAutoDetect)
			? null
			: type switch {
				LanguageType.Source => _languagesSource[languageCode],
				LanguageType.Target => _languagesTarget[languageCode],
				_ => throw new UnclosedEnumException(typeof(LanguageType), type),
			};
	// Helper method for converting the ID of an autocomplete option
	// (i.e. its language code) directly to a search string, to pass
	// into a `StringCompleter`.
	private static string CodeToSearchString(
		string languageCode,
		LanguageType type
	) {
		Language? language = CodeToLanguage(languageCode, type);
		return language is null
			? CodeAutoDetect
			: language.Value.SearchString;
	}
	// Helper method for looking up a search string and converting it
	// to an autocomplete label/value pair.
	private static (string, string) SearchStringToOption(
		string searchString,
		LanguageType type
	) {
		if (searchString == CodeAutoDetect)
			return new (CodeAutoDetect, CodeAutoDetect);

		Language language = type switch {
			LanguageType.Source => _searchStringsSource[searchString],
			LanguageType.Target => _searchStringsTarget[searchString],
			_ => throw new UnclosedEnumException(typeof(LanguageType), type),
		};
		return new (language.Name, language.Code);
	}

	// Helper method for initializing language caches from API data.
	private static ConcurrentDictionary<string, Language> CompileLanguages(
		DeepLLanguage[] languages,
		LanguageType type
	) {
		ConcurrentDictionary<string, Language> table = new ();
		foreach (DeepLLanguage language_i in languages) {
			string id = language_i.Code;
			Language language = new (
				type,
				id,
				language_i.Name,
				language_i.CultureInfo.NativeName
			);
			table.TryAdd(id, language);
		}
		return table;
	}
	// Helper method for converting a language cache to a table indexed
	// by each language's search string.
	private static ConcurrentDictionary<string, Language> CacheSearchStrings(
		IReadOnlyDictionary<string, Language> languages
	) {
		ConcurrentDictionary<string, Language> cache = new ();
		foreach (Language language in languages.Values)
			cache.TryAdd(language.SearchString, language);
		return cache;
	}
}
