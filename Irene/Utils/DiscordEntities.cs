namespace Irene.Utils;

static partial class Util {
	// A table of all (non-deprecated) permissions, categorized and
	// sorted corresponding to the desktop client's display order.
	private static readonly IReadOnlyDictionary<Permissions, string> _permissionsTable =
		new ConcurrentDictionary<Permissions, string> {
			// General permissions
			[Permissions.AccessChannels] = "View channels"  ,
			[Permissions.ManageChannels] = "Manage channels",
			[Permissions.ManageRoles   ] = "Manage roles"   ,
			[Permissions.ManageEmojis  ] = "Manage emojis & stickers",
			[Permissions.ViewAuditLog  ] = "View audit logs",
			[Permissions.ManageWebhooks] = "Manage webhooks",
			[Permissions.ManageGuild   ] = "Manage server"  ,

			// Membership permissions
			[Permissions.CreateInstantInvite] = "Create invites",
			[Permissions.ChangeNickname ] = "Change own nickname",
			[Permissions.ManageNicknames] = "Manage nicknames"   ,
			[Permissions.KickMembers    ] = "Kick members"       ,
			[Permissions.BanMembers     ] = "Ban members"        ,
			[Permissions.ModerateMembers] = "Timeout members"    ,

			// Text channel permissions
			[Permissions.SendMessages          ] = "Send messages"           ,
			[Permissions.SendMessagesInThreads ] = "Send messages in threads",
			[Permissions.CreatePublicThreads   ] = "Create public threads"   ,
			[Permissions.CreatePrivateThreads  ] = "Create private threads"  ,
			[Permissions.EmbedLinks            ] = "Embed links"             ,
			[Permissions.AttachFiles           ] = "Attach files"            ,
			[Permissions.AddReactions          ] = "Add reactions"           ,
			[Permissions.UseExternalEmojis     ] = "Use external emojis"     ,
			[Permissions.UseExternalStickers   ] = "Use external stickers"   ,
			[Permissions.MentionEveryone       ] = "Mention all roles"       ,
			[Permissions.ManageMessages        ] = "Manage messages"         ,
			[Permissions.ManageThreads         ] = "Manage threads"          ,
			[Permissions.ReadMessageHistory    ] = "Read message history"    ,
			[Permissions.SendTtsMessages       ] = "Send TTS messages"       ,
			[Permissions.UseApplicationCommands] = "Use application commands",

			// Voice channel permissions
			[Permissions.UseVoice         ] = "Connect to voice chat",
			[Permissions.Speak            ] = "Speak"                ,
			[Permissions.Stream           ] = "Stream video"         ,
			[Permissions.StartEmbeddedActivities] = "Use activities" ,
			[Permissions.UseVoiceDetection] = "Use voice activity"   ,
			[Permissions.PrioritySpeaker  ] = "Priority speaker"     ,
			[Permissions.MuteMembers      ] = "Mute members"         ,
			[Permissions.DeafenMembers    ] = "Deafen members"       ,
			[Permissions.MoveMembers      ] = "Move members"         ,

			// Stage channel permissions
			[Permissions.RequestToSpeak] = "Request to speak",

			// Events permissions
			[Permissions.ManageEvents] = "Manage events",

			// Special permissions
			[Permissions.Administrator] = "Administrator",
			[Permissions.All ] = "All" ,
			[Permissions.None] = "None",
		};

	// Returns a list of permission flags.
	public static IReadOnlyList<Permissions> PermissionsFlags() =>
		new List<Permissions>(_permissionsTable.Keys);
	// Returns the human readable display string for the permission.
	public static string Description(this Permissions perms) =>
		_permissionsTable.ContainsKey(perms) ?
			_permissionsTable[perms] : "Unknown";

	// Stringify a DiscordActivity (accounting for custom statuses).
	public static string AsStatusText(this DiscordActivity status) {
		if (status.ActivityType == ActivityType.Custom) {
			DiscordCustomStatus customStatus = status.CustomStatus;
			return $"{customStatus.Emoji} {customStatus.Name}";
		}

		string prefix = status.ActivityType switch {
			ActivityType.Playing     => "Playing"     ,
			ActivityType.Streaming   => "Streaming"   ,
			ActivityType.ListeningTo => "Listening to",
			ActivityType.Watching    => "Watching"    ,
			ActivityType.Competing   => "Competing in",
			ActivityType.Custom => throw new ImpossibleException(),
			_ => throw new UnclosedEnumException(typeof(ActivityType), status.ActivityType),
		};
		return $"{prefix} {status.Name}";
	}

	// Returns the DiscordMember equivalent of the DiscordUser.
	// Returns null if the conversion wasn't possible.
	public static async Task<DiscordMember?> ToMember(this DiscordUser user) {
		// Check if trivially convertible.
		DiscordMember? member = user as DiscordMember;
		if (member is not null)
			return member;

		// Check if guild is loaded (to convert users with).
		if (Erythro is null)
			return null;

		// Fetch the member by user ID.
		try {
			// We always want to update cache, to ensure our resolved
			// member data (e.g. roles) is fully up-to-date. There is
			// no other way to determine if the data is outdated.
			member = await Erythro.Guild.GetMemberAsync(user.Id, true);
			return member;
		} catch (DSharpPlus.Exceptions.ServerErrorException) {
			return null;
		}
	}

	// Repopulate a message using only its ID and channel ID.
	public static async Task<DiscordMessage> RefetchMessage(DiscordClient client, DiscordMessage message) {
		DiscordChannel channel = await
			client.GetChannelAsync(message.ChannelId);
		return await channel.GetMessageAsync(message.Id);
	}

	// Fetches audit log entries, but wrapping the call in a
	// try/catch block to handle exceptions.
	public static async Task<DiscordAuditLogEntry?> LatestAuditLogEntry(
		this DiscordGuild guild,
		AuditLogActionType? type
	) {
		try {
			List<DiscordAuditLogEntry> entry =
				new (await guild.GetAuditLogsAsync(
					limit: 1,
					by_member: null,
					action_type: type
				));
			return (entry.Count < 1) ? null : entry[0];
		} catch {
			return null;
		}
	}
}
