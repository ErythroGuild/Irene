using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Serilog;

using static Irene.Program;
using Irene.Modules;
using Irene.Commands;

namespace Irene {
	using id_r = RoleIDs;
	using id_ch = ChannelIDs;
	using id_e = EmojiIDs;

	class Program {
		// Discord client objects
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
			path_build   = @"config/commit.txt",
			path_version = @"config/tag.txt",
			path_serilog = @"logs_D#+/serilog.txt",
			dir_logs = @"logs";

		// Discord IDs of various components.
		internal const ulong id_g_erythro = 317723973968461824;
		internal const string str_mention_n = @"<@!609752546994683911>";
		internal static class RoleIDs {
			public const ulong
				// Colors
				ambassador = 724762507612520530,
				stylist    = 777570683159838760,
				bot        = 614642181587599439,
				admin      = 452298394967343105,
				officer    = 542021861727272981,
				member     = 452299909371265042,
				guest      = 452299336123023360,

				// Titles
				acclaimed = 780161029111021630,
				beloved   = 780159178202284053,
				esteemed  = 780159754676338700,

				// Officers
				raidOfficer   = 723061867127373874,
				eventPlanner  = 723061585156898836,
				mythicOfficer = 824852094493917194,
				recruiter     = 723060994859073607,
				banker        = 723061777213947974,

				// Guilds
				erythro = 542021676884557824,
				glaive  = 529135445712568323,
				dragons = 830985516639584257,
				angels  = 529135186382946345,
				asgard  = 539515941877448728,
				enclave = 676356017461919744,

				// Subscriptions
				raid    = 654062159956803664,
				mythics = 653334284299534336,
				ksm     = 665203067767226368,
				gearing = 854879377930453002,
				events  = 771584406724935710,
				herald  = 712469431573544972;
		}
		internal static class ChannelIDs {
			public const ulong
				// Broadcast
				rules     = 443002035604815872,
				announce  = 443001903123791873,
				resources = 542093130502111252,
				pins      = 648461869979271179,
				starboard = 778102974551293962,
				audit     = 778102942141382678,

				// Text
				general  = 317723973968461824,
				sharing  = 443000872968912896,
				spoilers = 454338639933997068,
				memes    = 543476538125844511,
				tts      = 444792161158823936,
				lfg      = 542093438238326804,
				bots     = 613034262823698452,
				news     = 612307690613637125,

				// Officer
				officer     = 542023200549371906,
				officerBots = 779385198533804062,
				officerInfo = 650603757113049088,

				// Voice chat
				v_hangout = 442986379392319490,
				v_request = 854884304362274816,
				v_raid1   = 443001346678063104,
				v_raid2   = 670853551869919232,
				v_officer = 542093594618757140,
				v_afk     = 545060010967957534,

				// Testing
				test       = 489274692255875091,
				ingest     = 777935219193020426,
				v_heartwood = 498636532530282512;
		}
		internal static class EmojiIDs {
			public const ulong
				erythro = 651475835387248660,

				wooloo   = 588112288628604928,
				toxic    = 839929134326349825,
				pooh     = 756560918208446514,
				mrrogers = 777861300423884803,

				// Analysis websites
				raiderio     = 699710975451856967,
				warcraftlogs = 699709399094132884,
				wipefest     = 699647884819169700,
				wowanalyzer  = 699648774099828796,

				// Roles
				tank = 708431859369115790,
				heal = 708431859435962418,
				dps  = 708431859385630862,

				// Covenants
				kyrian    = 697164668866658385,
				necrolord = 697164668506079332,
				nightfae  = 697164668975972433,
				venthyr   = 697166461164322927,

				// Classes
				dk      = 676750707759513611,
				dh      = 676750708175011860,
				druid   = 676750708447641611,
				hunter  = 676750708879523850,
				mage    = 676750710699720736,
				monk    = 676750843286126624,
				paladin = 676750876433711114,
				priest  = 676750889922330665,
				rogue   = 676750902895312901,
				shaman  = 676750915843260436,
				warlock = 676750927889170437,
				warrior = 676750939910045707;
		}

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

			// Initialize modules.
			AuditLog.init();
			Welcome.init();
			Starboard.init();

			// Initialize commands.
			Help.init();
			Roles.init();

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
						new ("@Irene -help", ActivityType.Watching);
#else
						new ("DEBUG MODE", ActivityType.Playing);
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
						log.debug($"  {author.tag()}: {msg_text}");
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
						log.debug($"  {author.tag()}: {msg_text}");
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
}
