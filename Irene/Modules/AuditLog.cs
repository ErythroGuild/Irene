namespace Irene.Modules;

using System.Diagnostics;

static partial class AuditLog {
	private static readonly ConcurrentDictionary<AuditLogActionType, DiscordAuditLogEntry?> _logsLatest = new ();
	private static bool _isLoaded = false;

	private const string
		_t = "\u2003",
		_t2 = _t + _t,
		_b = "\u2022",
		_a = "\u2B9A",
		_r = "\u21A6",
		_n = "`N/A`";

	static AuditLog() {
		_ = InitAsync();
	}
	private static async Task InitAsync() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		await InitEntriesAsync();
		InitHandlers();

		Log.Information("  Initialized module: AuditLog");
		string entry_count = _logsLatest.Count switch {
			1 => "entry",
			_ => "entries", // includes 0
		};
		Log.Debug($"    Fetched {{EntryCount}} audit log {entry_count}.", _logsLatest.Count);
		Log.Debug("    Event handlers registered.");
		stopwatch.LogMsec(2);
	}

	// Initialize the audit log table with "base" values, these can
	// later be compared to to determine if a new entry was added.
	private static async Task InitEntriesAsync() {
		CheckErythroInit();

		// Fire off all the requests and wait for them to finish.
		List<Task> tasks = new ();
		AuditLogActionType[] types =
			Enum.GetValues<AuditLogActionType>();
		foreach (AuditLogActionType type in types) {
			tasks.Add(
				Erythro.Guild.LatestAuditLogEntry(type)
				.ContinueWith((task) =>
					{ _logsLatest.TryAdd(type, task.Result); }
				)
			);
		}
		await Task.WhenAll(tasks);
		_isLoaded = true;
	}

	// Attach handlers to all relevant events.
	private static void InitHandlers() {
		CheckErythroInit();

		// New member joined server.
		// (Includes bots being added to the server.)
		Erythro.Client.GuildMemberAdded += (c, e) => {
			_ = Task.Run(async () => {
				DiscordMember member = e.Member;

				// Fetch additional data.
				DiscordAuditLogBotAddEntry? entry = await
					FindEntryAsync<DiscordAuditLogBotAddEntry>
					(AuditLogActionType.BotAdd);

				// Format output.
				List<string> data = new ();
				string type_join_str = member.IsBot
					? "Bot added"
					: "Member joined";
				data.Add($"**{type_join_str}:** {AsData(member)}");
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// Member left server.
		// (Includes member pruning and members being kicked.)
		Erythro.Client.GuildMemberRemoved += (c, e) => {
			_ = Task.Run(async () => {
				DiscordMember member = e.Member;

				// Fetch additional data.
				Task<DiscordAuditLogKickEntry?> task_entry_kick =
					FindEntryAsync<DiscordAuditLogKickEntry>
					(AuditLogActionType.Kick);
				Task<DiscordAuditLogPruneEntry?> task_entry_prune =
					FindEntryAsync<DiscordAuditLogPruneEntry>
					(AuditLogActionType.Prune);
				await Task.WhenAll(task_entry_kick, task_entry_prune);
				DiscordAuditLogKickEntry? entry_kick =
					await task_entry_kick;
				DiscordAuditLogPruneEntry? entry_prune =
					await task_entry_prune;

				// Format output.
				List<string> data = new ();
				if (entry_prune is not null) {
					data.Add($"**Members pruned:** {AsData(member)}");
					data.Add($"{entry_prune.Toll} members inactive for {entry_prune.Days}+ days.");
					data = await AddEntryDataAsync(data, entry_prune);
				} else if (entry_kick is not null) {
					data.Add($"**Member removed:** {AsData(member)}");
					data = await AddEntryDataAsync(data, entry_kick);
				} else {
					data.Add($"**Member left:** {AsData(member)}");
				}
				LogEntry(data);
			});
			return Task.CompletedTask;
		};

		// User banned.
		Erythro.Client.GuildBanAdded += (c, e) => {
			_ = Task.Run(async () => {
				DiscordMember member = e.Member;

				// Fetch additional data.
				DiscordAuditLogBanEntry? entry = await
					FindEntryAsync<DiscordAuditLogBanEntry>
					(AuditLogActionType.Ban);

				// Format output.
				List<string> data = new ()
					{ $"**User banned:** {AsData(member)}" };
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// User unbanned.
		Erythro.Client.GuildBanRemoved += (c, e) => {
			_ = Task.Run(async () => {
				DiscordMember member = e.Member;

				// Fetch additional data.
				DiscordAuditLogBanEntry? entry = await
					FindEntryAsync<DiscordAuditLogBanEntry>
					(AuditLogActionType.Unban);

				// Format output.
				List<string> data = new ()
					{ $"**User unbanned:** {AsData(member)}" };
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};

		// User info/roles updated.
		Erythro.Client.GuildMemberUpdated += (c, e) => {
			_ = Task.Run(async () => {
				DiscordMember member = e.Member;

				// Fetch additional data.
				Task<DiscordAuditLogMemberUpdateEntry?>
					task_entry =
						FindEntryAsync<DiscordAuditLogMemberUpdateEntry>
						(AuditLogActionType.MemberUpdate),
					task_entry_roles =
						FindEntryAsync<DiscordAuditLogMemberUpdateEntry>
						(AuditLogActionType.MemberRoleUpdate);
				await Task.WhenAll(task_entry, task_entry_roles);
				DiscordAuditLogMemberUpdateEntry?
					entry = await task_entry,
					entry_roles = await task_entry_roles;

				// Only print this event if an audit log entry was found,
				// meaning the change was significant:
				if (entry is null && entry_roles is null)
					return;

				// Format output.
				List<string> data = new ()
					{ $"**Member info updated:** {AsData(member)}" };
				if (entry is not null) {
					data = AddChanges(data, entry);
					data = await AddEntryDataAsync(data, entry);
				}
				if (entry_roles is not null) {
					data = AddChanges(data, entry);
					data = await AddEntryDataAsync(data, entry_roles);
				}
				LogEntry(data);
			});
			return Task.CompletedTask;
		};

		// Member was disconnected from channel.
		// Member was moved from channel.
		// ^ Both of these events are wrapped into "voice state" event,
		//   but there is no easy way to distinguish user-initiated ones.

		// Guild updated.
		Erythro.Client.GuildUpdated += (c, e) => {
			_ = Task.Run(async () => {
				// Fetch additional data.
				DiscordAuditLogGuildEntry? entry = await
					FindEntryAsync<DiscordAuditLogGuildEntry>
					(AuditLogActionType.GuildUpdate);

				// Format output.
				List<string> data = new ()
					{ "**Server settings updated.**" };
				data = AddChanges(data, entry);
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};

		// Role created.
		Erythro.Client.GuildRoleCreated += (c, e) => {
			_ = Task.Run(async () => {
				DiscordRole role = e.Role;

				// Fetch additional data.
				DiscordAuditLogRoleUpdateEntry? entry = await
					FindEntryAsync<DiscordAuditLogRoleUpdateEntry>
					(AuditLogActionType.RoleCreate);

				// Format output.
				List <string> data = new ()
					{ $"**New role created:** {role.Name} (`{role.Id}`)" };
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// Role deleted.
		Erythro.Client.GuildRoleDeleted += (c, e) => {
			_ = Task.Run(async () => {
				DiscordRole role = e.Role;

				// Fetch additional data.
				DiscordAuditLogRoleUpdateEntry? entry = await
					FindEntryAsync<DiscordAuditLogRoleUpdateEntry>
					(AuditLogActionType.RoleDelete);

				// Format output.
				List <string> data = new ()
					{ $"**Role deleted:** {role.Name} (`{role.Id}`)" };
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// Role updated.
		Erythro.Client.GuildRoleUpdated += (c, e) => {
			_ = Task.Run(async () => {
				DiscordRole role_before = e.RoleBefore;
				DiscordRole role_after = e.RoleAfter;

				// Fetch additional data.
				DiscordAuditLogRoleUpdateEntry? entry = await
					FindEntryAsync<DiscordAuditLogRoleUpdateEntry>
					(AuditLogActionType.RoleUpdate);

				// Format output.
				List <string> data = new ()
					{ $"**Role settings updated:** {role_after.Name} (`{role_after.Id}`)" };
				data = AddChanges(data, entry);
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};

		// Channel created.
		Erythro.Client.ChannelCreated += (c, e) => {
			_ = Task.Run(async () => {
				DiscordChannel ch = e.Channel;

				// Do not log if channel is a DM channel.
				if (ch.IsPrivate)
					return;

				// Fetch additional data.
				DiscordAuditLogChannelEntry? entry = await
					FindEntryAsync<DiscordAuditLogChannelEntry>
					(AuditLogActionType.ChannelCreate);

				// Format output.
				List<string> data = new () {
					$"**New channel{(ch.IsCategory ? " category " : " ")}created:** {ch.Mention}",
					$"{ch.Name} (type: {ch.Type}): `{ch.Id}`"
				};
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// Channel deleted.
		Erythro.Client.ChannelDeleted += (c, e) => {
			_ = Task.Run(async () => {
				DiscordChannel ch = e.Channel;

				// Do not log if channel is a DM channel.
				if (ch.IsPrivate)
					return;

				// Fetch additional data.
				DiscordAuditLogChannelEntry? entry = await
					FindEntryAsync<DiscordAuditLogChannelEntry>
					(AuditLogActionType.ChannelDelete);

				// Format output.
				List<string> data = new() {
					$"**Channel{(ch.IsCategory ? " category " : " ")}deleted:** {ch.Mention}",
					$"{ch.Name} (type: {ch.Type}): `{ch.Id}`"
				};
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// Channel settings updated.
		// (Includes updating channel permission overwrites.)
		Erythro.Client.ChannelUpdated += (c, e) => {
			_ = Task.Run(async () => {
				DiscordChannel ch = e.ChannelAfter;

				// Do not log if channel is a DM channel.
				if (ch.IsPrivate)
					return;

				// Fetch additional data.
				Task<DiscordAuditLogChannelEntry?>
					task_entry_channel =
						FindEntryAsync<DiscordAuditLogChannelEntry>
						(AuditLogActionType.ChannelUpdate);
				Task<DiscordAuditLogOverwriteEntry?>
					task_entry_perms_create =
						FindEntryAsync<DiscordAuditLogOverwriteEntry>
						(AuditLogActionType.OverwriteCreate),
					task_entry_perms_delete =
						FindEntryAsync<DiscordAuditLogOverwriteEntry>
						(AuditLogActionType.OverwriteDelete),
					task_entry_perms_update =
						FindEntryAsync<DiscordAuditLogOverwriteEntry>
						(AuditLogActionType.OverwriteUpdate);
				await Task.WhenAll(
					task_entry_channel,
					task_entry_perms_create,
					task_entry_perms_delete,
					task_entry_perms_update
				);
				DiscordAuditLogChannelEntry?
					entry_channel = await task_entry_channel;
				DiscordAuditLogOverwriteEntry?
					entry_perms_create = await task_entry_perms_create,
					entry_perms_delete = await task_entry_perms_delete,
					entry_perms_update = await task_entry_perms_update;

				// Format output.
				List<string> data = new () {
					$"**Channel{(ch.IsCategory ? " category " : " ")}settings updated:** {ch.Mention}",
					$"{ch.Name} (type: {ch.Type}): `{ch.Id}`"
				};
				if (entry_channel is not null) {
					data = AddChanges(data, entry_channel);
					data = await AddEntryDataAsync(data, entry_channel);
				}
				if (entry_perms_create is not null) {
					data = AddChanges(data, entry_perms_create);
					data = await AddEntryDataAsync(data, entry_perms_create);
				}
				if (entry_perms_delete is not null) {
					data = AddChanges(data, entry_perms_delete);
					data = await AddEntryDataAsync(data, entry_perms_delete);
				}
				if (entry_perms_update is not null) {
					data = AddChanges(data, entry_perms_update);
					data = await AddEntryDataAsync(data, entry_perms_update);
				}
				LogEntry(data);
			});
			return Task.CompletedTask;
		};

		// Emoji created, deleted, or updated.
		Erythro.Client.GuildEmojisUpdated += (c, e) => {
			_ = Task.Run(async () => {
				// Diff the two lists of emojis.
				HashSet<ulong> emojis_before = new (e.EmojisBefore.Keys);
				HashSet<ulong> emojis_after  = new (e.EmojisAfter.Keys);

				HashSet<ulong> emojis_added = new (emojis_after);
				emojis_added.ExceptWith(emojis_before);

				HashSet<ulong> emojis_removed = new (emojis_before);
				emojis_removed.ExceptWith(emojis_after);

				// Fetch additional data.
				Task<DiscordAuditLogEmojiEntry?>
					task_entry_create =
						FindEntryAsync<DiscordAuditLogEmojiEntry>
						(AuditLogActionType.EmojiCreate),
					task_entry_delete =
						FindEntryAsync<DiscordAuditLogEmojiEntry>
						(AuditLogActionType.EmojiDelete),
					task_entry_update =
						FindEntryAsync<DiscordAuditLogEmojiEntry>
						(AuditLogActionType.EmojiUpdate);
				await Task.WhenAll(
					task_entry_create,
					task_entry_delete,
					task_entry_update
				);
				DiscordAuditLogEmojiEntry?
					entry_create = await task_entry_create,
					entry_delete = await task_entry_delete,
					entry_update = await task_entry_update;

				// Format output.
				List<string> data = new ()
					{ "**Server emojis updated.**" };
				if (emojis_added.Count > 0) {
					data.Add($"{_t}Emoji added:");
					foreach (ulong id in emojis_added) {
						DiscordEmoji emoji = e.EmojisAfter[id];
						data.Add($"{_t2}{AsData(emoji)}");
					}
				}
				if (emojis_removed.Count > 0) {
					data.Add($"{_t}Emoji removed:");
					foreach (ulong id in emojis_removed) {
						DiscordEmoji emoji = e.EmojisBefore[id];
						data.Add($"{_t2}{AsData(emoji)}");
					}
				}
				// No need to print additions; already displayed.
				if (entry_create is not null)
					data = await AddEntryDataAsync(data, entry_create);
				// No need to print removals; already displayed.
				if (entry_delete is not null)
					data = await AddEntryDataAsync(data, entry_delete);
				if (entry_update is not null) {
					data = AddChanges(data, entry_update);
					data = await AddEntryDataAsync(data, entry_update);
				}
				LogEntry(data);

			});
			return Task.CompletedTask;
		};

		// sticker create/delete/update

		// Invite created.
		Erythro.Client.InviteCreated += (c, e) => {
			_ = Task.Run(async () => {
				DiscordInvite inv = e.Invite;
				DiscordUser user = inv.Inviter;
				TimeSpan expiry = TimeSpan.FromSeconds(inv.MaxAge);

				// Fetch additional data.
				DiscordAuditLogInviteEntry? entry = await
					FindEntryAsync<DiscordAuditLogInviteEntry>
					(AuditLogActionType.InviteCreate);

				// Format output.
				List<string> data = new () {
					$"**Invite created:** `{inv.Code}`",
					$"Created by {user.Tag()}, can be used {inv.MaxUses} times, expires in {expiry:g}.",
					$"This invite grants {(inv.IsTemporary ? "temporary" : "normal")} access."
				};
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// Invite deleted.
		Erythro.Client.InviteDeleted += (c, e) => {
			_ = Task.Run(async () => {
				DiscordInvite inv = e.Invite;
				DiscordUser user = inv.Inviter;
				TimeSpan expiry = TimeSpan.FromSeconds(inv.MaxAge);

				// Fetch additional data.
				DiscordAuditLogInviteEntry? entry = await
					FindEntryAsync<DiscordAuditLogInviteEntry>
					(AuditLogActionType.InviteCreate);

				// Format output.
				List<string> data = new () {
					$"**Invite deleted:** `{inv.Code}`",
					$"Created by {user.Tag()}, expired in {expiry:g}.",
					$"This invite granted {(inv.IsTemporary ? "temporary" : "normal")} access."
				};
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};

		// webhook create/delete/update

		// integration create/delete/update

		// Message deleted.
		Erythro.Client.MessageDeleted += (c, e) => {
			_ = Task.Run(async () => {
				DiscordChannel channel = e.Channel;
				// Do not log if channel is a DM channel.
				if (channel.IsPrivate)
					return;
				DiscordMessage message = e.Message;

				// Fetch additional data.
				DiscordAuditLogMessageEntry? entry = await
					FindEntryAsync<DiscordAuditLogMessageEntry>
					(AuditLogActionType.MessageDelete);

				// Only log this event if the author isn't the user to
				// delete the message.
				// (Be overly-conservative: exit early if entry not found.)
				if (entry is null)
					return;
				if (entry.UserResponsible == message.Author)
					return;

				// Format output.
				List<string> data = new ()
					{ $"**Message deleted.** `{message.Id}`" };
				string timestamp = message.Timestamp.Timestamp(Util.TimestampStyle.DateTimeShort);
				data.Add($"Originally posted in {message.Channel.Mention}, on {timestamp}.");
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// Messages bulk deleted.
		Erythro.Client.MessagesBulkDeleted += (c, e) => {
			_ = Task.Run(async () => {
				List<DiscordMessage> messages = new (e.Messages);

				// Fetch additional data.
				DiscordAuditLogMessageEntry? entry = await
					FindEntryAsync<DiscordAuditLogMessageEntry>
					(AuditLogActionType.MessageBulkDelete);

				// Format output.
				List<string> data = new () {
					"**Messages bulk deleted.**",
					$"Removed `{messages.Count}` message(s)."
				};
				data = await AddEntryDataAsync(data, entry);
				LogEntry(data);
			});
			return Task.CompletedTask;
		};

		// All reactions cleared on message.
		Erythro.Client.MessageReactionsCleared += (c, e) => {
			_ = Task.Run(async () => {
				DiscordMessage message = e.Message;

				// Do not log if channel is a DM channel.
				DiscordChannel channel = await
					Erythro.Client.GetChannelAsync(message.ChannelId);
				if (channel.IsPrivate)
					return;

				List<string> data = new() {
					"**All reactions cleared from message.**",
					$"{_t}message ID:`{message.Id}`",
					$"{_t}<{message.JumpLink}>"
				};
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
		// All reactions of a specific emoji cleared on message.
		Erythro.Client.MessageReactionRemovedEmoji += (c, e) => {
			_ = Task.Run(async () => {
				DiscordMessage message = e.Message;
				DiscordEmoji emoji = e.Emoji;

				// Do not log if channel is private.
				DiscordChannel channel = await
					Erythro.Client.GetChannelAsync(message.ChannelId);
				if (channel.IsPrivate)
					return;

				List<string> data = new () {
					$"**Specific emoji reactions cleared from message:** `{emoji.GetDiscordName()}`",
					$"{_t}message ID:`{message.Id}`",
					$"{_t}<{message.JumpLink}>"
				};
				LogEntry(data);
			});
			return Task.CompletedTask;
		};
	}

	// Fetches the most recent audit log entry of the given type.
	// Returns null if entries cannot be searched.
	public static async Task<T?> FindEntryAsync<T>(AuditLogActionType type)
		where T : DiscordAuditLogEntry
	{
		CheckErythroInit();

		const int
			retry_count = 6, // ~3000 msec
			retry_interval_init = 50, // msec
			retry_interval_exp = 2;

		// Exit early if audit log baseline hasn't been initialized.
		if (!_isLoaded) {
			Log.Warning("    Must fetch baseline audit logs first.");
			return null;
		}

		// Repeatedly try to find the updated entry.
		int retry_interval = retry_interval_init;
		for (int i=0; i<retry_count; i++) {
			// Pause slightly before trying.
			retry_interval *= retry_interval_exp;
			await Task.Delay(retry_interval);

			// Attempt to fetch entry.
			DiscordAuditLogEntry? entry =
				await Erythro.Guild.LatestAuditLogEntry(type);

			// Return the entry if one was found and is new.
			// Also update the "most recent" audit log entry of that type.
			if (entry is not null) {
				if (entry.Id != (_logsLatest[type]?.Id ?? null)) {
					_logsLatest[type] = entry;
					T? entry_t = entry as T;
					if (entry_t is not null)
						return entry_t;
				}
			}
		}

		// Return null if nothing could be found even after retries.
		return null;
	}

	// Adds any user / reason data that can be found in the entry
	// (if entry is non-null).
	private static async Task<List<string>> AddEntryDataAsync(
		List<string> data,
		DiscordAuditLogEntry? entry
	) {
		CheckErythroInit();

		if (entry is null)
			return data;

		// Append user.
		DiscordUser user = entry.UserResponsible;
		DiscordMember member =
			await Erythro.Guild.GetMemberAsync(user.Id);
		data.Add($"*Action by:* {AsData(member)}");

		// Append reason.
		string reason = entry.Reason?.Trim() ?? "";
		if (reason != "")
			data.Add(reason.Quote());

		return data;
	}

	// Convenience function for outputting a log message.
	private static void LogEntry(List<string> data) {
		CheckErythroInit();

		// Log data to audit log channel.
		DateTimeOffset now = DateTimeOffset.UtcNow;
		string line_time = $"{_a} {now.Timestamp(Util.TimestampStyle.DateTimeShort)}";
		data.Insert(0, line_time);
		_ = Erythro.Channel(id_ch.audit).SendMessageAsync(
			new DiscordMessageBuilder()
			.WithContent(data.ToLines())
			.WithAllowedMentions(Mentions.None)
		);

		// Log data to console.
		Log.Information("Audit log entry added.");
		foreach (string line in data)
			Log.Debug($"  {line}");
	}

	// String representation of a user, without needing to ping them.
	private static string AsData(DiscordMember member) =>
		$"{member.DisplayName} ({member.Tag()})";
	// String representation of an emoji (including ID).
	private static string AsData(DiscordEmoji emoji) =>
		$"{emoji} ({emoji.GetDiscordName()}): `{emoji.Id}`";
}
