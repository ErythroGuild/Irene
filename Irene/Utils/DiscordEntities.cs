namespace Irene.Utils;

static partial class Util {
	// A table of all (non-deprecated) permissions, categorized and
	// sorted corresponding to the desktop client's display order.
	private static readonly ReadOnlyDictionary<Permissions, string> _permissionsTable =
		new (new ConcurrentDictionary<Permissions, string>() {
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
		});

	// Returns a list of permission flags.
	public static IReadOnlyList<Permissions> PermissionsFlags() =>
		new List<Permissions>(_permissionsTable.Keys);
	// Returns the human readable display string for the permission.
	public static string Description(this Permissions perms) =>
		_permissionsTable.ContainsKey(perms) ?
			_permissionsTable[perms] : "Unknown";

	// Returns the DiscordMember equivalent of the DiscordUser.
	// Returns null if the conversion wasn't possible.
	public static async Task<DiscordMember?> ToMember(this DiscordUser user) {
		// Check if trivially convertible.
		DiscordMember? member_n = user as DiscordMember;
		if (member_n is not null)
			return member_n;

		// Check if guild is loaded (to convert users with).
		if (Guild is null)
			return null;

		// Fetch the member by user ID.
		await UpdateGuild();
		try {
			DiscordMember member = await
				Guild.GetMemberAsync(user.Id);
			return member;
		} catch (ServerErrorException) {
			return null;
		}
	}

	// Fetches audit log entries, but wrapping the call in a
	// try/catch block to handle exceptions.
	public static async Task<DiscordAuditLogEntry?> LatestAuditLogEntry(
		this DiscordGuild guild,
		AuditLogActionType? type) {
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
