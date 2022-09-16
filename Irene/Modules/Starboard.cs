namespace Irene.Modules;

static class Starboard {
	// The actual work of pinning messages to the starboard should
	// happen in a well-ordered fashion (ensured by this queue).
	private static readonly ConcurrentQueue<DiscordMessage> _workQueue = new ();
	private static Task _workTask = Task.CompletedTask;

	// Cache-related variables.
	// The cache itself is indexed by message ID of the starboarded
	// message, and contains the messages in the starboard channel.
	private const int _cacheSize = 20;
	private static readonly TimeSpan _cacheDuration = TimeSpan.FromDays(90);
	private static readonly ConcurrentQueue<ulong> _cacheQueue = new ();
	private static readonly ConcurrentDictionary<ulong, DiscordMessage?> _cache = new ();

	// Embed-related variables.
	private static readonly ReadOnlyDictionary<ulong, int> _channelThresholds =
		new (new ConcurrentDictionary<ulong, int>() {
			[id_ch.general ] = 4,
			[id_ch.sharing ] = 4,
			[id_ch.spoilers] = 4,
			[id_ch.memes   ] = 6,
			[id_ch.tts     ] = 4,
			[id_ch.bots    ] = 4,
			[id_ch.news    ] = 4,
		});
	private static readonly ReadOnlyCollection<ulong> _spoilerChannels =
		new (new List<ulong> () {
			id_ch.spoilers,
		});
	private static readonly ReadOnlyDictionary<ulong, DiscordColor> _channelColors =
		new (new ConcurrentDictionary<ulong, DiscordColor>() {
			[id_ch.general ] = new ("#FFCEC9"),
			[id_ch.sharing ] = new ("#DA4331"),
			[id_ch.spoilers] = new ("#FFCEC9"),
			[id_ch.memes   ] = new ("#3E0600"),
			[id_ch.tts     ] = new ("#FFCEC9"),
			[id_ch.bots    ] = new ("#3E0600"),
			[id_ch.news    ] = new ("#FFCEC9"),
		});

	private const int _capEmojiDisplay = 4;
	private const int _capCharsPreview = 420; // hi Ambi
	private const string _footerPrefix = "id: ";

	// Blacklist-related variables.
	private static readonly object _lock = new ();
	private const string _pathBlacklist = @"data/starboard-blocked.txt";

	public static void Init() { }
	static Starboard() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		Util.CreateIfMissing(_pathBlacklist, _lock);

		Client.MessageReactionAdded += (irene, e) => {
			_ = Task.Run(async () => {
				await AwaitGuildInitAsync();

				// Fetch latest data.
				// (Sometimes the cached data is missing.)
				DiscordChannel channel = await
					Client.GetChannelAsync(e.Message.ChannelId);
				DiscordMessage message = await
					channel.GetMessageAsync(e.Message.Id);
				DiscordUser author = message.Author;

				// Check if a pin should be made.
				if (e.User == Client.CurrentUser || e.User == author)
					return;
				bool? doPin = await DoPin(message);
				if (doPin is null || !doPin.Value)
					return;

				// Add pin if passed check.
				// Since the task for adding a pin handles queueing,
				// there's no need to await it here.
				_ = UpdatePinAsync(message);
			});
			return Task.CompletedTask;
		};

		Log.Information("  Initialized module: Starboard");
		Log.Debug($"    Registered react event handler.");
		Log.Debug($"    Initialized blacklist.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public static async Task<bool?> DoPin(DiscordMessage message) {
		await AwaitGuildInitAsync();

		// Fetch latest data.
		// (Sometimes the cached data is missing.)
		DiscordChannel channel = await
			Client.GetChannelAsync(message.ChannelId);
		message = await
			channel.GetMessageAsync(message.Id);
		DiscordUser author = message.Author;

		// Return false if message isn't in a tracked channel.
		if (!_channelThresholds.ContainsKey(channel.Id))
			return false;

		// Populate set of reacting users.
		// Don't include bots or the author themselves.
		HashSet<DiscordUser> users = new ();
		foreach (DiscordReaction reaction in message.Reactions) {
			// By default this only fetches the first 25,
			// but we only are checking for having enough to
			// pass the threshold, not the actual count.
			HashSet<DiscordUser> users_i = new (await
				message.GetReactionsAsync(reaction.Emoji)
			);
			users.UnionWith(users_i);
		}
		users.RemoveWhere(user =>
			user.IsBot || user == author
		);

		// Add pin if enough people reacted.
		return users.Count >= _channelThresholds[channel.Id];
	}
	
	// Return whether or not the message has been blacklisted.
	public static bool IsBlacklisted(DiscordMessage message) {
		List<string> blacklist = new ();
		lock (_lock) {
			blacklist.AddRange(File.ReadAllLines(_pathBlacklist));
		}
		blacklist.RemoveAll(line => line == "");

		return blacklist.Contains(message.Id.ToString());
	}

	// Toggle a discord message's blacklist status.
	// Returns true if blacklist was updated (a message ID was
	// blocked or unblocked).
	public static bool SetBlacklist(DiscordMessage message, bool doBlock=true) {
		ulong messageId = message.Id;

		// Read in current blacklist.
		List<string> blacklist = new ();
		lock (_lock) {
			blacklist.AddRange(File.ReadAllLines(_pathBlacklist));
		}
		blacklist.RemoveAll(line => line == "");
		
		// Add/remove message from blacklist.
		bool didModify = false;
		string id = messageId.ToString();
		if (!doBlock && blacklist.Contains(id)) {
			blacklist.RemoveAll(line => line == id);
			didModify = true;
		}
		if (doBlock && !blacklist.Contains(id)) {
			blacklist.Add(id);
			didModify = true;
		}

		// Sort blacklist, sanitize list.
		HashSet<string> blacklist_set = new (blacklist);
		blacklist = new (blacklist_set);
		blacklist.Sort();

		// Write out new blacklist.
		lock (_lock) {
			File.WriteAllLines(_pathBlacklist.Temp(), blacklist);
			File.Delete(_pathBlacklist);
			File.Move(_pathBlacklist.Temp(), _pathBlacklist);
		}

		return didModify;
	}

	// Creates a new pin in the starboard channel if one doesn't
	// already exist for this message. Otherwise, updates the react
	// emoji tallies.
	public static async Task UpdatePinAsync(DiscordMessage message) {
		await AwaitGuildInitAsync();

		_workQueue.Enqueue(message);
		// Let current work task finish first.
		await _workTask;
		_workTask = Task.Run(async () => {
			// Fetching the blacklist separately rather than using
			// the built-in function is more efficient.
			// The entire file doesn't need to be read every time.
			List<string> blacklist = new ();
			lock (_lock) {
				blacklist.AddRange(File.ReadAllLines(_pathBlacklist));
			}
			blacklist.RemoveAll(line => line == "");

			while (!_workQueue.IsEmpty) {
				// Work through items in order.
				_workQueue.TryDequeue(out DiscordMessage? message);
				if (message is null)
					continue;

				// Skip blacklisted items.
				ulong id = message.Id;
				if (blacklist.Contains(id.ToString()))
					continue;

				// Determine if an update is needed.
				// Construct the (updated) embed data.
				DiscordMessage? pin = await FetchPinAsync(message);
				DiscordMember? author = await message.Author.ToMember();
				bool is_update = pin is not null;
				DiscordEmbed embed = AsEmbed(message);

				if (pin is not null) {
					await pin.ModifyAsync(embed);
				} else {
					string author_name =
						author?.Nickname ?? message.Author.Tag();
					Log.Information("Adding new post to starboard.");
					Log.Debug($"  {author_name}, in #{message.Channel.Name}");

					DiscordChannel channel = Channels[id_ch.starboard];
					pin = await channel.SendMessageAsync(embed);
				}

				// Update cache.
				if (!_cache.ContainsKey(id)) {
					_cacheQueue.Enqueue(id);
					_cache.TryAdd(id, pin);
					if (_cacheQueue.Count > _cacheSize) {
						_cacheQueue.TryDequeue(out ulong id_discard);
						_cache.TryRemove(id_discard, out _);
					}
				}

				// Send congrats message (if new post).
				if (!is_update && author is not null) {
					string text = new List<string>() {
						"Congrats! :tada:",
						$"Your post in {message.Channel.Mention} was extra-popular," +
							$" and has been included in {Channels[id_ch.starboard].Mention}.",
						$":champagne_glass: {Emojis[id_e.eryLove]}",
					}.ToLines();
					await author.SendMessageAsync(text);
					Log.Information("  Notified original post author.");
				}
			}
		});
		await _workTask;
	}

	// Removes the pin in the starboard channel for the message,
	// if one exists.
	public static async Task RemovePinAsync(DiscordMessage message) {
		await AwaitGuildInitAsync();

		// Make sure the work task isn't running.
		await _workTask;
		_workTask = Task.Run(async () => {
			DiscordMessage? pin = await FetchPinAsync(message);

			// Return early if no pin existed in the first place.
			if (pin is null)
				return;

			// Invalidate cache and remove cache.
			ulong id = message.Id;
			if (_cache.ContainsKey(id))
				_cache[id] = null;
			await pin.Channel.DeleteMessageAsync(pin);

		});
		await _workTask;
	}

	// Searches through the starboard channel for an existing pin
	// for that message.
	// Returns that message if it exists, or null if it doesn't.
	public static async Task<DiscordMessage?> FetchPinAsync(DiscordMessage message) {
		await AwaitGuildInitAsync();

		DiscordChannel channel = Channels[id_ch.starboard];
		ulong id = message.Id;

		// Check cache for existing pinned message.
		if (_cache.ContainsKey(id) && _cache[id] is not null)
			return _cache[id];

		// Fetch recent messages.
		IReadOnlyList<DiscordMessage> messages =
			await channel.GetMessagesAsync();
		if (messages.Count == 0)
			return null;

		// Set the time at which to give up searching.
		DateTimeOffset time_untracked =
			DateTimeOffset.UtcNow - _cacheDuration;

		// Search through existing messages.
		while (messages.Count > 0) {
			foreach (DiscordMessage message_i in messages) {
				if (message_i.Timestamp < time_untracked)
					return null;

				IReadOnlyList<DiscordEmbed> embeds =
					message_i.Embeds;
				if (embeds.Count == 0)
					continue;
				DiscordEmbed embed = embeds[0];
				if (embed.Footer is null)
					continue;
				string? footer_text = embed.Footer.Text;
				if (footer_text is null)
					continue;

				if (footer_text.StartsWith(_footerPrefix)) {
					string id_i =
						footer_text.Replace(_footerPrefix, "");
					if (id_i == id.ToString())
						return message_i;
				}
			}
			ulong id_last = messages[^1].Id;
			messages = await
				channel.GetMessagesBeforeAsync(id_last);
		}

		// Could not find matching message.
		return null;
	}

	// Returns the message *embedded* into an embed.
	private static DiscordEmbed AsEmbed(DiscordMessage message) {
		// Fetch author name.
		string author_name;
		if (message.Channel is not null && !message.Channel.IsPrivate) {
			// Check for webhook.
			if (message.Author.IsBot && message.Author.Discriminator == "0000") {
				author_name = message.Author.Username;
			} else {
				DiscordMember? author =
					message.Author as DiscordMember;
				author_name = author?.DisplayName ??
					message.Author.Tag();
			}
		} else {
			author_name = message.Author.Tag();
		}

		// Get content strings.
		string emoji_list = PrintEmojiList(message.Reactions);
		string? content = GetSummary(message);
		if (content is not null) {
			if (DoSpoiler(message))
				content = content.Spoiler();
			content += "\n" + emoji_list;
		} else {
			content = emoji_list;
		}

		// Create the embed object.
		DiscordEmbedBuilder embed =
			new DiscordEmbedBuilder()
			.WithAuthor(author_name, null, message.Author.AvatarUrl)
			.WithTitle($"\u21D2 #{message.Channel?.Name}")
			.WithUrl(message.JumpLink)
			.WithColor(_channelColors[message.ChannelId])
			.WithDescription(content)
			.WithFooter(_footerPrefix + message.Id.ToString());

		// Add thumbnail if applicable.
		string? thumbnail = GetThumbnail(message);
		if (DoSpoiler(message))
			thumbnail = null;
		if (thumbnail is not null)
			embed = embed.WithThumbnail(thumbnail);

		return embed.Build();
	}

	// Returns whether or not the message is from a channel that
	// needs to be spoiled.
	private static bool DoSpoiler(DiscordMessage message) =>
		_spoilerChannels.Contains(message.ChannelId);

	// Returns a thumbnail of the crossposted message.
	// Returns null if there is nothing to preview.
	private static string? GetThumbnail(DiscordMessage message) {
		// Only fetch thumbnails for regular messages.
		switch (message.MessageType) {
		case MessageType.Default:
		case MessageType.Reply:
			break;
		default:
			return null;
		}

		// Return early if no thumbnail content exists.
		List<DiscordAttachment> files = new (message.Attachments);
		List<DiscordEmbed> embeds = new (message.Embeds);
		if (files.Count == 0 && embeds.Count == 0)
			return null;

		// Return image thumbnail if exists.
		foreach (DiscordAttachment file in files) {
			if (file.MediaType.StartsWith("image"))
				return file.Url;
		}

		// Return embed thumbnail if exists.
		if (embeds.Count > 0) {
			if (embeds[0].Image is not null)
				return embeds[0].Image.Url.ToString();
			if (embeds[0].Thumbnail is not null)
				return embeds[0].Thumbnail.Url.ToString();
		}

		return null;
	}

	// Returns a string representation of the crossposted message.
	// Returns null if the content is blank or unrecognized.
	private static string? GetSummary(DiscordMessage message) {
		const string ellipsis = "\u2026";

		// Return generic messages for specific message types.
		// Filter unsupported message types.
		switch (message.MessageType) {
		case MessageType.Default:
		case MessageType.Reply:
		case MessageType.ApplicationCommand:
		case MessageType.ContextMenuCommand:
			break;
		case MessageType.ChannelPinnedMessage:
			return "pinned message to the channel";
		case MessageType.GuildMemberJoin:
			return "joined the server";
		case MessageType.UserPremiumGuildSubscription:
			return "boosted the server";
		case MessageType.TierOneUserPremiumGuildSubscription:
			return "server boosted to level 1";
		case MessageType.TierTwoUserPremiumGuildSubscription:
			return "server boosted to level 2";
		case MessageType.TierThreeUserPremiumGuildSubscription:
			return "server boosted to level 3";
		case MessageType.AutoModAlert:
			throw new NotImplementedException("AutoMod alert messages not recognized yet.");
		default:
			return null;
		}

		// Return trimmed message content if available.
		string text = message.Content.Trim();
		if (text != "") {
			string preview = text;
			return (preview.Length <= _capCharsPreview)
				? preview
				: preview[.._capCharsPreview] + $" [{ellipsis}]";
		}

		// Return embed summary if available.
		List<DiscordEmbed> embeds = new (message.Embeds);
		if (embeds.Count == 0)
			return null;
		string title = embeds[0].Title.Trim();
		if (title != "")
			return title;
		string description = embeds[0].Description.Trim();
		if (description != "") {
			return (description.Length <= _capCharsPreview)
				? description
				: $"{description[.._capCharsPreview]} [{ellipsis}]";
		}

		// Return null if no summary could be created.
		return null;
	}

	// Returns a formatting string describing the emojis.
	private static string PrintEmojiList(IReadOnlyList<DiscordReaction> reacts) {
		const string
			nbsp      = "\u00A0",
			separator = "\u2003",
			ellipsis  = "\u2026";

		// Sort (and cap) list of emojis.
		List<DiscordReaction> reacts_list = new (reacts);
		reacts_list.Sort((x, y) => { return y.Count - x.Count; });
		bool is_elided = reacts_list.Count > _capEmojiDisplay;
		if (is_elided)
			reacts_list = reacts_list.GetRange(0, _capEmojiDisplay);

		// Format as string.
		string text = "";
		foreach (DiscordReaction reaction in reacts_list) {
			text += $"{reaction.Emoji}{nbsp}" +
				$"**{reaction.Count}**{separator}";
		}
		if (is_elided)
			text += ellipsis;
		if (text.EndsWith(separator))
			text = text[..^separator.Length];

		return text;
	}
}
