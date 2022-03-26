using System.IO;

using static Irene.Program;

namespace Irene.Commands;

class About {
	const string
		path_build   = @"config/commit.txt",
		path_version = @"config/tag.txt";

	public static string help() {
		StringWriter text = new ();
		text.WriteLine("Prints the most recent release version and build the bot is running.");
		return text.output();
	}

	public static void run(Command cmd) {
		StreamReader file;

		// Read in data.
		file = File.OpenText(path_build);
		string build = file.ReadLine() ?? "";
		if (build.Length > 7) {
			build = build[..7];
		}
		file.Close();

		file = File.OpenText(path_version);
		string version = file.ReadLine() ?? "";
		file.Close();

		// Respond with data.
		string output = $"**Irene {version}** build `{build}`";
		_ = cmd.msg.RespondAsync(output);
		log.info("Sending version information:");
		log.debug($"  {output}");
		log.endl();
	}
}
