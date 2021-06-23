using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.EventArgs;

using static Irene.Program;

namespace Irene.Commands {
	using ButtonPressLambda = Emzi0767.Utilities.AsyncEventHandler<DiscordClient, ComponentInteractionCreateEventArgs>;
	using id_r = RoleIDs;
	using id_ch = ChannelIDs;
	using id_e = EmojiIDs;

	class Help : ICommands {
		class PageHandler {
		}

		public static string help() {
			StringWriter text = new ();

			text.WriteLine("All commands are also available in DMs if you'd like to keep them to yourself.");
			text.WriteLine("(You will still need to include `@Irene` in the command.)");
			text.WriteLine("`@Irene -help` displays a summary of available commands.");
			text.WriteLine("`@Irene -help <command>` displays help for that specific command.");
			text.WriteLine("If you need more help, ask, or shoot Ernie a message! :+1:");

			text.Flush();
			return text.ToString();
		}

		public static void run(Command cmd) {
			// Command names are case-insensitive.
			string arg = cmd.args.Trim().ToLower();

			// Display specific help if requested.
			string? help_cmd = Command.help(arg);
			if (help_cmd is not null) {
				log.info("  Returned help string.");
				_ = cmd.msg.RespondAsync(help_cmd);
				return;
			}

			// Display general-case help.
			StringWriter text = new ();

			text.WriteLine("All command names are case-insensitive.");
			text.WriteLine("Commands can be DM'd but still need to include `@Irene`.");
			text.WriteLine("*`<required argument>`, `[optional argument]`*");
			text.WriteLine();
			text.WriteLine("`@Irene -help`: Display this help text.");
			text.WriteLine("`@Irene -help [command]`: Display help for a command.");
			text.WriteLine("`@Irene -roles`: List the available roles you can assign yourself.");
			text.WriteLine("`@Irene -roles-add <role(s)>`: Give yourself a role (or space-separated list of roles).");
			text.WriteLine("`@Irene -roles-remove <role(s)>`: Remove a role (or space-separated list of roles).");
			text.WriteLine("`@Irene -tags`: List the available tags.");
			text.WriteLine("`@Irene -tags <tag>`: Display the tag.");
			text.WriteLine("`@Irene -invite [erythro|leuko]`: Link the server invite link.");

			log.info("  Sending general help text.");
			text.Flush();
			_ = cmd.msg.RespondAsync(text.ToString());
		}
	}
}
