namespace Irene;

// A wrapper class for DiscordInteraction that also handles some related
// functionality (e.g. timers).
class Interaction {
	// --------
	// Properties, constructors, and basic access methods:
	// --------

	// List of allowed events to register time points at.
	// These aren't required, e.g., none may be registered.
	public enum Events {
		InitialResponse,
		FinalResponse,
	}

	// Properties with backing fields.
	public DateTimeOffset TimeReceived { get; }
	public DiscordInteraction Object { get; }
	public DiscordMessage? TargetMessage { get; } = null;
	public DiscordUser? TargetUser { get; } = null;

	// Private properties.
	private Stopwatch Timer { get; }
	private ConcurrentDictionary<Events, TimeSpan> TimeOffsets { get; } = new ();

	// Calculated properties.
	// These are provided as syntax sugar for common properties.
	public InteractionType Type => Object.Type;
	public string Name => Object.Data.Name;
	public string CustomId => Object.Data.CustomId;
	public IList<string> Values => new List<string>(Object.Data.Values);
	public DiscordUser User => Object.User;
	public DiscordInteractionData Data => Object.Data;

	private Interaction(
		DiscordInteraction interaction,
		DiscordMessage? targetMessage=null,
		DiscordUser? targetUser=null
	) {
		Timer = Stopwatch.StartNew();
		TimeReceived = DateTimeOffset.UtcNow;
		Object = interaction;
		TargetMessage = targetMessage;
		TargetUser = targetUser;
	}

	// Public factory constructors.
	// These cannot be instance methods because some processing needs
	// to be done before calling the actual constructor, and that isn't
	// allowed in C#.
	// The alternative would be a shared Init() method, which is still
	// clunkier than just using factory methods.
	public static Interaction FromCommand(InteractionCreateEventArgs e) =>
		new (e.Interaction);
	public static Interaction FromContextMenu(ContextMenuInteractionCreateEventArgs e) =>
		e.Type switch {
			ApplicationCommandType.MessageContextMenu =>
				new (e.Interaction, targetMessage: e.TargetMessage),
			ApplicationCommandType.UserContextMenu =>
				new (e.Interaction, targetUser: e.TargetUser),
			_ => throw new ArgumentException("Event args must be a context menu interaction.", nameof(e)),
		};
	public static Interaction FromModal(ModalSubmitEventArgs e) =>
		new (e.Interaction);
	public static Interaction FromComponent(ComponentInteractionCreateEventArgs e) =>
		new (e.Interaction);

	// Methods relating to event time points.
	// RegisterEvent() overwrites any current events of that type.
	public void RegisterEvent(Events id) {
		TimeOffsets[id] = Timer.Elapsed;
	}
	public TimeSpan? GetEventDuration(Events id) =>
		TimeOffsets.ContainsKey(id)
			? TimeOffsets[id]
			: null;
	public DateTimeOffset? GetEventTime(Events id) =>
		TimeOffsets.ContainsKey(id)
			? (TimeReceived + TimeOffsets[id])
			: null;


	// --------
	// Convenience methods for responding to interactions:
	// --------

	// Responses to autocomplete interactions:
	public Task AutocompleteAsync(IList<(string, string)> choices) =>
		AutocompleteAsync<string>(choices);
	public Task AutocompleteAsync(IList<(string, int)> choices) =>
		AutocompleteAsync<int>(choices);
	// This method does not check for the choices having valid types.
	// The caller must ensure `T` is either `string` or `int`.
	private Task AutocompleteAsync<T>(IList<(string, T)> pairs) {
		// Create list of choice objects.
		List<DiscordAutoCompleteChoice> list = new ();
		foreach ((string, T) pair in pairs)
			list.Add(new (pair.Item1, pair.Item2));

		// Create interaction response object.
		DiscordInteractionResponseBuilder builder = new ();
		builder.AddAutoCompleteChoices(list);

		return Object.CreateResponseAsync(
			InteractionResponseType.AutoCompleteResult,
			builder
		);
	}

	// Responses to command interactions:
	public Task RespondCommandAsync(DiscordMessageBuilder message, bool isEphemeral=false) =>
		Object.CreateResponseAsync(
			InteractionResponseType.ChannelMessageWithSource,
			new DiscordInteractionResponseBuilder(message)
				.AsEphemeral(isEphemeral)
		);
	public Task DeferCommandAsync(bool isEphemeral=false) =>
		Object.CreateResponseAsync(
			InteractionResponseType.DeferredChannelMessageWithSource,
			new DiscordInteractionResponseBuilder()
				.AsEphemeral(isEphemeral)
		);

	// Responses to component interactions:
	public Task UpdateComponentAsync(DiscordMessageBuilder message) =>
		Object.CreateResponseAsync(
			InteractionResponseType.UpdateMessage,
			new (message)
		);
	public Task DeferComponentAsync() =>
		Object.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

	// Responses to either command or component interactions:
	public Task RespondModalAsync(DiscordInteractionResponseBuilder modal) =>
		Object.CreateResponseAsync(InteractionResponseType.Modal, modal);
	public Task<DiscordMessage> FollowupAsync(DiscordFollowupMessageBuilder builder) =>
		Object.CreateFollowupMessageAsync(builder);

	// Methods for manipulating responses/followups.
	public Task<DiscordMessage> GetResponseAsync() =>
		Object.GetOriginalResponseAsync();
	public Task<DiscordMessage> GetFollowupAsync(ulong id) =>
		Object.GetFollowupMessageAsync(id);
	public Task DeleteResponseAsync() =>
		Object.DeleteOriginalResponseAsync();
	public Task DeleteFollowupAsync(ulong id) =>
		Object.DeleteFollowupMessageAsync(id);
	public Task<DiscordMessage> EditResponseAsync(DiscordWebhookBuilder message) =>
		Object.EditOriginalResponseAsync(message);


	// --------
	// Convenience methods for accessing response data:
	// --------

	// Data relating to command args.
	public static IList<DiscordInteractionDataOption> GetArgs(Interaction interaction) =>
		GetArgs(interaction.Data);
	public static IList<DiscordInteractionDataOption> GetArgs(DiscordInteractionData data) =>
		(data.Options is not null)
			? new List<DiscordInteractionDataOption>(data.Options)
			: new List<DiscordInteractionDataOption>();
	public static IList<DiscordInteractionDataOption> GetArgs(DiscordInteractionDataOption subcommand) =>
		(subcommand.Options is not null)
			? new List<DiscordInteractionDataOption>(subcommand.Options)
			: new List<DiscordInteractionDataOption>();

	public static IDictionary<string, object> UnpackArgs(Interaction interaction) =>
		UnpackArgs(GetArgs(interaction));
	public static IDictionary<string, object> UnpackArgs(IList<DiscordInteractionDataOption> args) {
		Dictionary<string, object> table = new ();
		foreach (DiscordInteractionDataOption arg in args)
			table.Add(arg.Name, arg.Value);
		return table;
	}

	public static DiscordInteractionDataOption? GetFocusedArg(Interaction interaction) =>
		GetFocusedArg(GetArgs(interaction));
	public static DiscordInteractionDataOption? GetFocusedArg(IList<DiscordInteractionDataOption> args) {
		foreach (DiscordInteractionDataOption arg in args) {
			if (arg.Focused)
				return arg;
		}
		return null;
	}

	// Data relating to modals.
	public IReadOnlyDictionary<string, DiscordComponent> GetModalData() { 
		Dictionary<string, DiscordComponent> components = new ();
		foreach (DiscordActionRowComponent row in Data.Components) {
			foreach (DiscordComponent component in row.Components) {
				if (component.Type is ComponentType.FormInput or ComponentType.Select)
					components.Add(component.CustomId, component);
			}
		}
		return new ReadOnlyDictionary<string, DiscordComponent>(components);
	}

	// Resolved data.
	public DiscordAttachment? ResolveAttachment(ulong id) =>
		Data.Resolved.Attachments.ContainsKey(id)
			? Data.Resolved.Attachments[id]
			: null;
	public DiscordChannel? ResolveChannel(ulong id) =>
		Data.Resolved.Channels.ContainsKey(id)
			? Data.Resolved.Channels[id]
			: null;
	public DiscordMember? ResolveMember(ulong id) =>
		Data.Resolved.Members.ContainsKey(id)
			? Data.Resolved.Members[id]
			: null;
	public DiscordMessage? ResolveMessage(ulong id) =>
		Data.Resolved.Messages.ContainsKey(id)
			? Data.Resolved.Messages[id]
			: null;
	public DiscordRole? ResolveRole(ulong id) =>
		Data.Resolved.Roles.ContainsKey(id)
			? Data.Resolved.Roles[id]
			: null;
	public DiscordUser? ResolveUser(ulong id) =>
		Data.Resolved.Users.ContainsKey(id)
			? Data.Resolved.Users[id]
			: null;
}
