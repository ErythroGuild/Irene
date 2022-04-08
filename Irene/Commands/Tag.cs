using System.Collections.Concurrent;

using Irene.Components;

namespace Irene.Commands;

class Tag: ICommand {
	private record class ModalData
		(bool IsUpdate, string OriginalTag);

	// Stripped tag -> tag (read from datafile).
	private static readonly ConcurrentDictionary<string, string> _tagCache;
	// Tag -> original tag values (before editing).
	private static readonly ConcurrentDictionary<string, ModalData> _modalData = new ();

	private const string
		_pathData = @"data/tags.txt",
		_pathBuffer = @"data/tags_buffer.txt";
	private static readonly object _lock = new ();
	private const string _delim = "=";

	private const string
		_commandView   = "view"  ,
		_commandList   = "list"  ,
		_commandSet    = "set"   ,
		_commandDelete = "delete";

	private const string
		_idTagName    = "tag_name",
		_idTagContent = "tag_content";

	// Force static initializer to run.
	public static void Init() { return; }
	static Tag() {
		// Make sure datafile exists.
		// This is useful for all methods, not just the sinitializer.
		Util.CreateIfMissing(_pathData, ref _lock);

		// Initialize tag cache.
		_tagCache = new ();
		lock (_lock) {
			using StreamReader data = File.OpenText(_pathData);
			while (!data.EndOfStream) {
				string line = data.ReadLine() ?? "";
				if (line == "")
					continue;
				string[] split = line.Split(_delim, 2);
				string tag = split[0];
				string tag_stripped = Strip(tag);
				_tagCache.TryAdd(tag_stripped, tag);
			}
		}
	}

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@"`/tags view <name> [share]` displays the named tag (optionally to everyone),",
			@"`/tags list` lists all available tags,",
			@":lock: `/tags set <name>` adds (or edits) the content of a named tag,",
			@":lock: `/tags delete <name>` removes the tag.",
			"Tag names are case-insensitive, and ignore spaces.",
			"Only officers can set or remove tags.",
			@"`/suggest tag` if you think one can be improved, or if you have an idea for a new one!",
		} ) };
	}

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"tag",
				"Display a previously-saved message.",
				options: new List<CommandOption> {
					new (
						_commandView,
						"View an existing tag.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> {
							new (
								"name",
								"The tag to view.",
								ApplicationCommandOptionType.String,
								required: true,
								autocomplete: true
							),
							new (
								"share",
								"Make the result visible to everyone.",
								ApplicationCommandOptionType.Boolean,
								required: false
							),
						}
					),
					new (
						_commandList,
						"List all available tags.",
						ApplicationCommandOptionType.SubCommand
					),
					new (
						_commandSet,
						"Add or edit an existing tag.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"name",
							"The name of the tag.",
							ApplicationCommandOptionType.String,
							required: true,
							autocomplete: true
						), }
					),
					new (
						_commandDelete,
						"Delete an existing tag.",
						ApplicationCommandOptionType.SubCommand,
						options: new List<CommandOption> { new (
							"name",
							"The name of the tag.",
							ApplicationCommandOptionType.String,
							required: true,
							autocomplete: true
						), }
					),
				},
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }

	public static List<AutoCompleteHandler> AutoComplete { get => new () {
		new ("tag", AutoCompleteAsync),
	}; }

	// Check permissions and dispatch subcommand.
	public static async Task RunAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string command = args[0].Name;

		// Check for permissions.
		switch (command) {
		case _commandSet:
		case _commandDelete:
			bool doContinue =
				await interaction.CheckAccessAsync(stopwatch, AccessLevel.Officer);
			if (!doContinue)
				return;
			break;
		}

		// Dispatch the correct subcommand.
		InteractionHandler subcommand = command switch {
			_commandView   => ViewAsync,
			_commandList   => List,
			_commandSet    => SetAsync,
			_commandDelete => DeleteAsync,
			_ => throw new ArgumentException("Unrecognized subcommand.", nameof(interaction)),
		};
		await subcommand(interaction, stopwatch);
		return;
	}

	public static async Task AutoCompleteAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();
		string arg = (string)args[0].Value;
		arg = Strip(arg);

		// Ignore results with delimiter.
		if (arg.Contains(_delim)) {
			await interaction.AutoCompleteResultsAsync(new ());
			return;
		}

		// Search through cached results.
		List<string> results = new ();
		List<string> tags = new (_tagCache.Keys);
		foreach (string tag in tags) {
			if (tag.Contains(arg))
				results.Add(_tagCache[tag]);
		}

		// Only return first 25 results.
		if (results.Count > 25)
			results = results.GetRange(0, 25);

		await interaction.AutoCompleteResultsAsync(results);
		return;
	}

	public static async Task ViewAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();
		string arg = (string)args[0].Value;
		string arg_stripped = Strip(arg);

		// The delimiter is not allowed in tag names.
		if (arg_stripped.Contains(_delim)) {
			Log.Information("  Requested tag name \"{Tag}\" contains delimiter.", arg);
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			string response =
				$"Due to technical limitations, tag names cannot contain `{_delim}`.";
			await interaction.RespondMessageAsync(response, true);
			Log.Information("  Response sent.");
			return;
		}

		// Respond with info if tag not found.
		if (!_tagCache.ContainsKey(arg_stripped)) {
			Log.Information("  Requested tag \"{Tag}\" does not exist.", arg);
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			string response =
				$"Tag `{arg}` does not exist.\n" +
				"Use `/tag list` to view available tags, or see `/help tag` for more info.\n" +
				"You can also ask an Officer to add a tag, or suggest one with `/suggest tag`.";
			await interaction.RespondMessageAsync(response, true);
			Log.Information("  Response sent.");
			return;
		}

		// Read tag name.
		string? content = ReadTag(arg_stripped);

		// Handle unsuccessful read.
		if (content is null) {
			Log.Warning("  Could not find tag \"{Tag}\", despite existence in cache.", arg);
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			string response =
				$"Tag `{arg}` was recently erased by someone else. :h_kerri_sad:";
			await interaction.RespondMessageAsync(response, true);
			Log.Information("  Response sent.");
			return;
		}

		// Respond.
		bool doShare = false;
		if (args.Count > 1)
			doShare = (bool)args[1].Value;
		Log.Debug("  Found tag \"{Tag}\".", arg);
		stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
		await interaction.RespondMessageAsync(content, !doShare);
		Log.Information("  Tag sent.");
	}

	public static async Task List(DiscordInteraction interaction, Stopwatch stopwatch) {
		// Since we only care about the keys, we can read directly from cache.
		List<string> tags = new (_tagCache.Values);

		// Exit early if no tags exist.
		if (tags.Count == 0) {
			Log.Information("  No tags currently saved.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			string response = "No tags currently saved.\n" +
				"Maybe ask an Officer to add a tag, or suggest one with `/suggest tag`?";
			await interaction.RespondMessageAsync(response, true);
			Log.Information("  Response sent.");
			return;
		}

		// Construct response.
		tags.Sort();
		Pages pages = new (tags, interaction.User);
		Log.Debug("  Sending tag list.");
		stopwatch.LogMsecDebug("    responded in {Time} msec.", false);
		await interaction.RespondMessageAsync(pages.first_page(), true);
		pages.msg = await interaction.GetOriginalResponseAsync();
		Log.Information("  Tag list sent.");
	}

	public static async Task SetAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();
		string arg = (string)args[0].Value;
		string arg_stripped = Strip(arg);

		// The delimiter is not allowed in tag names.
		if (arg_stripped.Contains(_delim)) {
			Log.Information("  Requested tag name \"{Tag}\" contains delimiter.", arg);
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			string response =
				$"Due to technical limitations, tag names cannot contain `{_delim}`.";
			await interaction.RespondMessageAsync(response, true);
			Log.Information("  Response sent.");
			return;
		}

		// Read in existing tag content.
		string content = "";
		bool isUpdate = false;
		if (_tagCache.ContainsKey(arg_stripped)) {
			isUpdate = true;
			_modalData.TryAdd(
				Modal.GetId(interaction),
				new ModalData(isUpdate, _tagCache[arg_stripped])
			);
			content = ReadTag(arg_stripped) ?? "";
		}
		string title = !isUpdate
			? "Create new tag"
			: "Edit tag";

		// Initialize modal components.
		List<TextInputComponent> components = new () {
			new TextInputComponent("Tag name", _idTagName, $"Tag name cannot contain `{_delim}`.", arg),
			new TextInputComponent("Content", _idTagContent, value: content, style: TextInputStyle.Paragraph),
		};

		static async Task set_tag(ModalSubmitEventArgs e) {
			Stopwatch stopwatch = Stopwatch.StartNew();
			Log.Information("Modal submission received (id: {Id}).", e.Interaction.Data.CustomId);

			// Initialize as much as possible before needing to read
			// from the datafile.
			SortedList<string, string> tags = new ();
			Dictionary<string, TextInputComponent> fields =
				e.Interaction.GetModalComponents();
			string data_name = fields[_idTagName].Value;
			string data_content = fields[_idTagContent].Value.Escape();

			lock (_lock) {
				// Read in current datafile.
				using (StreamReader data = File.OpenText(_pathData)) {
					while (!data.EndOfStream) {
						string line = data.ReadLine() ?? "";
						string[] split = line.Split(_delim, 2);
						if (split.Length == 2)
							tags.Add(split[0], split[1]);
					}
				}

				// Swap out updated tag (in cache too).
				string id = e.Interaction.Data.CustomId;
				if (_modalData.ContainsKey(id) && _modalData[id].IsUpdate) {
					string original_tag = _modalData[id].OriginalTag;
					tags.Remove(original_tag);
					_tagCache.TryRemove(original_tag, out _);
				}
				_modalData.TryRemove(id, out _);
				tags.TryAdd(data_name, data_content);
				_tagCache.TryAdd(Strip(data_name), data_name);

				// Write back data.
				WriteData(tags);
			}

			// Handle interaction.
			Log.Debug("  Responding to modal submission.");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			await e.Interaction.RespondMessageAsync("Updated tag successfully.", true);
			Log.Information("  Submission processed.");
			stopwatch.LogMsecDebug("  Modal submission processed in {Time} msec.");
		}

		// Submit modal.
		Log.Information("  Sending modal to set tag \"{Tag}\".", arg);
		stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
		await Modal.CreateAsync(interaction, title, components, set_tag);
		Log.Information("  Modal created.");
	}

	public static async Task DeleteAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs()[0].GetArgs();
		string arg = (string)args[0].Value;
		string arg_stripped = Strip(arg);

		// The delimiter is not allowed in tag names.
		if (arg_stripped.Contains(_delim)) {
			Log.Information("  Requested tag name \"{Tag}\" contains delimiter.", arg);
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			string response =
				$"Due to technical limitations, tag names cannot contain `{_delim}`.";
			await interaction.RespondMessageAsync(response, true);
			Log.Information("  Response sent.");
			return;
		}

		// Exit early if tag doesn't exist.
		if (!_tagCache.ContainsKey(arg_stripped)) {
			Log.Information("  \"{Tag}\" not found; no changes made.", arg);
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			string response =
				$"Could not find tag `{arg}`; no changes made.";
			await interaction.RespondMessageAsync(response, true);
			Log.Information("  Response sent.");
			return;
		}

		// Remove tag from datafile and cache.
		string tag_key = _tagCache[arg_stripped];
		SortedList<string, string> tags = new ();
		lock (_lock) {
			// Read in current datafile.
			using (StreamReader data = File.OpenText(_pathData)) {
				while (!data.EndOfStream) {
					string line = data.ReadLine() ?? "";
					string[] split = line.Split(_delim, 2);
					if (split.Length == 2)
						tags.Add(split[0], split[1]);
				}
			}

			// Remove tag (in cache too).
			tags.Remove(tag_key);
			_tagCache.TryRemove(arg_stripped, out _);

			// Write back data.
			WriteData(tags);
		}

		// Respond.
		Log.Information("  Sending response.");
		stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
		await interaction.RespondMessageAsync("Removed tag successfully.", true);
		Log.Information("  Deleted tag.");
	}

	// The standard procedure for stripping a key.
	private static string Strip(string key) =>
		key.ToLower().Replace(" ", "");

	// Return the content of the corresponding key.
	// Read access is locked.
	private static string? ReadTag(string key_stripped) {
		string? content = null;
		lock (_lock) {
			using StreamReader data = File.OpenText(_pathData);
			while (!data.EndOfStream) {
				string line = data.ReadLine() ?? "";
				string[] split = line.Split(_delim, 2);
				string key = Strip(split[0]);
				if (key == key_stripped) {
					content = split[1];
					break;
				}
			}
		}
		return content?.Unescape() ?? null;
	}

	// Write the contents of a SortedList to the datafile.
	// This is NOT locked and should be called from within a lock.
	private static void WriteData(SortedList<string, string> tags) {
		using (StreamWriter data = File.CreateText(_pathBuffer)) {
			foreach (string key in tags.Keys)
				data.WriteLine(key + _delim + tags[key]);
		}
		File.Replace(_pathBuffer, _pathData, null);
	}
}
