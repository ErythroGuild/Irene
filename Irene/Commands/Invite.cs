using System.Collections.Generic;
using System.IO;

using static Irene.Const;
using static Irene.Program;

namespace Irene.Commands;

using id_ch = ChannelIDs;

class Invite : ICommands {
	readonly static List<string> token_erythro = new () {
		"erythro", "ery", "wow"
	};
	readonly static List<string> token_leuko = new () {
		"leuko", "ffxiv", "ff14"
	};

	const string invite_erythro = @"https://discord.gg/ADzEwNS";
	const string invite_leuko   = @"https://discord.gg/zhadQf59xq";

	public static string help() {
		StringWriter text = new ();

		text.WriteLine("`@Irene -inv erythro` fetches the server invite for this server,");
		text.WriteLine("`@Irene -inv leuko` fetches the server invite for the FFXIV sister server,");
		text.WriteLine("and `@Irene -inv` fetches both server invites.");
		text.WriteLine($"These invite links can also be found in {channels[id_ch.resources]}.");

		return text.ToString();
	}

	public static void run(Command cmd) {
		string arg = cmd.args.Trim().ToLower();

		StringWriter text = new ();
		if (token_erythro.Contains(arg)) {
			text.WriteLine(invite_erythro);
		} else if (token_leuko.Contains(arg)) {
			text.WriteLine(invite_leuko);
		} else {
			text.WriteLine("**Erythro** and **Leuko** server invites:");
			text.WriteLine(invite_erythro);
			text.WriteLine(invite_leuko);
		}

		_ = cmd.msg.RespondAsync(text.ToString());
	}
}
