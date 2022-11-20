namespace Irene.Modules;

class Starboard {
	private record class ChannelSettings(
		int Threshold,
		bool HasSpoilers,
		DiscordColor EmbedColor
	);
	private record struct ChannelMessage {
		public readonly ulong ChannelId;
		public readonly ulong MessageId;

		public ChannelMessage(DiscordMessage message) :
			this (message.ChannelId, message.Id) { }
		public ChannelMessage(ulong channelId, ulong messageId) {
			ChannelId = channelId;
			MessageId = messageId;
		}

		private const string _separator = ">";
		public static ChannelMessage FromString(string input) {
			string[] split = input.Split(_separator, 2);
			return new (ulong.Parse(split[0]), ulong.Parse(split[1]));
		}
		public override string ToString() =>
			string.Join(_separator, ChannelId, MessageId);
	}


	// --------
	// Properties and fields:
	// --------

	private static readonly TaskQueue
		_queueCandidates  = new (), // messages to check
		_queueFileBlocked = new (), // file access
		_queueFileForced  = new (); // file access
	// The cache of recently pinned posts is indexed by the channel/message
	// IDs of the original posts, and the values are the corresponding
	// messages in the starboard channel.
	private static readonly HotQueueMap<ChannelMessage, DiscordMessage> _cache;
	private const int _cacheSize = 16;
	private const string
		_pathBlocked = @"data/starboard-blocked.txt",
		_pathForced  = @"data/starboard-forced.txt";
	private static readonly DiscordColor
		_colorBlack = new ("#000000"),
		_colorDark  = new ("#3E0600"),
		_colorRed   = new ("#DA4331"),
		_colorLight = new ("#FFCEC9");
	private const int
		_capEmojiPreview = 4,
		_capCharsPreview = 420; // hi Ambi
	private const string
		_nbsp  = "\u00A0",
		_emsp  = "\u2003",
		_ellip = "\u2026";

	static Starboard() {
		Util.CreateIfMissing(_pathBlocked);
		Util.CreateIfMissing(_pathForced);

		// Cache starts out empty and gets populated naturally.
		_cache = new (_cacheSize);

		// Queue up any candidate updates to the starboard.
		static Task HandleReaction(DiscordMessage messagePartial) {
			_ = Task.Run(async () => {
				DiscordMessage message = await
					PopulatePartialMessage(messagePartial);
				await _queueCandidates.Run(new Task<Task>(async () => {
					await RefreshStarredPost(message);
				}));
			});
			return Task.CompletedTask;
		}
		Client.MessageReactionAdded +=
			(irene, e) => HandleReaction(e.Message);
		Client.MessageReactionRemoved +=
			(irene, e) => HandleReaction(e.Message);
	}


	// --------
	// Public interface methods:
	// --------

	// Refreshing a starred post will go through all the relevant checks
	// (blocklist, forcelist, regular requirements). It will update any
	// existing posts, create a new post if needed, and delete the existing
	// post if one shouldn't exist.
	// Returns the message embedding the starred post, or null if the
	// corresponding post should not exist.
	public static async Task<DiscordMessage?> RefreshStarredPost(DiscordMessage message) {
		bool shouldStar = await ShouldStar(message);

		if (shouldStar) {
			// Create (or update) a starred post.
			return await OverwriteStarredPost(message);
		} else {
			// Ensure starred post is deleted.
			await DeleteStarredPost(message);
			return null;
		}
	}

	// "Blocked" messages are never added to the starboard, and will
	// always return false when checking if a message should be starred.
	public static async Task<bool> IsBlocked(DiscordMessage message) {
		string id = new ChannelMessage(message).ToString();
		List<string> blocklist = await ReadAllBlocked();
		return blocklist.Contains(id);
	}
	// Returns false if block/unblock was redundant; true otherwise.
	// Blocking a message will:
	// - remove its post from the starboard
	// - remove the message from the forcelist
	// Unblocking a message will:
	// - add a post to the starboard (if requirements are met)
	public static async Task<bool> Block(DiscordMessage message, bool doBlock=true) {
		string id = new ChannelMessage(message).ToString();
		List<string> blocklist = await ReadAllBlocked();
		List<string> forcelist = await ReadAllForced();
		List<Task> tasks = new ();

		// Remove message from forcelist if it's being blocked.
		if (doBlock && forcelist.Contains(id)) {
			forcelist.Remove(id);
			tasks.Add(WriteAllForced(forcelist));
		}

		// Short-circuit redundant block/unblock.
		if (doBlock == blocklist.Contains(id)) {
			await Task.WhenAll(tasks);
			return false;
		}

		// Update blocklist.
		if (doBlock)
			// Inserting the most recent entry at the start theoretically
			// increases read performance.
			blocklist.Insert(0, id);
		else
			blocklist.Remove(id);
		tasks.Add(WriteAllBlocked(blocklist));

		// Update starboard.
		await Task.WhenAll(tasks);
		await RefreshStarredPost(message);

		return true;
	}

	// "Forced" messages are added to the starboard, and will always
	// return true when checking if a message should be starred.
	public static async Task<bool> IsForced(DiscordMessage message) {
		string id = new ChannelMessage(message).ToString();
		List<string> forcelist = await ReadAllForced();
		return forcelist.Contains(id);
	}
	// Returns false if the force/unforce was redundant; true otherwise.
	// Forcing a message will:
	// - create/update the starboard
	// - remove the message from the blocklist
	// Unforcing a message will:
	// - remove its post from the starboard (if requirements aren't met)
	public static async Task<bool> Force(DiscordMessage message, bool doForce=true) {
		string id = new ChannelMessage(message).ToString();
		List<string> forcelist = await ReadAllForced();
		List<string> blocklist = await ReadAllBlocked();
		List<Task> tasks = new ();

		// Remove message from blocklist if it's being forced.
		if (doForce && blocklist.Contains(id)) {
			blocklist.Remove(id);
			tasks.Add(WriteAllBlocked(blocklist));
		}

		// Short-circuit redundant force/unforce.
		if (doForce == forcelist.Contains(id)) {
			await Task.WhenAll(tasks);
			return false;
		}

		// Update forcelist.
		if (doForce)
			// Inserting the most recent entry at the start theoretically
			// increases read performance.
			forcelist.Insert(0, id);
		else
			forcelist.Remove(id);
		tasks.Add(WriteAllForced(forcelist));

		// Update starboard.
		await Task.WhenAll(tasks);
		await RefreshStarredPost(message);

		return true;
	}


	// --------
	// Starboard posting helper methods:
	// --------

	// Updates starboard post for the specified message, or creates a
	// new one if one doesn't exist already. Creating a new post will
	// also notify the author and update the cache.
	// The input message is not checked against requirements.
	// Returns the updated message from the starboard channel.
	private static async Task<DiscordMessage> OverwriteStarredPost(DiscordMessage message) {
		ChannelSettings? settings = GetSettings(message.ChannelId);
		if (settings is null)
			throw new InvalidOperationException("Attempted to create starred post in an unsupported channel.");

		DiscordMessage? post = await FindStarredPost(message);
		DiscordEmbed embed = await CreateEmbed(message, settings);
		if (post is not null) {
			return await post.ModifyAsync(embed);
		} else {
			Log.Information("Adding new post to starboard.");
			Log.Debug("  Original post: #{Channel}", message.Channel.Name);
			post = await GetStarboard().SendMessageAsync(embed);
			await NotifyAuthor(message);
			_cache.Push(new ChannelMessage(message), post);
			return post;
		}
	}
	// Delete the corresponding starboard post, and flush it from cache
	// (if it was in cache).
	private static async Task DeleteStarredPost(DiscordMessage message) {
		DiscordMessage? post = await FindStarredPost(message);
		if (post is not null) {
			Log.Information("Removing post from starboard.");
			Log.Debug("  Original post: #{Channel}", message.Channel.Name);
			await post.DeleteAsync();
		}
		_cache.Flush(new ChannelMessage(message));
	}

	// Find a starred post (the message in the starboard channel) for
	// a given input message, if it exists.
	// Returns null if no existing starred post was found.
	private static async Task<DiscordMessage?> FindStarredPost(DiscordMessage message) {
		// Check cache for existing pinned message.
		ChannelMessage id = new (message);
		bool wasCached = _cache.TryAccess(id, out DiscordMessage? cacheValue);
		if (wasCached)
			return cacheValue;

		DiscordChannel starboard = GetStarboard();

		// Initialize data for upcoming loop.
		int searchSize = 60;
		List<DiscordMessage> messages =
			new (await starboard.GetMessagesAsync(searchSize));

		// Search through messages until we reach the beginning of the
		// channel, or until the timestamp of the fetched messages predates
		// the creation of the specified input message.
		while (messages.Count > 0) {
			// Check through fetched messages for a matching message.
			foreach (DiscordMessage message_i in messages) {
				IReadOnlyList<DiscordEmbed> embeds = message_i.Embeds;
				if (embeds.Count != 1)
					continue;
				DiscordEmbed embed = embeds[0];
				if (IsMatch(message, embed)) {
					_cache.Push(new ChannelMessage(message), message_i);
					return message_i;
				}
				if (message_i.Timestamp < message.Timestamp)
					return null;
			}

			// Fetch more messages to check through.
			ulong idPrev = messages[^1].Id;
			messages = new
				(await starboard.GetMessagesBeforeAsync(idPrev, searchSize));
		}

		// If the loop finishes, then nothing was found.
		return null;
	}
	

	// --------
	// Internal (queued) file I/O methods:
	// --------

	// File I/O for blocklist.
	private static async Task WriteAllBlocked(List<string> blocklist) {
		await _queueFileBlocked.Run(new Task<Task>(async () => {
			await File.WriteAllLinesAsync(_pathBlocked, blocklist);
		}));
	}
	private static async Task<List<string>> ReadAllBlocked() =>
		await _queueFileBlocked.Run(
			new Task<Task<List<string>>> (async () => {
				return new (await File.ReadAllLinesAsync(_pathBlocked));
			})
		);

	// File I/O for forcelist.
	private static async Task WriteAllForced(List<string> forcelist) {
		await _queueFileForced.Run(new Task<Task>(async () => {
			await File.WriteAllLinesAsync(_pathForced, forcelist);
		}));
	}
	private static async Task<List<string>> ReadAllForced() =>
		await _queueFileForced.Run(
			new Task<Task<List<string>>> (async () => {
				return new (await File.ReadAllLinesAsync(_pathForced));
			})
		);


	// --------
	// Embed formatting methods:
	// --------

	// Returns an embed that can be directly posted to the starboard.
	private static async Task<DiscordEmbed> CreateEmbed(DiscordMessage message, ChannelSettings settings) {
		// Fetch author name.
		// Webhooks should be formatted without a tag.
		string author = message.WebhookMessage
			? message.Author.Username
			: (await message.Author.ToMember())
				?.DisplayName ?? message.Author.Tag();

		// Construct summary string.
		string emojiPreview = PreviewEmojis(message);
		string? content = Summarize(message);
		if (content is not null) {
			if (settings.HasSpoilers)
				content = content.Spoiler();
			content += "\n" + emojiPreview;
		} else {
			content = emojiPreview;
		}

		// Construct embed object.
		DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
			.WithAuthor(author, null, message.Author.AvatarUrl)
			.WithTitle($"\u21D2 #{message.Channel?.Name}")
			.WithUrl(message.JumpLink)
			.WithColor(settings.EmbedColor)
			.WithDescription(content);

		// Add thumbnail (if applicable).
		string? thumbnail = PreviewThumbnail(message);
		if (settings.HasSpoilers)
			thumbnail = null;
		if (thumbnail is not null)
			embed = embed.WithThumbnail(thumbnail);

		return embed.Build();
	}
	
	// Returns a string representation of the crossposted message.
	// Returns null if the content is blank or unrecognized.
	private static string? Summarize(DiscordMessage message) {
		// Return generic messages for specific message types.
		// Filter unsupported message types.
		switch (message.MessageType) {
		case MessageType.Default:
		case MessageType.Reply:
			break; // Continue summarization logic.
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
		case MessageType.ChannelFollowAdd:
			return "followed announcements channel";
		case MessageType.GuildDiscoveryDisqualified:
			return "server removed from server discovery";
		case MessageType.GuildDiscoveryRequalified:
			return "server re-added to server discovery";
		case MessageType.GuildDiscoveryGracePeriodInitialWarning:
		case MessageType.GuildDiscoveryGracePeriodFinalWarning:
			return "server has not met server discovery requirements";
		case MessageType.ApplicationCommand:
		case MessageType.ContextMenuCommand:
			return "used a command";
		case MessageType.GuildInviteReminder:
			return "invite friends to join the server";
		case MessageType.AutoModAlert:
			return "automod blocked a message";
		default:
			//MessageType.RecipientAdd      (group DM)
			//MessageType.RecipientRemove   (group DM)
			//MessageType.Call              (DM, group DM)
			//MessageType.ChannelNameChange (group DM)
			//MessageType.ChannelIconChange (group DM)
			return null;
		}

		// Return trimmed message content (if non-empty).
		string? text = message.Content.Trim();
		if (text != "") {
			return (text.Length <= _capCharsPreview)
				? text
				: $"{text[.._capCharsPreview]} [{_ellip}]";
		}

		// Return embed summary (if available).
		List<DiscordEmbed> embeds = new (message.Embeds);
		foreach (DiscordEmbed embed in embeds) {
			text = Summarize(embed);
			if (text is not null)
				return text;
		}

		// Return null if summarization failed.
		return null;
	}
	private static string? Summarize(DiscordEmbed embed) {
		string title = embed.Title.Trim();
		if (title != "")
			return title;

		string description = embed.Description.Trim();
		if (description != "") {
			return (description.Length <= _capCharsPreview)
				? description
				: $"{description[.._capCharsPreview]} [{_ellip}]";
		}

		// Return null if summarization failed.
		return null;
	}
	
	private static string PreviewEmojis(DiscordMessage message) {
		// Sort (and cap) list of emojis.
		List<DiscordReaction> reactions = new (message.Reactions);
		reactions.Sort((x, y) => y.Count - x.Count);
		bool doElide = reactions.Count > _capEmojiPreview;
		if (doElide)
			reactions = reactions.GetRange(0, _capEmojiPreview);

		// Concatenate all emojis for display.
		string text = "";
		foreach (DiscordReaction reaction in reactions)
			text += $"{reaction.Emoji}{_nbsp}**{reaction.Count}**{_emsp}";
		if (doElide)
			text += _ellip;
		else
			text = text[..^_emsp.Length];

		return text;
	}
	// Returns a thumbnail url for the crossposted message.
	// Returns null if there is nothing to preview, or if the thumbnail
	// is a spoiler image.
	private static string? PreviewThumbnail(DiscordMessage message) {
		// Only fetch thumbnails for "regular" messages.
		switch (message.MessageType) {
		case MessageType.Default:
		case MessageType.Reply:
			break;
		default:
			return null;
		}

		// Check, in order, files, then embeds, then stickers.
		// Return as soon as a valid thumbnail is found.
		// This is roughly equivalent to checking if any exist (at all)
		// at the start, and has minimal additional impact on performance.

		List<DiscordAttachment> files = new (message.Attachments);
		foreach (DiscordAttachment file in files) {
			if (file.MediaType.StartsWith("image") &&
				!file.FileName.StartsWith("SPOILER_")
			)
				{ return file.Url; }
		}

		List<DiscordEmbed> embeds = new (message.Embeds);
		foreach (DiscordEmbed embed in embeds) {
			if (embed.Image is not null)
				return embed.Image.Url.ToString();
			if (embed.Thumbnail is not null)
				return embed.Thumbnail.Url.ToString();
		}

		List<DiscordMessageSticker> stickers = new (message.Stickers);
		foreach (DiscordMessageSticker sticker in stickers) {
			if (sticker.StickerUrl is not null)
				return sticker.StickerUrl;
		}

		// Return null if no valid thumbnails were found.
		return null;
	}


	// --------
	// Other internal helper/component methods:
	// --------

	// The DiscordMessage returned from the "reaction added" event is
	// incomplete; this method fetches the full message object.
	// According to official API docs, the partial object has:
	// - user ID
	// - channel ID
	// - message ID
	// - guild ID (nullable)
	// - member object (nullable)
	// - emoji (partial object)
	private static async Task<DiscordMessage> PopulatePartialMessage(DiscordMessage message) {
		if (Erythro is null)
			throw new InvalidOperationException("Guild not initialized yet.");

		DiscordChannel channel = await
			Erythro.Client.GetChannelAsync(message.ChannelId);
		return await channel.GetMessageAsync(message.Id);
	}

	// Convenience function holding settings for allowed channels.
	// Returns null if a channel should be ignored for the starboard.
	private static ChannelSettings? GetSettings(ulong channelId) =>
		channelId switch {
			id_ch.general  => new (4, false, _colorLight),
			id_ch.sharing  => new (4, false, _colorRed  ),
			id_ch.spoilers => new (4, true , _colorLight),
			id_ch.memes    => new (6, false, _colorDark ),
			id_ch.bots     => new (4, false, _colorDark ),
			id_ch.news     => new (6, false, _colorRed  ),
			_ => null,
		};
	// Convenience function for fetching an instantiated starboard channel.
	private static DiscordChannel GetStarboard() {
		if (Erythro is null)
			throw new InvalidOperationException("Guild not initialized yet.");
		return Erythro.Channel(id_ch.starboard);
	}
	
	// Checks if the given embed is a summary of a message.
	private static bool IsMatch(DiscordMessage message, DiscordEmbed embed) {
		string link = embed.Url.ToString();
		string id = $"{message.ChannelId}/{message.Id}";
		return link.EndsWith(id);
	}
	// Checks if the given message meets the requirements to be posted
	// to the starboard, including checking the blocklist/forcelist.
	private static async Task<bool> ShouldStar(DiscordMessage message) {
		// Check blocklist and forcelist.
		string id = new ChannelMessage(message).ToString();
		List<string> blocklist = await ReadAllBlocked();
		if (blocklist.Contains(id))
			return false;
		List<string> forcelist = await ReadAllForced();
		if (forcelist.Contains(id))
			return true;

		ChannelSettings? settings = GetSettings(message.ChannelId);

		// No need to check for private channels--they won't have settings
		// defined in the first place, and will always return null here.
		if (settings is null)
			return false;

		// First pass: quick check if total reaction count even passes
		// threshold (without checking for duplicate/OP reactions).
		List<DiscordReaction> reactions = new (message.Reactions);
		int reactionCount = 0;
		foreach (DiscordReaction reaction in reactions)
			reactionCount += reaction.Count;
		if (reactionCount < settings.Threshold)
			return false;

		// Fetch all reaction data in parallel and check for duplicate
		// reactions / reactions from OP.
		List<Task<IReadOnlyList<DiscordUser>>> tasks = new ();
		foreach (DiscordReaction reaction in reactions)
			tasks.Add(message.GetReactionsAsync(reaction.Emoji));
		// By default this only fetches 25 users, but we're only checking
		// to pass the threshold, not to get the actual count.
		await Task.WhenAll(tasks);

		HashSet<DiscordUser> users = new ();
		foreach (Task<IReadOnlyList<DiscordUser>> task in tasks)
			users.UnionWith(await task);
		users.Remove(message.Author); // no exception if not found

		// Return result.
		return users.Count >= settings.Threshold;
	}

	// Send a message to the author of a starred post.
	private static async Task NotifyAuthor(DiscordMessage message) {
		DiscordMember? author = await message.Author.ToMember();
		if (author is null) {
			Log.Warning("Could not convert {Author} to member object.", message.Author.Tag());
			return;
		}

		if (Erythro is null)
			throw new InvalidOperationException("Guild not initialized yet.");

		Log.Information("  Notifying message author: {Author}", author.Tag());
		await author.SendMessageAsync($"""
			Congrats! :tada:
			Your post in {message.Channel.Mention} was extra-popular, and has been included in {GetStarboard().Mention}.
			{Erythro.Emoji(id_e.eryLove)} {Erythro.Emoji(id_e.erythro)}
			""");
	}
}
