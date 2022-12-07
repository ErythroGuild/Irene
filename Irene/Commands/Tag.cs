namespace Irene.Commands;

using Irene.Interactables;

class Tag : CommandHandler {
	public const string
		CommandTag  = "tag" ,
		CommandList = "list",
		CommandView = "view",
		CommandSet  = "set" ,
		CommandSetServer    = "set-server"   ,
		CommandRemove       = "remove"       ,
		CommandRemoveServer = "remove-server",
		ArgName  = "name" ,
		ArgShare = "share",
		ArgUser  = "user" ;

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}{Mention($"{CommandTag} {CommandList}")} lists all available tags,
		{RankIcon(AccessLevel.Guest)}{Mention($"{CommandTag} {CommandView}")} `<{ArgName}> <{ArgShare}>` displays a tag.
		{_t}Tag names are *not* case-sensitive, and ignore spaces.
		{RankIcon(AccessLevel.Guest)}{Mention($"{CommandTag} {CommandSet}")} `<{ArgName}>` sets the contents of a personal tag,
		{RankIcon(AccessLevel.Officer)}{Mention($"{CommandTag} {CommandSetServer}")} `<{ArgName}>` sets the contents of a server tag.
		{_t}Tags you create are only usable by you.
		{_t}You can /suggest new server tags, or edits to current ones!
		{RankIcon(AccessLevel.Guest)}{Mention($"{CommandTag} {CommandRemove}")} `<{ArgName}> [{ArgUser}]` removes a tag.
		{_t}Only officers can remove personal tags (with `[{ArgUser}]`).
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandTag,
			"Save and display text snippets."
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				AccessLevel.Guest,
				new (
					CommandList,
					"List all available tags.",
					ArgType.SubCommand
				),
				new (ListTagsAsync)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandView,
					"Display an existing tag.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							ArgName,
							"The name of the tag.",
							ArgType.String,
							required: true,
							autocomplete: true
						),
						new (
							ArgShare,
							"Whether to show the tag publicly.",
							ArgType.Boolean,
							required: false
						)
					}
				),
				new (Placeholder)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandSet,
					"Add or edit a personal tag.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> { new (
						ArgName,
						"The name of the tag.",
						ArgType.String,
						required: true,
						autocomplete: true
					) }
				),
				new (Placeholder)
			),
			new (
				AccessLevel.Officer,
				new (
					CommandSetServer,
					"Add or edit a server tag.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> { new (
						ArgName,
						"The name of the tag.",
						ArgType.String,
						required: true,
						autocomplete: true
					) }
				),
				new (Placeholder)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandRemove,
					"Delete a tag.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							ArgName,
							"The name of the tag.",
							ArgType.String,
							required: true,
							autocomplete: true
						),
						new (
							ArgUser,
							"The user to remove the tag from.",
							ArgType.User,
							required: false
						),
					}
				),
				new (Placeholder)
			),
		}
	);

	public async Task Placeholder(Interaction interaction, ParsedArgs args) { }

	public async Task ListTagsAsync(Interaction interaction, ParsedArgs args) {
	}


	//private record class ModalData
	//	(bool IsUpdate, string OriginalTag);

	//// Confirmation messages, indexed by the ID of the user who is
	//// accessing them.
	//private static readonly ConcurrentDictionary<ulong, Confirm> _confirms = new ();

	//// Stripped tag -> tag (read from datafile).
	//private static readonly ConcurrentDictionary<string, string> _tagCache;
	//// Tag -> original tag values (before editing).
	//private static readonly ConcurrentDictionary<string, ModalData> _modalData = new ();

	//private const string
	//	_pathData = @"data/tags.txt",
	//	_pathBuffer = @"data/tags_buffer.txt";
	//private static readonly object _lock = new ();
	//private const string _delim = "=";

	//private const string
	//	_idTagName    = "tag_name",
	//	_idTagContent = "tag_content";

	//static Tag() {
	//	// Make sure datafile exists.
	//	// This is useful for all methods, not just the initializer.
	//	Util.CreateIfMissing(_pathData);

	//	// Initialize tag cache.
	//	_tagCache = new ();
	//	lock (_lock) {
	//		using StreamReader data = File.OpenText(_pathData);
	//		while (!data.EndOfStream) {
	//			string line = data.ReadLine() ?? "";
	//			if (line == "")
	//				continue;
	//			string[] split = line.Split(_delim, 2);
	//			string tag = split[0];
	//			string tag_stripped = Strip(tag);
	//			_tagCache.TryAdd(tag_stripped, tag);
	//		}
	//	}
	//}

	//public static async Task AutoCompleteAsync(TimedInteraction interaction) {
	//	List<DiscordInteractionDataOption> args =
	//		interaction.GetArgs()[0].GetArgs();
	//	string arg = (string)args[0].Value;
	//	arg = Strip(arg);

	//	// Ignore results with delimiter.
	//	if (arg.Contains(_delim)) {
	//		await interaction
	//			.AutoCompleteResultsAsync(new List<string>());
	//		return;
	//	}

	//	// Search through cached results.
	//	List<string> results = new ();
	//	List<string> tags = new (_tagCache.Keys);
	//	foreach (string tag in tags) {
	//		if (tag.Contains(arg))
	//			results.Add(_tagCache[tag]);
	//	}

	//	// Only return first 25 results.
	//	if (results.Count > 25)
	//		results = results.GetRange(0, 25);

	//	await interaction.AutoCompleteResultsAsync(results);
	//}

	//private static async Task ViewAsync(DeferrerHandler handler) {
	//	List<DiscordInteractionDataOption> args =
	//		handler.GetArgs()[0].GetArgs();
	//	string arg = (string)args[0].Value;
	//	string arg_stripped = Strip(arg);

	//	// The delimiter is not allowed in tag names.
	//	if (arg_stripped.Contains(_delim)) {
	//		if (handler.IsDeferrer) {
	//			await Command.DeferAsync(handler, true);
	//			return;
	//		}
	//		string response =
	//			$"Due to technical limitations, tag names cannot contain `{_delim}`.";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response,
	//			"Requested tag name with delimiter.",
	//			LogLevel.Information,
	//			"Tag name \"{Name}\" is invalid. (contains delimiter \"{Delimiter}\")".AsLazy(),
	//			arg, _delim
	//		);
	//		return;
	//	}

	//	// Respond with info if tag not found.
	//	if (!_tagCache.ContainsKey(arg_stripped)) {
	//		if (handler.IsDeferrer) {
	//			await Command.DeferAsync(handler, true);
	//			return;
	//		}
	//		string response =
	//			$"Tag `{arg}` does not exist.\n" +
	//			"Use `/tag list` to view available tags, or see `/help tag` for more info.\n" +
	//			"You can also ask an Officer to add a tag, or suggest one with `/suggest tag`.";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response,
	//			"Requested tag does not exist.",
	//			LogLevel.Information,
	//			"No tag exists with the tag name \"{Name}\".".AsLazy(),
	//			arg
	//		);
	//		return;
	//	}
		
	//	// Determine ephemeral-ness.
	//	bool doShare = false;
	//	if (args.Count > 1)
	//		doShare = (bool)args[1].Value;
	//	if (handler.IsDeferrer) {
	//		await Command.DeferAsync(handler, !doShare);
	//		return;
	//	}

	//	// Read tag name.
	//	string? content = ReadTag(arg_stripped);

	//	// Handle unsuccessful read.
	//	if (content is null) {
	//		string response =
	//			$"Tag `{arg}` was just now erased by someone else. :h_kerri_sad:";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response,
	//			"Unsuccessful read of tag existing in cache.",
	//			LogLevel.Warning,
	//			"Error occurred while reading contents of tag \"{Name}\".".AsLazy(),
	//			arg
	//		);
	//		return;
	//	}

	//	// Respond.
	//	await Command.SubmitResponseAsync(
	//		handler.Interaction,
	//		content,
	//		"Tag found. Sending tag content.",
	//		LogLevel.Debug,
	//		new Lazy<string>(() => {
	//			string preview = content.ElideFirstLine();
	//			return $"Tag \"{arg}\" sent: {preview}";
	//		})
	//	);
	//}

	//private static async Task ListAsync(DeferrerHandler handler) {
	//	// Always ephemeral.
	//	if (handler.IsDeferrer) {
	//		await Command.DeferAsync(handler, true);
	//		return;
	//	}

	//	// Since we only care about the keys, we can read directly from cache.
	//	List<string> tags = new (_tagCache.Values);
	//	tags.Sort();

	//	// Exit early if no tags exist.
	//	if (tags.Count == 0) {
	//		string response_error = "No tags currently saved.\n" +
	//			"Maybe ask an Officer to add a tag, or suggest one with `/suggest tag`?";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response_error,
	//			"No tags currently defined.",
	//			LogLevel.Debug,
	//			"Response sent.".AsLazy()
	//		);
	//		return;
	//	}

	//	//// Else, send general help.
	//	//MessagePromise message_promise = new ();
	//	//DiscordMessageBuilder response = Pages.Create(
	//	//	handler.Interaction.Interaction.User,
	//	//	message_promise.Task,
	//	//	tags
	//	//);
	//	//DiscordMessage message = await Command.SubmitResponseAsync(
	//	//	handler.Interaction,
	//	//	response,
	//	//	"Sending list of all tags.",
	//	//	LogLevel.Debug,
	//	//	"List sent.".AsLazy()
	//	//);
	//	//message_promise.SetResult(message);
	//}

	//private static async Task SetAsync(DeferrerHandler handler) {
	//	List<DiscordInteractionDataOption> args =
	//		handler.GetArgs()[0].GetArgs();
	//	string arg = (string)args[0].Value;
	//	string arg_stripped = Strip(arg);

	//	// The delimiter is not allowed in tag names.
	//	if (arg_stripped.Contains(_delim)) {
	//		if (handler.IsDeferrer) {
	//			await Command.DeferAsync(handler, true);
	//			return;
	//		}
	//		string response =
	//			$"Due to technical limitations, tag names cannot contain `{_delim}`.";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response,
	//			"Requested tag name with delimiter.",
	//			LogLevel.Information,
	//			"Tag name \"{Name}\" is invalid. (contains delimiter \"{Delimiter}\")".AsLazy(),
	//			arg, _delim
	//		);
	//		return;
	//	}

	//	// Setting a modal cannot have a prior deferral.
	//	if (handler.IsDeferrer) {
	//		await Command.DeferNoOp();
	//		return;
	//	}

	//	// Read in existing tag content.
	//	string content = "";
	//	bool isUpdate = false;
	//	if (_tagCache.ContainsKey(arg_stripped)) {
	//		isUpdate = true;
	//		_modalData.TryAdd(
	//			Modal.GetId(handler.Interaction.Interaction),
	//			new ModalData(isUpdate, _tagCache[arg_stripped])
	//		);
	//		content = ReadTag(arg_stripped) ?? "";
	//	}
	//	string title = !isUpdate
	//		? "Create new tag"
	//		: "Edit tag";

	//	// Initialize modal components.
	//	List<TextInputComponent> components = new () {
	//		new TextInputComponent("Tag name", _idTagName, $"Tag name cannot contain `{_delim}`.", arg),
	//		new TextInputComponent("Content", _idTagContent, value: content, style: TextInputStyle.Paragraph),
	//	};

	//	async Task SetTag(ModalSubmitEventArgs e) {
	//		Stopwatch stopwatch = Stopwatch.StartNew();
	//		Log.Information("Modal submission received (id: {Id}).", e.Interaction.Data.CustomId);

	//		// Initialize as much as possible before needing to read
	//		// from the datafile.
	//		SortedList<string, string> tags = new ();
	//		Dictionary<string, TextInputComponent> fields =
	//			e.Interaction.GetModalComponents();
	//		string data_name = fields[_idTagName].Value;
	//		string data_content = fields[_idTagContent].Value.Escape();

	//		lock (_lock) {
	//			// Read in current datafile.
	//			using (StreamReader data = File.OpenText(_pathData)) {
	//				while (!data.EndOfStream) {
	//					string line = data.ReadLine() ?? "";
	//					string[] split = line.Split(_delim, 2);
	//					if (split.Length == 2)
	//						tags.Add(split[0], split[1]);
	//				}
	//			}

	//			// Swap out updated tag (in cache too).
	//			string id = e.Interaction.Data.CustomId;
	//			if (_modalData.ContainsKey(id) && _modalData[id].IsUpdate) {
	//				string original_tag = _modalData[id].OriginalTag;
	//				tags.Remove(original_tag);
	//				_tagCache.TryRemove(original_tag, out _);
	//			}
	//			_modalData.TryRemove(id, out _);
	//			tags.TryAdd(data_name, data_content);
	//			_tagCache.TryAdd(Strip(data_name), data_name);

	//			// Write back data.
	//			WriteData(tags);
	//		}

	//		// Handle interaction.
	//		await Command.SubmitModalAsync(
	//			new TimedInteraction(e.Interaction, stopwatch),
	//			$"Tag `{arg}` updated successfully.", true,
	//			"Updated tag successfully.",
	//			LogLevel.Debug,
	//			new Lazy<string>(() => {
	//				string preview = data_content.ElideFirstLine();
	//				return $"Tag \"{arg}\" updated: {preview}";
	//			})
	//		);
	//	}

	//	// Submit modal.
	//	await Command.SubmitResponseAsync(
	//		handler.Interaction,
	//		new Func<Task<DiscordMessage?>>(async () => {
	//			await Modal.RespondAsync(
	//				handler.Interaction.Interaction,
	//				title,
	//				components,
	//				SetTag
	//			);
	//			return null;
	//		})(),
	//		"Creating modal to set tag.",
	//		LogLevel.Debug,
	//		"Modal created. (editing tag \"{Name}\")".AsLazy(),
	//		arg
	//	);
	//}

	//private static async Task DeleteAsync(DeferrerHandler handler) {
	//	List<DiscordInteractionDataOption> args =
	//		handler.GetArgs()[0].GetArgs();
	//	string arg = (string)args[0].Value;
	//	string arg_stripped = Strip(arg);

	//	// The delimiter is not allowed in tag names.
	//	if (arg_stripped.Contains(_delim)) {
	//		if (handler.IsDeferrer) {
	//			await Command.DeferAsync(handler, true);
	//			return;
	//		}
	//		string response =
	//			$"Due to technical limitations, tag names cannot contain `{_delim}`.";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response,
	//			"Requested tag name with delimiter.",
	//			LogLevel.Information,
	//			"Tag name \"{Name}\" is invalid. (contains delimiter \"{Delimiter}\")".AsLazy(),
	//			arg, _delim
	//		);
	//		return;
	//	}

	//	// Exit early if tag doesn't exist.
	//	if (!_tagCache.ContainsKey(arg_stripped)) {
	//		if (handler.IsDeferrer) {
	//			await Command.DeferAsync(handler, true);
	//			return;
	//		}
	//		string response =
	//			$"Could not find tag `{arg}`; no changes made.";
	//		await Command.SubmitResponseAsync(
	//			handler.Interaction,
	//			response,
	//			"Tag not found.",
	//			LogLevel.Information,
	//			"No tags found matching the name \"{Name}\".".AsLazy(),
	//			arg
	//		);
	//		return;
	//	}

	//	// Deferrer is non-ephemeral for the rest.
	//	if (handler.IsDeferrer) {
	//		await Command.DeferAsync(handler, false);
	//		return;
	//	}

	//	// Create and send confirmation message.
	//	MessagePromise message_promise = new ();
	//	string tag_key = _tagCache[arg_stripped];
	//	Confirm confirm = Confirm.Create(
	//		handler.Interaction.Interaction,
	//		DeleteTag,
	//		message_promise.Task,
	//		$"Are you sure you want to delete the tag `{tag_key}`?",
	//		$"Tag `{tag_key}` successfully deleted.",
	//		$"Tag `{tag_key}` was not deleted.",
	//		"Delete", "Cancel"
	//	);

	//	// Disable any confirms already in-flight.
	//	ulong user_id = handler.Interaction.Interaction.User.Id;
	//	if (_confirms.ContainsKey(user_id)) {
	//		await _confirms[user_id].Discard();
	//		_confirms.TryRemove(user_id, out _);
	//	}
	//	_confirms.TryAdd(user_id, confirm);

	//	// Tag deletion callback.
	//	Task DeleteTag(bool doContinue, ComponentInteractionCreateEventArgs e) {
	//		// Remove confirm from table.
	//		_confirms.TryRemove(e.User.Id, out _);

	//		if (!doContinue) {
	//			Log.Debug("  Tag \"{Name}\" unmodified (deletion request canceled).", tag_key);
	//			return Task.CompletedTask;
	//		}

	//		// Remove tag from datafile and cache.
	//		SortedList<string, string> tags = new ();
	//		lock (_lock) {
	//			// Read in current datafile.
	//			using (StreamReader data = File.OpenText(_pathData)) {
	//				while (!data.EndOfStream) {
	//					string line = data.ReadLine() ?? "";
	//					string[] split = line.Split(_delim, 2);
	//					if (split.Length == 2)
	//						tags.Add(split[0], split[1]);
	//				}
	//			}

	//			// Remove tag (in cache too).
	//			tags.Remove(tag_key);
	//			_tagCache.TryRemove(arg_stripped, out _);

	//			// Write back data.
	//			WriteData(tags);
	//		}

	//		Log.Information("  Tag \"{Name}\" deleted (deletion request confirmed).", tag_key);
	//		return Task.CompletedTask;
	//	}

	//	// Respond.
	//	DiscordMessage message = await Command.SubmitResponseAsync(
	//		handler.Interaction,
	//		confirm.WebhookBuilder,
	//		"Tag deletion requested. Awaiting confirmation.",
	//		LogLevel.Information,
	//		"Deletion confirmation requested for: \"{Name}\"".AsLazy(),
	//		tag_key
	//	);
	//	message_promise.SetResult(message);
	//}

	//// The standard procedure for stripping a key.
	//private static string Strip(string key) =>
	//	key.ToLower().Replace(" ", "");

	//// Return the content of the corresponding key.
	//// Read access is locked.
	//private static string? ReadTag(string key_stripped) {
	//	string? content = null;
	//	lock (_lock) {
	//		using StreamReader data = File.OpenText(_pathData);
	//		while (!data.EndOfStream) {
	//			string line = data.ReadLine() ?? "";
	//			string[] split = line.Split(_delim, 2);
	//			string key = Strip(split[0]);
	//			if (key == key_stripped) {
	//				content = split[1];
	//				break;
	//			}
	//		}
	//	}
	//	return content?.Unescape() ?? null;
	//}

	//// Write the contents of a SortedList to the datafile.
	//// This is NOT locked and should be called from within a lock.
	//private static void WriteData(SortedList<string, string> tags) {
	//	using (StreamWriter data = File.CreateText(_pathBuffer)) {
	//		foreach (string key in tags.Keys)
	//			data.WriteLine(key + _delim + tags[key]);
	//	}
	//	File.Replace(_pathBuffer, _pathData, null);
	//}
}
