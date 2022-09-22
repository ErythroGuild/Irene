using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Spectre.Console;

using Irene.Interactables;
using Irene.Modules;

namespace Irene;

class Program {
	// Debug flag.
	public static bool IsDebug { get {
		bool isDebug = false;
		CheckDebug(ref isDebug);
		return isDebug;
	} }

	// Discord client objects.
	public static DiscordClient Client { get; private set; }
	public static DiscordGuild? Guild { get; private set; } = null;
	public static GuildData? Erythro { get; private set; } = null;
	public static ConcurrentDictionary<ulong, DiscordChannel>? Channels { get; private set; }
	public static ConcurrentDictionary<ulong, DiscordEmoji>? Emojis { get; private set; }
	public static ConcurrentDictionary<ulong, DiscordRole>? Roles { get; private set; }
	public static ConcurrentDictionary<ulong, DiscordChannel>? VoiceChats { get; private set; }

	// DiscordGuild promise/future objects.
	// GuildFuture is only set when Guild and all associated variables
	// are set. This will always be complete when called, since it
	// should only be called *after* init functions are called.
	// `await AwaitGuildInit();` can be used to remove null warnings.
	private static Task<DiscordGuild> GuildFuture => _guildPromise.Task;
	private static readonly TaskCompletionSource<DiscordGuild> _guildPromise = new ();
	// Member must have a non-null value when exiting.
	#pragma warning disable CS8774
	[MemberNotNull(nameof(Guild), nameof(Channels), nameof(Emojis), nameof(Roles), nameof(VoiceChats))]
	public static async Task AwaitGuildInitAsync() => await GuildFuture;
	#pragma warning restore CS8774

	// Separate logger pipeline for D#+.
	private static Serilog.ILogger _loggerDsp;

	// Diagnostic timers.
	private static readonly Stopwatch
		_stopwatchConfig   = new (),
		_stopwatchConnect  = new (),
		_stopwatchDownload = new (),
		_stopwatchInitData = new (),
		_stopwatchRegister = new ();

	// File paths for config files.
	private const string
		_pathToken = @"config/token.txt",
		_pathLogs = @"logs/";

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

	public static async Task UpdateGuild() =>
		Guild = await Client.GetGuildAsync(Id_Erythro);

	// Construct all static members.
	static Program() {
		Console.OutputEncoding = System.Text.Encoding.UTF8;
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

		// Initialize static members.
		Guild = null;
		Channels = new ();
		Emojis = new ();
		Roles = new ();
		VoiceChats = new ();

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
		Client = new DiscordClient(new DiscordConfiguration {
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
		// Start configuration timer.
		_stopwatchConfig.Start();

		// Connected to discord servers (but not necessarily guilds yet!).
		Client.Ready += (irene, e) => {
			_ = Task.Run(() => {
				Log.Information("  Logged in to Discord servers.");
				_stopwatchConnect.LogMsecDebug("    Took {ConnectionTime} msec.");
			});
			return Task.CompletedTask;
		};

		// Guild data has finished downloading.
		Client.GuildDownloadCompleted += (irene, e) => {
			_ = Task.Run(async () => {
				// Stop download timer.
				Log.Information("  Downloaded guild data from Discord.");
				_stopwatchDownload.LogMsecDebug("    Took {DownloadTime} msec.");

				// Initialize guild.
				Guild = await irene.GetGuildAsync(Id_Erythro);
				Log.Information("  Guild initialized.");

				// Initialize data.
				Erythro = await GuildData.InitializeData(Client);

				// Initialize commands.
				CommandDispatcher.Register(Erythro);

				// Collate all command objects.
				IReadOnlyList<CommandHandler> handlers =
					CommandDispatcher.Handlers;
				List<DiscordApplicationCommand> commands = new ();
				foreach (CommandHandler handler in handlers)
					commands.Add(handler.Command);
				// Register (and fetch updated) commands.
				commands = new (await Client.BulkOverwriteGlobalApplicationCommandsAsync(commands));
				// Update command objects.
				foreach (DiscordApplicationCommand command in commands) {
					CommandDispatcher.HandlerTable[command.Name]
						.UpdateRegisteredCommand(command);
				}

				// Start data initialization timer.
				_stopwatchInitData.Start();

				// Initialize channels.
				Channels = new ();
				FieldInfo[] channel_ids =
					typeof(ChannelIDs).GetFields();
				foreach (var channel_id in channel_ids) {
					ulong id = (ulong)channel_id.GetValue(null)!;
					DiscordChannel channel = Guild.GetChannel(id);
					Channels.TryAdd(id, channel);
				}
				Log.Debug("    Channels populated.");

				// Initialize emojis.
				// Fetching entire list of emojis first instead of fetching
				// each emoji individually to minimize awaiting.
				Emojis = new ();
				FieldInfo[] emoji_ids =
					typeof(EmojiIDs).GetFields();
				List<DiscordGuildEmoji> emojis =
					new (await Guild.GetEmojisAsync());
				foreach (var emoji_id in emoji_ids) {
					ulong id = (ulong)emoji_id.GetValue(null)!;
					foreach (DiscordGuildEmoji emoji in emojis) {
						if (emoji.Id == id) {
							Emojis.TryAdd(id, emoji);
							emojis.Remove(emoji);
							break;
						}
					}
				}
				Log.Debug("    Emojis populated.");

				// Initialize roles.
				Roles = new ();
				FieldInfo[] role_ids =
					typeof(RoleIDs).GetFields();
				foreach (var role_id in role_ids) {
					ulong id = (ulong)role_id.GetValue(null)!;
					DiscordRole role = Guild.GetRole(id);
					Roles.TryAdd(id, role);
				}
				Log.Debug("    Roles populated.");

				// Initialize voice chats.
				VoiceChats = new ();
				FieldInfo[] voiceChat_ids =
					typeof(VoiceChatIDs).GetFields();
				foreach (var voiceChat_id in voiceChat_ids) {
					ulong id = (ulong)voiceChat_id.GetValue(null)!;
					DiscordChannel channel = Guild.GetChannel(id);
					VoiceChats.TryAdd(id, channel);
				}
				Log.Debug("    Voice chats populated.");

				// Stop data initialization timer.
				Log.Debug("    Discord data initialized and populated.");
				_stopwatchInitData.LogMsecDebug("      Took {DataInitTime} msec.");

				// Set GuildFuture.
				_guildPromise.SetResult(Guild);

				// Initialize modules.
				await InitModules();

				// Register (update-by-overwriting) application commands.
				_stopwatchRegister.Start();
				try {
					await Client.BulkOverwriteGuildApplicationCommandsAsync(Id_Erythro, Command.Commands);
					Log.Information("  Application commands registered.");
					_stopwatchRegister.LogMsecDebug("    Took {RegisterTime} msec.");
					Log.Debug("    Registered {CommandCount} commands.", Command.Commands.Count);
				} catch (BadRequestException e) {
					Log.Error("Failed to register commands.");
					Log.Error("{@Exception}", e.JsonMessage);
				}
			});
			return Task.CompletedTask;
		};

		// Log standard interaction data.
		static void LogInteractionData(Interaction interaction) {
			Log.Information(
				"{FlagDM}Command processed: /{CommandName}",
				interaction.Object.Channel.IsPrivate ? "[DM] " : "",
				interaction.Name
			);
			Log.Debug(
				"  Received: {TimestampReceived:HH:mm:ss.fff}",
				interaction.TimeReceived.ToLocalTime()
			);
			double? initialResponse = interaction
				.GetEventDuration(Interaction.Events.InitialResponse)
				?.TotalSeconds
				?? null;
			if (initialResponse is not null) {
				Log.Debug(
					"  Initial response - {DurationInitial,6:F2} sec",
					initialResponse
				);
			}
			double? finalResponse = interaction
				.GetEventDuration(Interaction.Events.FinalResponse)
				?.TotalSeconds
				?? null;
			if (finalResponse is not null) {
				Log.Debug(
					"  Final response   - {DurationFinal,6:F2} sec",
					finalResponse
				);
			}
		}
		// Even though this is used for context menu commands, the event
		// args always use `InteractionCreateEventArgs`. This means the
		// actual `Interaction` object must be created outside with an
		// appropriately specialized factory method, and passed in.
		static async Task HandleInteraction(Interaction interaction, InteractionCreateEventArgs e) {
			string commandName = interaction.Name;

			if (!CommandDispatcher.CanHandle(commandName)) {
				Log.Error("Unrecognized command: /{CommandName}", commandName);
				return;
			}
			e.Handled = true;
			await CommandDispatcher.HandleAsync(commandName, interaction);

			// Filter for autocomplete interactions (to avoid filling
			// logs with noise).
			// Autocomplete info needs to be logged by the handler itself.
			if (e.Interaction.Type == InteractionType.AutoComplete)
				return;
			LogInteractionData(interaction);
		}
		// Interaction received.
		Client.InteractionCreated += (irene, e) => {
			_ = Task.Run(async () => {
				Interaction interaction = Interaction.FromCommand(e);
				await HandleInteraction(interaction, e);
			});
			return Task.CompletedTask;
		};
		Client.ContextMenuInteractionCreated += (irene, e) => {
			_ = Task.Run(async () => {
				Interaction interaction = Interaction.FromContextMenu(e);
				await HandleInteraction(interaction, e);
			});
			return Task.CompletedTask;
		};

		// (Any) message has been received.
		Client.MessageCreated += (irene, e) => {
			_ = Task.Run(async () => {
				DiscordMessage msg = e.Message;

				// Never respond to self!
				if (msg.Author == irene.CurrentUser)
					return;

				// React to boost messages.
				if (msg.MessageType == MessageType.UserPremiumGuildSubscription) {
					DiscordEmoji emoji_gem =
						DiscordEmoji.FromUnicode("\U0001F48E");
					DiscordEmoji emoji_party =
						DiscordEmoji.FromUnicode("\U0001F973");
					await msg.CreateReactionAsync(emoji_gem);
					await msg.CreateReactionAsync(emoji_party);
				}

				// Trim leading whitespace.
				string msg_text = msg.Content.TrimStart();

				// Handle special commands.
				if (msg_text.ToLower().StartsWith("!keys")) {
					return;
				}
				if (msg_text.ToLower().StartsWith($"{irene.CurrentUser.Mention} :wave:")) {
					await msg.Channel.TriggerTypingAsync();
					await Task.Delay(1500);
					_ = msg.RespondAsync(":wave:");
					return;
				}
				if (msg_text.ToLower().StartsWith($"{irene.CurrentUser.Mention} 👋")) {
					await msg.Channel.TriggerTypingAsync();
					await Task.Delay(1500);
					_ = msg.RespondAsync(":wave:");
					return;
				}
			});
			return Task.CompletedTask;
		};

		// Stop configuration timer.
		Log.Debug("  Configured Discord client.");
		_stopwatchConfig.LogMsecDebug("    Took {ConfigTime} msec.");

		// Start connection timer and connect.
		_stopwatchConnect.Start();
		_stopwatchDownload.Start();
		await Client.ConnectAsync();
		await Task.Delay(-1);
	}

	private static async Task InitModules() {
		await AwaitGuildInitAsync();

		// List all initializers.
		List<Action>
			classes = new () {
				ClassSpec.Init,
				TimeZones.Init,
			},
			components = new () {
				Confirm.Init,
				Modal.Init,
				Pages.Init,
				Selection.Init,
				Interactables.Minigames.RPS.Init,
			},
			modules = new () {
				AuditLog.Init,
				Command.Init,
				Minigame.Init,
				IreneStatus.Init,
				Raid.Init,
				RecurringEvents.Init,
				Welcome.Init,
				Starboard.Init,
			};
		static void RunInitializers(List<Action> initializers) {
			foreach (Action initializer in initializers)
				initializer.Invoke();
		}

		RunInitializers(classes);
		RunInitializers(components);
		RunInitializers(modules);
		
		// Run command initializers last.
		foreach (Action initializer in Command.Initializers)
			initializer.Invoke();
	}

	// Private method used to define the public "IsDebug" property.
	[Conditional("DEBUG")]
	private static void CheckDebug(ref bool isDebug) { isDebug = true; }
}
