using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Timers;

using Microsoft.Extensions.Logging;
using Spectre.Console;

using Irene.Commands;
using Irene.Modules;

namespace Irene;

using CmdRoles = Roles;

class Program {
	// Discord client objects.
	public static List<Timer> Timers { get; private set; }
	public static DiscordClient Client { get; private set; }
	public static DiscordGuild? Guild  { get; private set; }
	public static Dictionary<ulong, DiscordChannel> Channels   { get; private set; }
	public static Dictionary<ulong, DiscordEmoji>   Emojis     { get; private set; }
	public static Dictionary<ulong, DiscordRole>    Roles      { get; private set; }
	public static Dictionary<ulong, DiscordChannel> VoiceChats { get; private set; }

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

	// Discord IDs of various components.
	private const ulong _id_Erythro = 317723973968461824;
	public const string str_mention_n = @"<@!609752546994683911>";

	public static async Task UpdateGuild() =>
		Guild = await Client.GetGuildAsync(_id_Erythro);

	// Construct all static members.
	static Program() {
		// Display logo (in TrueColor™).
		List<string> logo_ascii = new () {
			@"   [#da4331 on black]__ [/][#ffcec9 on black]____  [/][#da4331 on black] ____ [/][#ffcec9 on black]__  __ [/][#da4331 on black] ____[/]",
			@"   [#da4331 on black]|| [/][#ffcec9 on black]|| \\ [/][#da4331 on black]||    [/][#ffcec9 on black]||\ || [/][#da4331 on black]||   [/]",
			@"   [#da4331 on black]|| [/][#ffcec9 on black]||_// [/][#da4331 on black]||==  [/][#ffcec9 on black]||\\|| [/][#da4331 on black]||== [/]",
			@"   [#da4331 on black]|| [/][#ffcec9 on black]|| \\ [/][#da4331 on black]||___ [/][#ffcec9 on black]|| \|| [/][#da4331 on black]||___[/]",
			@"   [#da4331 on black]   [/][#ffcec9 on black]      [/][#da4331 on black]      [/][#ffcec9 on black]       [/][#da4331 on black]     [/]",
		};
		foreach (string line in logo_ascii)
			AnsiConsole.MarkupLine(line);

		// Set up Serilog.
		InitSerilog();
		Log.Information("Logging initialized (Serilog).");

		// Initialize static members.
		Timers = new ();
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

		// Initialize Discord cient.
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
	private static void InitStatic() { return; }
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

	static void Main() {
		// Initialize static members.
		InitStatic();

		// Run async entry point.
		MainAsync()
			.ConfigureAwait(false)
			.GetAwaiter()
			.GetResult();
	}
	static async Task MainAsync() {
		// Start configuration timer.
		_stopwatchConfig.Start();

		// Connected to discord servers (but not necessarily guilds yet!).
		Client.Ready += (irene, e) => {
			_ = Task.Run(() => {
				_stopwatchConnect.Stop();
				Log.Information("  Logged in to Discord servers.");
				long connect_time = GetMsec(_stopwatchConnect);
				Log.Debug("    Took {ConnectionTime} msec.", connect_time);

				// Update bot status.
#if DEBUG
				DiscordActivity helptext =
					new(@"with 🔥 - [DEBUG]", ActivityType.Playing);
				irene.UpdateStatusAsync(helptext);
#endif
			});
			return Task.CompletedTask;
		};

		// Guild data has finished downloading.
		Client.GuildDownloadCompleted += (irene, e) => {
			_ = Task.Run(async () => {
				// Stop download timer.
				_stopwatchDownload.Stop();
				Log.Information("  Downloaded guild data from Discord.");
				long download_time = GetMsec(_stopwatchDownload);
				Log.Debug("    Took {DownloadTime} msec.", download_time);

				// Initialize guild.
				Guild = await irene.GetGuildAsync(_id_Erythro);
				Log.Information("  Guild fetched and initialized.");

				// Start data initialization timer.
				_stopwatchInitData.Start();

				// Initialize channels.
				var channel_ids = typeof(ChannelIDs).GetFields();
				foreach (var channel_id in channel_ids) {
					ulong id = (ulong)channel_id.GetValue(null)!;
					DiscordChannel channel = Guild.GetChannel(id);
					Channels.Add(id, channel);
				}
				Log.Debug("  Channels populated.");

				// Initialize emojis.
				// Fetching entire list of emojis first instead of fetching
				// each emoji individually to minimize awaiting.
				List<DiscordGuildEmoji> emojis =
					new (await Guild.GetEmojisAsync());
				var emoji_ids = typeof(EmojiIDs).GetFields();
				foreach (var emoji_id in emoji_ids) {
					ulong id = (ulong)emoji_id.GetValue(null)!;
					foreach (DiscordGuildEmoji emoji in emojis) {
						if (emoji.Id == id) {
							Emojis.Add(id, emoji);
							emojis.Remove(emoji);
							break;
						}
					}
				}
				Log.Debug("  Emojis populated.");

				// Initialize roles.
				var role_ids = typeof(RoleIDs).GetFields();
				foreach (var role_id in role_ids) {
					ulong id = (ulong)role_id.GetValue(null)!;
					DiscordRole role = Guild.GetRole(id);
					Roles.Add(id, role);
				}
				Log.Debug("  Roles populated.");

				// Initialize voice chats.
				var voiceChat_ids = typeof(VoiceChatIDs).GetFields();
				foreach (var voiceChat_id in voiceChat_ids) {
					ulong id = (ulong)voiceChat_id.GetValue(null)!;
					DiscordChannel channel = Guild.GetChannel(id);
					VoiceChats.Add(id, channel);
				}
				Log.Debug("  Voice chats populated.");

				// Stop data initialization timer.
				_stopwatchInitData.Stop();
				Log.Debug("  Discord data initialized and populated.");
				long dataInit_time = GetMsec(_stopwatchInitData);
				Log.Debug("    Took {DataInitTime} msec.", dataInit_time);

				// Initialize modules.
				AuditLog.init();
				Command.Init();
				WeeklyEvent.init();
				Welcome.init();
				Starboard.init();

				// Initialize commands.
				Help.init();
				CmdRoles.init();
				Rank.init();

				// Register (update-by-overwriting) application commands.
				_stopwatchRegister.Start();
				await Client.BulkOverwriteGuildApplicationCommandsAsync(_id_Erythro, Command.Commands);
				_stopwatchRegister.Stop();
				Log.Information("  Application commands registered.");
				Log.Debug("    Registered {CommandCount} commands.", Command.Commands.Count);
				long register_time = GetMsec(_stopwatchRegister);
				Log.Debug("    Took {RegisterTime} msec.", register_time);
			});
			return Task.CompletedTask;
		};

		// Interaction received.
		Client.InteractionCreated += (irene, e) => {
			_ = Task.Run(() => {
				string name = e.Interaction.Data.Name;
				if (Command.Handlers.ContainsKey(name)) {
					Command.Handlers[name].Invoke(e);
					e.Handled = true;
				}
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

				// Trim leading whitespace.
				string msg_text = msg.Content.TrimStart();

				// Handle special commands.
				if (msg_text.ToLower().StartsWith("!keys")) {
					return;
				}
				if (msg_text.ToLower().StartsWith("/roll")) {
					Log.Information("Command received.");
					if (msg.Channel.IsPrivate) {
						Log.Debug("[DM command]");
					}
					DiscordUser author = msg.Author;
					Log.Debug($"  {author.Tag()}: {msg_text}");
					msg_text = msg_text.ToLower().Replace("/roll", "-roll");
					Command cmd = new (msg_text, msg);
					cmd.invoke();
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

				// Handle normal commands.
				string str_mention = irene.CurrentUser.Mention;
				if (msg_text.StartsWith(str_mention_n)) {
					msg_text = msg_text.Replace(str_mention_n, str_mention);
				}
				if (msg_text.StartsWith(str_mention)) {
					msg_text = msg_text[str_mention.Length..];
					msg_text = msg_text.TrimStart();
					Log.Information("Command received.");
					if (msg.Channel.IsPrivate) {
						Log.Debug("[DM command]");
					}
					DiscordUser author = msg.Author;
					Log.Debug($"  {author.Tag()}: {msg_text}");
					Command cmd = new (msg_text, msg);
					cmd.invoke();
				}
			});
			return Task.CompletedTask;
		};

		// Stop configuration timer.
		_stopwatchConfig.Stop();
		Log.Debug("  Configured Discord client.");
		long config_time = GetMsec(_stopwatchConfig);
		Log.Debug("    Took {ConfigTime} msec.", config_time);

		// Start connection timer and connect.
		_stopwatchConnect.Start();
		_stopwatchDownload.Start();
		await Client.ConnectAsync();
		await Task.Delay(-1);
	}

	// Syntax sugar for fetching stopwatch time.
	private static long GetMsec(Stopwatch stopwatch) =>
		stopwatch.ElapsedMilliseconds;
}
