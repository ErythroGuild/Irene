using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;

using DSharpPlus;
using DSharpPlus.Entities;

using static Irene.Program;

namespace Irene.Commands {
	using ButtonPressLambda = Emzi0767.Utilities.AsyncEventHandler<DiscordClient, DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs>;
	
	class Tags : ICommands {
		class ListHandler {
			public int page { get; private set; }
			public readonly int page_count;
			public readonly List<string> list;
			public readonly DiscordUser author;
			public DiscordMessage? msg;
			readonly Timer timer;
			readonly ButtonPressLambda handler;

			public ListHandler(List<string> list, DiscordUser author) {
				// Initialize simple variables.
				this.author = author;
				this.list = list;
				page = 0;
				double page_count_d = (double)list.Count / list_page_size;
				page_count = (int)Math.Round(Math.Ceiling(page_count_d));
				timer = new Timer(timeout.TotalMilliseconds);
				timer.AutoReset = false;

				handler = async (irene, e) => {
					// Ignore triggers from the wrong message.
					if (e.Message != msg) {
						return;
					}

					// Ignore people who aren't the original user.
					if (e.User != author) {
						await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
						return;
					}

					switch (e.Id) {
					case id_button_prev:
						page--;
						break;
					case id_button_next:
						page++;
						break;
					}
					page = Math.Max(page, 0);
					page = Math.Min(page, page_count);
					await e.Interaction.CreateResponseAsync(
						InteractionResponseType.UpdateMessage,
						new DiscordInteractionResponseBuilder()
						.WithContent(get_page(page, list_page_size, this.list))
						.AddComponents(buttons_nav_list(page, page_count))
					);
					timer.Stop();
					timer.Start();
				};

				// Configure timeout event listener.
				timer.Elapsed += async (s, e) => {
					irene.ComponentInteractionCreated -= handler;
					if (msg is not null) {
						await msg.ModifyAsync(
							new DiscordMessageBuilder()
							.WithContent(msg.Content)
							.AddComponents(buttons_nav_list(page, page_count, false))
						);
					}
					handlers_list.Remove(this);
				};

				// Actually attach handler.
				irene.ComponentInteractionCreated += handler;

				// Start timer.
				timer.Start();
			}
		}

		static readonly List<ListHandler> handlers_list = new ();
		static readonly Dictionary<string, string> escape_codes = new () {
			{ @"\n"    , "\n"     },
			{ @"\u2022", "\u2022" },
			{ @"\u25E6", "\u25E6" },
			{ @":emsp:", "\u2003" },
			{ @":ensp:", "\u2022" },
		};

		const int list_page_size = 8;
		static readonly TimeSpan timeout = TimeSpan.FromMinutes(10);

		const string
			path_data = @"data/tags.txt",
			path_buffer = @"data/tags_buffer.txt";
		const string delim = "=";
		const string
			id_button_prev = "list_prev",
			id_button_next = "list_next",
			id_button_page = "list_page";
		const string
			label_prev = "\u25B2",
			label_next = "\u25BC";

		public static string help() {
			StringWriter text = new ();

			text.WriteLine("`@Irene -tags <tag>` posts the message corresponding to the tag.");
			text.WriteLine("`@Irene -tags-add <tag>=<content>` adds the tag;");
			text.WriteLine("`@Irene -tags-update <tag>=<content>` updates the tag.");
			text.WriteLine("`@Irene -tags-remove <tag>` removes the tag.");
			text.WriteLine("All tags are case-insensitive.");
			text.WriteLine("Only officers can add, update, and remove tags.");
			text.WriteLine("If you'd like a tag added/updated/removed, message an officer.");

			text.Flush();
			return text.ToString();
		}

		public static void run(Command cmd) {
			// All tags are case-insensitive.
			string arg = cmd.args.Trim().ToLower();

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
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());
				return;
			}

			// Display tag.
			content = unescape(content);
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
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());
				return;
			} else {
				log.debug("  Tag list created.");
			}

			// Construct message and respond.
			List<string> tags_list = new (tags.Keys);
			ListHandler handler = new (tags_list, cmd.msg.Author);
			handlers_list.Add(handler);
			DiscordMessageBuilder msg =
				new DiscordMessageBuilder()
				.WithContent(get_page(handler.page, list_page_size, handler.list))
				.AddComponents(buttons_nav_list(handler.page, handler.page_count));
			handler.msg = cmd.msg.RespondAsync(msg).Result;
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
				text.Flush();
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
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());

				return;
			}

			// Parse the (pending) tag to add.
			string[] split = cmd.args.Split(delim, 2);
			string tag = split[0].Trim().ToLower();
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
					text.WriteLine("Use `@Irene -tags-update <tag>=<content>` to updating existing tags.");
					text.WriteLine("Also see: `@Irene -help tags`.");
					text.Flush();
					_ = cmd.msg.RespondAsync(text.ToString());

					data.Close();
					return;
				}
				tags.Add(line, null);
			}
			data.Close();

			// Add the tag.
			content = escape(content);
			tags.Add($"{tag}{delim}{content}", null);

			// Write the file (to a buffer first, then overwrite).
			File.WriteAllLines(path_buffer, tags.Keys);
			File.Replace(path_buffer, path_data, null);
			log.info($"  Successfully added new tag: {tag}");
			log.debug($"  {content}");
			_ = cmd.msg.RespondAsync($"New tag added: `{tag}`");
		}

		public static void update(Command cmd) {
			string arg = cmd.args.Trim();

			// If nothing is specified, redirect user to help.
			if (arg == "" || arg.StartsWith(delim)) {
				log.info("  Cannot update an empty tag.");

				StringWriter text = new ();
				text.WriteLine("A tag cannot be empty.");
				text.WriteLine("See `@Irene -help tags` for syntax help.");
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());

				return;
			}

			// Don't allow empty tags to be added.
			if (!arg.Contains(delim) || arg.EndsWith(delim)) {
				log.info("  Cannot update tag with empty content.");
				log.debug($"  {cmd.args}");

				StringWriter text = new ();
				text.WriteLine("Cannot update a tag with no content.");
				text.WriteLine("See `@Irene -help tags` for syntax help.");
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());

				return;
			}

			// Parse the (pending) tag to add.
			string[] split = cmd.args.Split(delim, 2);
			string tag = split[0].Trim().ToLower();
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
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());

				return;
			}

			// Add the tag.
			tags.Add($"{tag}{delim}{content}", null);

			// Write the file (to a buffer first, then overwrite).
			File.WriteAllLines(path_buffer, tags.Keys);
			File.Replace(path_buffer, path_data, null);
			log.info($"  Successfully updated tag: {arg}");
			log.debug($"  {content}");
			log.debug("  Original content:");
			log.debug($"{content_old}");
			_ = cmd.msg.RespondAsync($"Updated tag: `{tag}`");
		}

		public static void remove(Command cmd) {
			// All tags are case-insensitive.
			string arg = cmd.args.Trim().ToLower();

			// The delimiter string is not allowed in tag names.
			if (arg.Contains(delim)) {
				log.info("  Delimiter in tag name.");

				StringWriter text = new ();
				text.WriteLine($"Tag names cannot contain `{delim}`.");
				text.WriteLine("Did you mean to add or update a tag instead?");
				text.Flush();
				_ = cmd.msg.RespondAsync(text.ToString());

				return;
			}

			// If nothing is specified, redirect user to help.
			if (arg == "") {
				log.info("  No tag specified.");

				StringWriter text = new ();
				text.WriteLine("Specify a tag to remove.");
				text.WriteLine("See `@Irene -help tags` for syntax help.");
				text.Flush();
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
				text.Flush();
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
			text_respond.Flush();
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

		// Replace all recognized codepoints with their escape codes.
		static string escape(string str) {
			foreach (string escape_code in escape_codes.Keys) {
				string codepoint = escape_codes[escape_code];
				str = str.Replace(codepoint, escape_code);
			}
			return str;
		}

		// Replace all recognized escape codes with their codepoints.
		static string unescape(string str) {
			foreach (string escape_code in escape_codes.Keys) {
				string codepoint = escape_codes[escape_code];
				str = str.Replace(escape_code, codepoint);
			}
			return str;
		}

		// Return the paginated content of a list's given page.
		// Assumes all arguments are within bounds.
		static string get_page(int page, int page_size, List<string> list) {
			StringWriter text = new ();

			int i_start = page * page_size;
			for (int i=i_start; i<i_start+page_size && i<list.Count; i++) {
				text.WriteLine(list[i]);
			}

			text.Flush();
			return text.ToString();
		}

		// Returns the paginated button row for the list of tags.
		static DiscordComponent[] buttons_nav_list(
			int page,
			int total,
			bool is_enabled=true ) {
			return new DiscordComponent[] {
				new DiscordButtonComponent(
					ButtonStyle.Secondary,
					id_button_prev,
					label_prev,
					(page + 1 == 1) || !is_enabled
				),
				new DiscordButtonComponent(
					ButtonStyle.Secondary,
					id_button_page,
					$"{page + 1} / {total}",
					!is_enabled
				),
				new DiscordButtonComponent(
					ButtonStyle.Secondary,
					id_button_next,
					label_next,
					(page + 1 == total) || !is_enabled
				),
			};
		}
	}
}
