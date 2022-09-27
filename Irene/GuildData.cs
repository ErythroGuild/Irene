using System.Reflection;

namespace Irene;

class GuildData {
	// Public properties.
	public DiscordClient Client { get; }
	public DiscordGuild Guild { get; private set; }

	// Private data tables - these should only be updated via `PopulateData()`,
	// and accessed through the publically accessible methods.
	// `Channels` includes voice channels.
	private ConcurrentDictionary<ulong, DiscordChannel> _channels = new ();
	private ConcurrentDictionary<ulong, DiscordEmoji  > _emojis   = new ();
	private ConcurrentDictionary<ulong, DiscordRole   > _roles    = new ();

	// Constructors cannot be async, so `GuildData` object isn't returned
	// until static factory method also has a chance to initialize everything.
	public static async Task<GuildData> InitializeData(DiscordClient client) {
		DiscordGuild guild = await client.GetGuildAsync(Id_Erythro);
		GuildData data = new (client, guild);
		await data.PopulateData();
		return data;
	}

	// Newly-constructed `GuildData` objects are unusable--the static
	// factory method handles calling `PopulateData()` and only returns
	// valid `GuildData` instances.
	private GuildData(DiscordClient client, DiscordGuild guild) {
		Client = client;
		Guild = guild;
	}
	// This can be called to re-initialize data.
	public async Task PopulateData() {
		Log.Information("(Re-)populating guild data...");

		Guild = await Client.GetGuildAsync(Id_Erythro);

		List<FieldInfo> fields;
		// Helper function for listing fields.
		static List<FieldInfo> ListFields(Type type) =>
			new (type.GetFields());
		// Helper function for parsing (nullable) field values.
		static ulong FieldToId(FieldInfo field) {
			// `GetValue(null)` fetches static data.
			ulong? id = field.GetValue(null) as ulong?;
			return (id is null)
				? throw new ArgumentException("Could not parse ID from field.", nameof(field))
				: id.Value;
		}

		// Initialize `DiscordChannel` table.
		_channels = new ();
		fields = ListFields(typeof(ChannelIDs));
		foreach (FieldInfo field in fields) {
			ulong id = FieldToId(field);
			DiscordChannel channel = Guild.GetChannel(id);
			_channels.TryAdd(id, channel);
		}
		fields = ListFields(typeof(VoiceChatIDs));
		foreach (FieldInfo field in fields) {
			ulong id = FieldToId(field);
			DiscordChannel channel = Guild.GetChannel(id);
			_channels.TryAdd(id, channel);
		}

		// Initialize `DiscordEmoji` table.
		_emojis = new ();
		fields = ListFields(typeof(EmojiIDs));
		// Fetching entire list of emojis in bulk first, instead of
		// awaiting each emoji individually.
		List<DiscordEmoji> emojis = new (await Guild.GetEmojisAsync());
		foreach (FieldInfo field in fields) {
			ulong id = FieldToId(field);
			foreach (DiscordEmoji emoji in emojis) {
				if (emoji.Id == id) {
					_emojis.TryAdd(id, emoji);
					emojis.Remove(emoji);
					break;
				}
			}
		}

		// Initialize `DiscordRole` table.
		_roles = new ();
		fields = ListFields(typeof(RoleIDs));
		foreach (FieldInfo field in fields) {
			ulong id = FieldToId(field);
			DiscordRole role = Guild.GetRole(id);
			_roles.TryAdd(id, role);
		}

		Log.Debug("  Guild data populated.");
	}

	// Public access methods for data tables.
	// `Channels` includes voice channels.
	public DiscordChannel Channel(ulong id) =>
		_channels.ContainsKey(id)
			? _channels[id]
			: throw new ArgumentException("Unrecognized channel.", nameof(id));
	public DiscordEmoji Emoji(ulong id) =>
		_emojis.ContainsKey(id)
			? _emojis[id]
			: throw new ArgumentException("Unrecognized emoji.", nameof(id));
	public DiscordRole Role(ulong id) =>
		_roles.ContainsKey(id)
			? _roles[id]
			: throw new ArgumentException("Unrecognized role.", nameof(id));
}
