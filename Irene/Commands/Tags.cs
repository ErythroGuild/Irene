using System.Collections.Generic;
using System.IO;

using DSharpPlus.Entities;

using static Irene.Program;
using Irene.Components;

namespace Irene.Commands;

class Tags : ICommands {
	const string
		path_data = @"data/tags.txt",
		path_buffer = @"data/tags_buffer.txt";
	const string delim = "=";

	public static string help() {
		StringWriter text = new ();

		text.WriteLine("`@Irene -tags <tag>` posts the message corresponding to the tag.");
		text.WriteLine(":lock: `@Irene -tags-add <tag>=<content>` adds the tag;");
		text.WriteLine(":lock: `@Irene -tags-edit <tag>=<content>` edits the tag.");
		text.WriteLine(":lock: `@Irene -tags-remove <tag>` removes the tag.");
		text.WriteLine("All tags are case-insensitive and ignore spaces.");
		text.WriteLine("Only officers can add, edit, and remove tags.");
		text.WriteLine("If you'd like a tag added/edited/removed, message an officer.");

		return text.ToString();
	}

	public static void run(Command cmd) {
		// All tags are case-insensitive and ignore spaces.
		string arg = cmd.args.Trim().ToLower();
		arg = arg.Replace(" ", "");

		// If no tags are specified, assume the user wants to see
		// what tags are available.
		if (arg == "") {
			list(cmd);
			return;
		}

		// The delimiter string is not allowed in tag names.
		if (arg.Contains(delim)) {
			log.info("  Delimiter in tag name.");
			_ = cmd.msg.RespondAsync($"Tag names cannot contain `{delim}`.");
			return;
		}

		// Look up the tag.
		try_create_data();
		string? content = null;
		StreamReader data = File.OpenText(path_data);
		while (!data.EndOfStream) {
			string line = data.ReadLine() ?? "";
			if (line.StartsWith(arg)) {
				string[] split = line.Split(delim, 2);
				content = split[1];
				break;
			}
		}
		data.Close();

		// If tag not found:
		if (content is null) {
			log.info($"  Tag not found: {arg}");
			StringWriter text = new ();
			text.WriteLine($"Tag `{arg}` not found.");
			text.WriteLine("Ask an officer to add a tag, or use `@Irene -tags-list` to view available tags.");
			text.WriteLine("Also see: `@Irene -help tags`.");
			_ = cmd.msg.RespondAsync(text.ToString());
			return;
		}

		// Display tag.
		content = content.Unescape();
		log.debug($"  {content}");
		_ = cmd.msg.RespondAsync(content);
	}

	public static void list(Command cmd) {
		// Read in all tags.
		try_create_data();
		SortedList<string, string> tags = new ();
		StreamReader data = File.OpenText(path_data);
		while (!data.EndOfStream) {
			string line = data.ReadLine() ?? "";
			string[] split = line.Split(delim, 2);
			tags.Add(split[0], split[1]);
		}
		data.Close();

		// Exit early if no tags exist.
		if (tags.Count == 0) {
			log.info("  No tags currently saved.");
			StringWriter text = new ();
			text.WriteLine("No tags currently saved.");
			text.WriteLine("Maybe ask an officer to add a tag?");
			_ = cmd.msg.RespondAsync(text.ToString());
			return;
		} else {
			log.debug("  Tag list created.");
		}

		// Construct message and respond.
		List<string> tags_list = new (tags.Keys);
		Pages pages = new (tags_list, cmd.msg.Author);
		DiscordMessage msg =
			cmd.msg.RespondAsync(pages.first_page()).Result;
		pages.msg = msg;
		log.info("  Tag list sent.");
	}

	public static void add(Command cmd) {
		string arg = cmd.args.Trim();

		// If nothing is specified, redirect user to help.
		if (arg == "" || arg.StartsWith(delim)) {
			log.info("  Cannot add an empty tag.");

			StringWriter text = new ();
			text.WriteLine("Cannot add a tag with no content.");
			text.WriteLine("See `@Irene -help tags` for syntax help.");
			_ = cmd.msg.RespondAsync(text.ToString());

			return;
		}

		// Don't allow empty tags to be added.
		if (!arg.Contains(delim) || arg.EndsWith(delim)) {
			log.info("  Cannot add tag with empty content.");
			log.debug($"  {cmd.args}");

			StringWriter text = new ();
			text.WriteLine("Cannot add a tag with no content.");
			text.WriteLine("See `@Irene -help tags` for syntax help.");
			_ = cmd.msg.RespondAsync(text.ToString());

			return;
		}

		// Parse the (pending) tag to add.
		string[] split = cmd.args.Split(delim, 2);
		string tag = split[0].Trim().ToLower();
		tag = tag.Replace(" ", "");
		string content = split[1];

		// Read in the current tags.
		try_create_data();
		SortedList<string, string?> tags = new ();	// keys are ignored
		StreamReader data = File.OpenText(path_data);
		while (!data.EndOfStream) {
			string line = data.ReadLine() ?? "";
			// If the tag exists, redirect the user to `-tag-update`.
			if (line.StartsWith(tag + delim)) {
				log.info($"  Tag already exists for: {tag}");
				log.debug($"  {line}");

				StringWriter text = new ();
				text.WriteLine($"Tag `{tag}` already exists.");
				text.WriteLine("Use `@Irene -tags-edit <tag>=<content>` to edit existing tags.");
				text.WriteLine("Also see: `@Irene -help tags`.");
				_ = cmd.msg.RespondAsync(text.ToString());

				data.Close();
				return;
			}
			tags.Add(line, null);
		}
		data.Close();

		// Add the tag.
		content = content.Escape();
		tags.Add($"{tag}{delim}{content}", null);

		// Write the file (to a buffer first, then overwrite).
		File.WriteAllLines(path_buffer, tags.Keys);
		File.Replace(path_buffer, path_data, null);
		log.info($"  Successfully added new tag: {tag}");
		log.debug($"  {content}");
		_ = cmd.msg.RespondAsync($"New tag added: `{tag}`");
	}

	public static void edit(Command cmd) {
		string arg = cmd.args.Trim();

		// If nothing is specified, redirect user to help.
		if (arg == "" || arg.StartsWith(delim)) {
			log.info("  Cannot edit an empty tag.");

			StringWriter text = new ();
			text.WriteLine("A tag cannot be empty.");
			text.WriteLine("See `@Irene -help tags` for syntax help.");
			_ = cmd.msg.RespondAsync(text.ToString());

			return;
		}

		// Don't allow empty tags to be added.
		if (!arg.Contains(delim) || arg.EndsWith(delim)) {
			log.info("  Cannot edit tag with empty content.");
			log.debug($"  {cmd.args}");

			StringWriter text = new ();
			text.WriteLine("Cannot edit a tag with no content.");
			text.WriteLine("See `@Irene -help tags` for syntax help.");
			_ = cmd.msg.RespondAsync(text.ToString());

			return;
		}

		// Parse the (pending) tag to add.
		string[] split = cmd.args.Split(delim, 2);
		string tag = split[0].Trim().ToLower();
		tag = tag.Replace(" ", "");
		string content = split[1];

		// Read in the current tags.
		try_create_data();
		bool is_found = false;
		string content_old = "";
		SortedList<string, string?> tags = new ();  // keys are ignored
		StreamReader data = File.OpenText(path_data);
		while (!data.EndOfStream) {
			string line = data.ReadLine() ?? "";
			// If the tag exists, redirect the user to `-tag-update`.
			if (line.StartsWith(tag + delim)) {
				is_found = true;
				content_old = line;
				log.info($"  Tag found: {tag}");
				continue;
			}
			tags.Add(line, null);
		}
		data.Close();

		// Notify if tag was not found.
		if (!is_found) {
			log.info($"  Could not find tag: {arg}");

			StringWriter text = new ();
			text.WriteLine($"No tag `{arg}` exists yet.");
			text.WriteLine("Use `@Irene -tags-add <tag>=<content>` to add a new tag.");
			text.WriteLine("See also: `@Irene -help tags`.");
			_ = cmd.msg.RespondAsync(text.ToString());

			return;
		}

		// Add the tag.
		tags.Add($"{tag}{delim}{content}", null);

		// Write the file (to a buffer first, then overwrite).
		File.WriteAllLines(path_buffer, tags.Keys);
		File.Replace(path_buffer, path_data, null);
		log.info($"  Successfully edited tag: {arg}");
		log.debug($"  {content}");
		log.debug("  Original content:");
		log.debug($"{content_old}");
		_ = cmd.msg.RespondAsync($"Edited tag: `{tag}`");
	}

	public static void remove(Command cmd) {
		// All tags are case-insensitive and ignore spaces.
		string arg = cmd.args.Trim().ToLower();
		arg = arg.Replace(" ", "");

		// The delimiter string is not allowed in tag names.
		if (arg.Contains(delim)) {
			log.info("  Delimiter in tag name.");

			StringWriter text = new ();
			text.WriteLine($"Tag names cannot contain `{delim}`.");
			text.WriteLine("Did you mean to add or update a tag instead?");
			_ = cmd.msg.RespondAsync(text.ToString());

			return;
		}

		// If nothing is specified, redirect user to help.
		if (arg == "") {
			log.info("  No tag specified.");

			StringWriter text = new ();
			text.WriteLine("Specify a tag to remove.");
			text.WriteLine("See `@Irene -help tags` for syntax help.");
			_ = cmd.msg.RespondAsync(text.ToString());

			return;
		}

		// Read in the current tags.
		try_create_data();
		bool is_found = false;
		string content = "";
		SortedList<string, string?> tags = new ();  // keys are ignored
		StreamReader data = File.OpenText(path_data);
		while (!data.EndOfStream) {
			string line = data.ReadLine() ?? "";
			if (line.StartsWith(arg + delim)) {
				is_found = true;
				content = line;
				continue;
			}
			tags.Add(line, null);
		}
		data.Close();

		// Notify if tag was not found.
		if (!is_found) {
			log.info($"  Could not find tag: {arg}");

			StringWriter text = new ();
			text.WriteLine($"No tag `{arg}` exists.");
			text.WriteLine("You can add a new tag instead.");
			text.WriteLine("See also: `@Irene -help tags`.");
			_ = cmd.msg.RespondAsync(text.ToString());

			return;
		}

		// Write the file (to a buffer first, then overwrite).
		File.WriteAllLines(path_buffer, tags.Keys);
		File.Replace(path_buffer, path_data, null);
		log.info($"  Successfully removed tag: {arg}");
		log.debug($"  {content}");

		StringWriter text_respond = new ();
		text_respond.WriteLine($"Removed tag: `{arg}`");
		text_respond.WriteLine(content);
		_ = cmd.msg.RespondAsync(text_respond.ToString());
	}

	// Make sure the data file (and its directory) exists.
	static void try_create_data() {
		// Make sure directory exists.
		string? dir = new FileInfo(path_data).DirectoryName;
		if (dir is null) {
			log.error("  Tag data directory could not be created.");
			log.debug($"  Tag data path: {path_data}");
			throw new InvalidDataException("Tag data directory could not be created.");
		}
		Directory.CreateDirectory(dir);

		// Make sure file exists.
		if (!File.Exists(path_data)) {
			log.info("  Creating tag file.");
			File.Create(path_data).Close();
		}
	}
}
