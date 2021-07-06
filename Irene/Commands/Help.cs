using System;
using System.Collections.Generic;
using System.IO;

using DSharpPlus.Entities;

using static Irene.Program;

namespace Irene.Commands {
	class Help : ICommands {
		// Customize pagination class for help pages.
		class HelpPages : Pages {
			protected override int list_page_size
				{ get { return 1; } }
			protected override TimeSpan timeout
				{ get { return TimeSpan.FromMinutes(30); } }

			public HelpPages(List<string> list, DiscordUser author) :
				base(list, author) { }
		}

		static readonly List<string> list_general = new ();
		static readonly List<string> list_officer = new ();

		const string
			m = "@Irene",
			t = "\u2003";

		// Force static initializer to run.
		public static void init() { return; }
		// Populate helptext lists with data.
		static Help() {
			init_general();
			init_officer();
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

			// Fetch the needed help messages depending on user's
			// access level.
			List<string> list_help = list_general;
			if (cmd.access >= Command.AccessLevel.Officer) {
				list_help.AddRange(list_officer);
			}

			// Construct the paginated help message.
			HelpPages pages = new (list_help, cmd.msg.Author);
			log.info("  Displaying help pages.");
			DiscordMessage msg =
				cmd.msg.RespondAsync(pages.first_page()).Result;
			pages.msg = msg;
		}

		// Populate `list_general`.
		static void init_general() {
			StringWriter s;

			s = new StringWriter();
			s.WriteLine("All command names are case-insensitive.");
			s.WriteLine($"Commands can be DM'd to Irene, but still need to start with `{m}`.");
			s.WriteLine("*`<required argument>`, `[optional argument]`, `[option A | option B]`*");
			s.WriteLine($"{t}If you need more help, ask, or shoot Ernie a message! :+1:");
			s.WriteLine("");
			s.WriteLine("**Help**");
			s.WriteLine($"`{m} -help`: Display this help text.");
			s.WriteLine($"`{m} -help <command>`: Display help for a specific command.");
			s.WriteLine($"{t}*aliases:* `-h`, `-?`");
			s.Flush();
			list_general.Add(s.ToString());

			s = new StringWriter();
			s.WriteLine("**Roles**");
			s.WriteLine($"`{m} -roles`: Set (or clear) your own roles.");
			s.WriteLine($"{t}*aliases:* `-r`");
			s.WriteLine($"`{m} -roles-info`: Display a more detailed description of each role.");
			s.WriteLine($"{t}*aliases:* `-rinfo`");
			s.WriteLine();
			s.WriteLine("**Tags**");
			s.WriteLine($"`{m} -tags <tag>`: Display the named tag.");
			s.WriteLine($"`{m} -tags`: List all available tags.");
			s.WriteLine($"{t}*aliases:* `-t`, `-tag`");
			s.Flush();
			list_general.Add(s.ToString());

			s = new StringWriter();
			s.WriteLine("**Reference**");
			s.WriteLine($"`{m} -cap <type>`: Display the current cap of the resource.");
			s.WriteLine($"{t}*aliases:* `-c`");
			s.WriteLine($"`{m} -classdiscord <class>`: Link the class' class discord.");
			s.WriteLine($"{t}*aliases:* `-cd`, `-classdiscords`, `-class-discord`");
			s.WriteLine($"`{m} -invite [erythro|leuko]`: Display the server invite link.");
			s.WriteLine($"{t}*aliases:* `-i`, `-inv`");
			s.Flush();
			list_general.Add(s.ToString());

			s = new StringWriter();
			s.WriteLine("**Miscellaneous**");
			s.WriteLine($"`{m} -roll [x] [y]`: Generates a random, positive integer.");
			s.WriteLine($"{t}*aliases:* `-dice`, `-random`, `-rand`");
			s.Flush();
			list_general.Add(s.ToString());
		}

		// Populate `list_officer`.
		static void init_officer() {
			StringWriter s;

			s = new StringWriter();
			s.Flush();
			list_officer.Add(s.ToString());

			s = new StringWriter();
			s.Flush();
			list_officer.Add(s.ToString());

			s = new StringWriter();
			s.Flush();
			list_officer.Add(s.ToString());
		}
	}
}
