namespace Irene;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Spectre.Console;

using Irene.Modules;

using CommandResult = CommandHandler.ResultType;

class Program {
	// The GuildData object contains the program's `DiscordClient` and
	// `DiscordGuild`, as well as other objects populated from those.
	public static GuildData? Erythro { get; private set; } = null;
	// Although `Erythro` is nullable, initialization order should ensure
	// it's initialized before any dependents are. Use `CheckErythroInit()`
	// to remove any nullable warnings.
	[MemberNotNull(nameof(Erythro))]
	public static void CheckErythroInit() {
		if (Erythro is null)
			throw new UninitializedException();
	}

	// --------
	// Properties, fields, and constants:
	// --------

	// Separate logger pipeline for D#+.
	private static Serilog.ILogger _loggerDsp;
	// Discord client objects.
	private static readonly DiscordClient _client;
	// Diagnostic timers.
	private static readonly Stopwatch
		_stopwatchConnect  = new (),
		_stopwatchDownload = new ();

	private static readonly TimeSpan _timeoutRegex = TimeSpan.FromMilliseconds(200);
	// Date / time format strings.
	private const string
		_formatLogs = @"yyyy-MM\/lo\g\s-MM-dd";
	// Serilog message templates.
	private const string
		_templateConsoleDebug   = @"[grey]{Timestamp:H:mm:ss} [{Level:w4}] {Message:lj}[/]{NewLine}{Exception}",
		_templateConsoleInfo    = @"[grey]{Timestamp:H:mm:ss}[/] [silver][{Level:w4}][/] {Message:lj}{NewLine}{Exception}",
		_templateConsoleWarning = @"[grey]{Timestamp:H:mm:ss}[/] [yellow][{Level:u4}][/] {Message:lj}{NewLine}{Exception}",
		_templateConsoleError   = @"[red]{Timestamp:H:mm:ss}[/] [invert red][{Level}][/] {Message:lj}{NewLine}{Exception}",
		_templateFile           = @"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} > [{Level:u3}] {Message:j}{NewLine}{Exception}";

	// File paths for config files.
	private const string
		_pathToken = @"config/token.txt",
		_pathLogs = @"logs/";


	// --------
	// Initialization:
	// --------

	// Set up logger and D#+ client.
	static Program() {
		// Ensure the console window can display emoji/colors properly.
		Console.OutputEncoding = Encoding.UTF8;

		// Display logo.
		const string
			red  = "[#da4331 on black]",
			pink = "[#ffcec9 on black]";
		const string logo_ascii =
			$"""
			   {red}__ [/]{pink}____  [/]{red} ____ [/]{pink}__  __ [/]{red} ____[/]
			   {red}|| [/]{pink}|| \\ [/]{red}||    [/]{pink}||\ || [/]{red}||   [/]
			   {red}|| [/]{pink}||_// [/]{red}||==  [/]{pink}||\\|| [/]{red}||== [/]
			   {red}|| [/]{pink}|| \\ [/]{red}||___ [/]{pink}|| \|| [/]{red}||___[/]
			   {red}   [/]{pink}      [/]{red}      [/]{pink}       [/]{red}     [/]
			""";
		AnsiConsole.Markup(logo_ascii);
		AnsiConsole.WriteLine();

		// Set up Serilog.
		InitSerilog();
		Log.Information("Logging initialized (Serilog).");

		// Set program-wide regex timeout.
		AppDomain.CurrentDomain.SetData(
			"REGEX_DEFAULT_MATCH_TIMEOUT",
			_timeoutRegex
		);

		// Parse authentication token from file.
		// Throw if token is not found.
		string bot_token = "";
		using (StreamReader token = File.OpenText(_pathToken)) {
			Log.Debug("  Token file opened.");
			bot_token = token.ReadLine() ?? "";
		}
		if (bot_token != "") {
			Log.Information("  Authentication token found.");
			int disp_size = 8;
			string token_disp =
				bot_token[..disp_size] +
				new string('*', bot_token.Length - 2*disp_size) +
				bot_token[^disp_size..];
			Log.Debug("    {DisplayToken}", token_disp);
			Log.Verbose("    {Token}", bot_token);
		} else {
			Log.Fatal("  No authentication token found.");
			Log.Debug("    Path: {TokenPath}", _pathToken);
			throw new FormatException($"Could not find auth token at {_pathToken}.");
		}

		// Initialize Discord client.
		_client = new DiscordClient(new DiscordConfiguration {
			Intents = DiscordIntents.All,
			LoggerFactory = new LoggerFactory().AddSerilog(_loggerDsp),
			Token = bot_token,
			TokenType = TokenType.Bot
		});
		Log.Information("  Discord client configured.");
		Log.Debug("  Serilog attached to D#+.");
	}

	// A dummy function to force the static constructor to run.
	private static void InitStatic() { }
	// Set up and configure Serilog.
	[MemberNotNull(nameof(_loggerDsp))]
	private static void InitSerilog() {
		// General logs (all logs except D#+).
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			// Spectre.Console colorizes/formats any logs.
			.WriteTo.Map(
				e => e.Level,
				(level, writeTo) => writeTo.DelegatingTextSink(
					s => {
						s = s.EscapeMarkup()
							.Replace(@"[[/]]", @"[/]")
							.Replace(@"[[grey]]", @"[grey]")
							.Replace(@"[[silver]]", @"[silver]")
							.Replace(@"[[yellow]]", @"[yellow]")
							.Replace(@"[[red]]", @"[red]")
							.Replace(@"[[invert red]]", @"[invert red]");
						AnsiConsole.Markup(s);
					},
					outputTemplate: level switch {
						Serilog.Events.LogEventLevel.Debug or
						Serilog.Events.LogEventLevel.Verbose =>
							_templateConsoleDebug,
						Serilog.Events.LogEventLevel.Information =>
							_templateConsoleInfo,
						Serilog.Events.LogEventLevel.Warning =>
							_templateConsoleWarning,
						Serilog.Events.LogEventLevel.Error or
						Serilog.Events.LogEventLevel.Fatal =>
							_templateConsoleError,
						_ =>
							_templateConsoleInfo,
					}
				)
			)
			// New directories are created for every month of logs.
			.WriteTo.Map(
				e => DateTime.Now.ToString(_formatLogs),
				(prefix, writeTo) => writeTo.File(
					$"{_pathLogs}{prefix}.txt",
					outputTemplate: _templateFile,
					retainedFileTimeLimit: null
				)
			)
			.CreateLogger();

		// D#+ logs.
		_loggerDsp = new LoggerConfiguration()
			.MinimumLevel.Information()
			// New directories are created for every month of logs.
			.WriteTo.Map(
				e => {
					string prefix = DateTime.Now.ToString(_formatLogs);
					return prefix.Replace(@"logs-", @"logs-DSharpPlus-");
				},
				(prefix, writeTo) => writeTo.File(
					$"{_pathLogs}{prefix}.txt",
					outputTemplate: _templateFile,
					retainedFileTimeLimit: null
				)
			)
			.CreateLogger();
	}


	// --------
	// Main program:
	// --------

	public static void Main() {
		// Initialize static members.
		InitStatic();

		// Run async entry point.
		MainAsync()
			.ConfigureAwait(false)
			.GetAwaiter()
			.GetResult();
	}
	private static async Task MainAsync() {
		// Connected to discord servers, but not necessarily guilds yet!
		_client.Ready += (_, _) => {
			Log.Information("  Logged in to Discord servers.");
			_stopwatchConnect.LogMsec(2);
			return Task.CompletedTask;
		};

		// All guild data has finished downloading.
		_client.GuildDownloadCompleted += (_, _) => {
			_ = Task.Run(async () => {
				// Stop download timer.
				Log.Information("  Discord guild data downloaded.");
				_stopwatchDownload.LogMsec(2);

				// Initialize GuildData.
				Erythro = await GuildData.InitializeData(_client);
				Log.Debug("  GuildData object initialized.");

				// --------
				// IMPORTANT!
				// After this point, `Erythro` initialization is complete,
				// and everything else can safely be initialized.
				// --------

				// Initialize commands.
				Dispatcher.ReplaceAllHandlers();

				// Collate all command objects.
				List<DiscordCommand> commands = new ();
				List<string> commandsAllowed = new () {
					"help",
				};
				foreach (CommandHandler handler in Dispatcher.Handlers) {
					if (!commandsAllowed.Contains(handler.Command.Name)) {
						Log.Debug("   {Command}", handler.Command.Name);
						continue;
					}
					commands.Add(handler.Command);
				}
				// Register (and fetch updated) commands.
				Stopwatch stopwatchRegister = Stopwatch.StartNew();
				commands = new (await _client.BulkOverwriteGlobalApplicationCommandsAsync(commands));
				Log.Information("  Registered all commands.");
				stopwatchRegister.LogMsec(2);

				// Update command objects and keep a tally of types.
				// The tally is used for updating `Module.About`.
				int countSlash = 0, countContext = 0;
				foreach (DiscordCommand command in commands) {
					Dispatcher.Table[command.Name]
						.UpdateRegisteredCommand(command);
					IncrementCommandCounters(
						command,
						ref countSlash,
						ref countContext
					);
				}
				// Update status module with registered command count.
				About.SetRegisteredCommands(countSlash, countContext);

				// --------
				// After this point, commands have their proper IDs.
				// --------

				// Initialize remaining uninitialized modules.
				// `Dispatcher` should've initialized all commands.
				EnsureStaticConstruction();

				// Only register command & message handlers after guild
				// initialization completes; this ensures they cannot
				// be called before everything is ready.
				RegisterInteractionHandlers();
				RegisterMessageHandler();
			});
			return Task.CompletedTask;
		};

		// Start connection timer and connect.
		_stopwatchConnect.Start();
		_stopwatchDownload.Start();
		await _client.ConnectAsync();
		await Task.Delay(-1);
	}


	// --------
	// Decomposed helper methods for Main():
	// --------

	// Register event handlers for received interactions.
	private static void RegisterInteractionHandlers() {
		// C# only infers an unambiguous discard if there are multiple
		// parameters named `_`, so this `c` needs to be named.
		_client.InteractionCreated += (c, e) => {
			_ = Task.Run(async () => {
				Interaction interaction = Interaction.FromCommand(e);
				await HandleInteraction(interaction, e);
			});
			return Task.CompletedTask;
		};
		_client.ContextMenuInteractionCreated += (c, e) => {
			_ = Task.Run(async () => {
				Interaction interaction = Interaction.FromContextMenu(e);
				await HandleInteraction(interaction, e);
			});
			return Task.CompletedTask;
		};
	}

	// Register event handler for received messages.
	private static void RegisterMessageHandler() {
		// C# only infers an unambiguous discard if there are multiple
		// parameters named `_`, so this `c` needs to be named.
		_client.MessageCreated += (c, e) => {
			_ = Task.Run(async () => {
				// Only handle messages from Erythro.
				if ((e.Guild?.Id ?? null) != id_g.erythro)
					return;

				CheckErythroInit();
				DiscordMessage message = e.Message;

				// Never respond to self!
				if (message.Author == _client.CurrentUser)
					return;

				// Special handler for the `Keys` command, to emulate
				// the way the command behaves in-game.
				string messageText = message.Content.Trim().ToLower();
				if (messageText.StartsWith("!keys")) {
					return;
				}

				// React to boost messages.
				if (message.MessageType == MessageType.UserPremiumGuildSubscription) {
					await ReactToBoostAsync(Erythro, message);
					return;
				}

				// Any other message is parsed and responded to by the
				// chat module.
				await Chatbot.RespondAsync(message);
			});
			return Task.CompletedTask;
		};
	}

	// Increment command counter separately for different types of
	// commands (slash commands vs. context menu commands).
	private static void IncrementCommandCounters(
		DiscordCommand command,
		ref int countSlash,
		ref int countContext
	) {
		switch (command.Type) {
		case CommandType.SlashCommand:
			countSlash++;
			break;
		case CommandType.MessageContextMenu:
		case CommandType.UserContextMenu:
			countContext++;
			break;
		}
	}

	// Search through all Irene's types to find ones which have static
	// constructors, and ensure that they're called (without the need
	// to manually call an empty `Init()` method).
	private static void EnsureStaticConstruction() {
		List<Type> typesAll = new (Assembly.GetExecutingAssembly().GetTypes());
		HashSet<Type> typesIrene = new ();

		// Filter all types to only select the ones inside the same
		// namespace as `Program` (or deeper-nested ones). Since
		// namespaces have strict naming rules, the namespace string
		// can safely be split on '.'.
		string? namespaceIrene = typeof(Program).Namespace;
		foreach (Type type in typesAll) {
			string @namespace = type.Namespace ?? "";
			string[] namespaces = @namespace.Split('.', 2);
			if (namespaces[0] == namespaceIrene) {
				typesIrene.Add(type);
			}
		}

		Util.RunAllStaticConstructors(typesIrene);
	}

	// Even though context menu commands and autocomplete interactions
	// share handling logic, they pass different event args. This means
	// an `Interaction` object needs to be created appropriately outside
	// the method, then passed in.
	private static async Task HandleInteraction(
		Interaction interaction,
		InteractionCreateEventArgs e
	) {
		string commandName = interaction.Name;

		// Only attempt to handle registered commands.
		if (!Dispatcher.CanHandle(commandName)) {
			Log.Error("Unrecognized command: /{CommandName}", commandName);
			return;
		}
		e.Handled = true;

		// `UnknownCommandException` is the only type of exception that
		// can't be caught by the `CommandHandler`, since it will throw
		// before the `CommandHandler` is even called.
		CommandResult commandResult;
		try {
			commandResult = await
				Dispatcher.HandleAsync(commandName, interaction);
		} catch (IreneException ex) {
			commandResult = CommandResult.Exception;
			await interaction.RegisterAndRespondAsync(ex.ResponseMessage, true);
			ex.Log();
		}

		// Notify Ernie in DMs if an exception occured.
		if (commandResult == CommandResult.Exception) {
			DiscordUser adminUser =
				await _client.GetUserAsync(id_u.admin);
			DiscordMember? admin = await adminUser.ToMember();

			if (admin is not null) {
				string timestamp = DateTimeOffset.UtcNow
					.Timestamp(Util.TimestampStyle.TimeLong);
				string errorDM =
					$"""
					An exception occured!
					Check logs for errors occuring just before: {timestamp}
					""";
				await admin.SendMessageAsync(errorDM);
				Log.Information("  Notified Ernie with a DM.");
			}
		}

		// Filter for autocomplete interactions (to avoid filling
		// logs with noise).
		// Autocomplete info needs to be logged by the handler itself.
		if (e.Interaction.Type == InteractionType.AutoComplete)
			return;

		interaction.LogResponseData();
	}

	// React to a message with some celebratory emojis.
	private static async Task ReactToBoostAsync(
		GuildData erythro,
		DiscordMessage message
	) {
		List<DiscordEmoji> emojisBoost = new () {
			erythro.Emoji(id_e.eryLove),
			erythro.Emoji(id_e.notoParty),
			erythro.Emoji(id_e.notoFireworks),
			DiscordEmoji.FromUnicode("\U0001F48E") // :gem:
		};
		// Having a slight delay makes it feel more "human".
		await Task.Delay(TimeSpan.FromSeconds(6));
		foreach (DiscordEmoji emoji in emojisBoost) {
			await Task.Delay(TimeSpan.FromSeconds(1.5));
			await message.CreateReactionAsync(emoji);
		}
	}
}
