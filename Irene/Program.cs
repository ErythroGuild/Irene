using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Serilog;

using static Irene.Const;
using Irene.Commands;
using Irene.Modules;
using Irene.Utils;

namespace Irene;

class Program {
	// Discord client objects.
	internal static readonly DiscordClient irene;
	internal static DiscordGuild? guild;
	internal static readonly Dictionary<ulong, DiscordRole> roles = new ();
	internal static readonly Dictionary<ulong, DiscordChannel> channels = new ();
	internal static readonly Dictionary<ulong, DiscordGuildEmoji> emojis = new ();
	internal static readonly Logger log;
	static readonly Stopwatch stopwatch_connect;
	internal static bool is_guild_loaded = false;

	// File paths for config files.
	internal const string
		path_token   = @"config/token.txt",
		path_ak      = @"config/path_ak.txt",
		path_serilog = @"logs_D#+/serilog.txt",
		dir_logs = @"logs";

	// Discord IDs of various components.
	internal const ulong id_g_erythro = 317723973968461824;
	internal const string str_mention_n = @"<@!609752546994683911>";

	static Program() {
		log = new Logger(dir_logs, TimeSpan.FromDays(1));
		log.info("Initializing Irene...");

		// Parse authentication token from file.
		log.info("  Reading auth token...");
		string bot_token = "";
		using (StreamReader token = File.OpenText(path_token)) {
			bot_token = token.ReadLine() ?? "";
		}
		if (bot_token != "") {
			log.info("  Auth token found.");
			int disp_size = 8;
			string token_disp =
				bot_token[..disp_size] +
				new string('*', bot_token.Length - 2*disp_size) +
				bot_token[^disp_size..];
			log.debug($"    {token_disp}");
		} else {
			log.error("  Could not find auth token.");
			log.debug($"    Path: {path_token}");
			throw new FormatException($"Could not find auth token at {path_token}.");
		}

		// Initialize Serilog and connect it to Logger.
		log.debug("  Setting up Serilog...");
		Log.Logger = new LoggerConfiguration()
			.WriteTo.File(
				path_serilog,
				outputTemplate: "{Timestamp:yyyy-MM-dd H:mm:ss.ff} > [{Level:u3}] {Message}{NewLine}",
				rollingInterval: RollingInterval.Day)
			.CreateLogger();
		var serilog = new LoggerFactory().AddSerilog();
		log.debug("  Serilog has been set up.");

		// Initialize `DiscordClient`.
		stopwatch_connect = Stopwatch.StartNew();
		log.info("  Logging in to Discord.");
		irene = new DiscordClient(new DiscordConfiguration {
			Intents = DiscordIntents.All,
			LoggerFactory = serilog,
			Token = bot_token,
			TokenType = TokenType.Bot
		});

		log.info("Irene initialized.");
	}

	static void Main() {
		const string title_ascii =
			@"   __ ____   ____ __  __  ____" + "\n" +
			@"   || || \\ ||    ||\ || ||   " + "\n" +
			@"   || ||_// ||==  ||\\|| ||== " + "\n" +
			@"   || || \\ ||___ || \|| ||___" + "\n" +
			@"                              " + "\n";
		Console.ForegroundColor = ConsoleColor.DarkRed;
		Console.WriteLine();
		Console.WriteLine(title_ascii);
		Console.ForegroundColor = ConsoleColor.Gray;
		MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
	}
	static async Task MainAsync() {
		// Connected to discord servers (but not necessarily guilds yet!).
		irene.Ready += (irene, e) => {
			_ = Task.Run(() => {
				DiscordActivity helptext =
#if RELEASE
					new ("Sylvanas' redemption arc", ActivityType.Watching);
#else
					new ("with fire - DEBUGGING", ActivityType.Playing);
#endif
				irene.UpdateStatusAsync(helptext);

				stopwatch_connect.Stop();

				log.info("  Logged in to Discord servers.");
				log.debug($"    Took {stopwatch_connect.ElapsedMilliseconds} msec.");
				log.endl();
				log.info("Monitoring messages...");
				log.endl();
			});
			return Task.CompletedTask;
		};

		// Guild data has finished downloading.
		irene.GuildDownloadCompleted += (irene, e) => {
			_ = Task.Run(async () => {
				// Initialize guild.
				guild = await irene.GetGuildAsync(id_g_erythro);
				log.debug("Guild fetched.");

				// Initialize roles.
				var role_ids = typeof(RoleIDs).GetFields();
				foreach (var role_id in role_ids) {
					ulong id = (ulong)role_id.GetValue(null)!;
					DiscordRole role = guild.GetRole(id);
					roles.Add(id, role);
				}
				log.debug("Roles fetched.");

				// Initialize channels.
				var channel_ids = typeof(ChannelIDs).GetFields();
				foreach (var channel_id in channel_ids) {
					ulong id = (ulong)channel_id.GetValue(null)!;
					DiscordChannel channel = guild.GetChannel(id);
					channels.Add(id, channel);
				}
				log.debug("Channels fetched.");

				// Initialize emojis.
				List<DiscordGuildEmoji> emojis =
					new (await guild.GetEmojisAsync());
				var emoji_ids = typeof(EmojiIDs).GetFields();
				foreach (var emoji_id in emoji_ids) {
					ulong id = (ulong)emoji_id.GetValue(null)!;
					foreach (DiscordGuildEmoji emoji in emojis) {
						if (emoji.Id == id) {
							Program.emojis.Add(id, emoji);
							emojis.Remove(emoji);
							break;
						}
					}
				}
				log.debug("Emojis fetched.");

				is_guild_loaded = true;
				log.endl();

				// Initialize modules.
				AuditLog.init();
				WeeklyEvent.init();
				Welcome.init();
				Starboard.init();

				// Initialize commands.
				Help.init();
				Roles.init();
				Rank.init();
			});
			return Task.CompletedTask;
		};

		// (Any) message has been received.
		irene.MessageCreated += (irene, e) => {
			_ = Task.Run(async () => {
				DiscordMessage msg = e.Message;

				// Never respond to self!
				if (msg.Author == irene.CurrentUser)
					{ return; }

				// Trim leading whitespace.
				string msg_text = msg.Content.TrimStart();

				// Handle special commands.
				if (msg_text.ToLower().StartsWith("!keys")) {
					return;
				}
				if (msg_text.ToLower().StartsWith("/roll")) {
					log.info("Command received.");
					if (msg.Channel.IsPrivate) {
						log.debug("[DM command]");
					}
					DiscordUser author = msg.Author;
					log.debug($"  {author.Tag()}: {msg_text}");
					msg_text = msg_text.ToLower().Replace("/roll", "-roll");
					Command cmd = new (msg_text, msg);
					cmd.invoke();
					log.endl();
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
					log.info("Command received.");
					if (msg.Channel.IsPrivate) {
						log.debug("[DM command]");
					}
					DiscordUser author = msg.Author;
					log.debug($"  {author.Tag()}: {msg_text}");
					Command cmd = new (msg_text, msg);
					cmd.invoke();
					log.endl();
				}
			});
			return Task.CompletedTask;
		};

		await irene.ConnectAsync();
		await Task.Delay(-1);
	}
}
