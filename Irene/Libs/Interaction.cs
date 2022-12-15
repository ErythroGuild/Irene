namespace Irene;

using System.Diagnostics;

using DiscordArg = DiscordInteractionDataOption;

// A wrapper class for DiscordInteraction that also handles some related
// functionality (e.g. timers).
class Interaction {
	// List of allowed events to register time points at.
	// These aren't required, e.g., none may be registered.
	public enum Events {
		InitialResponse,
		FinalResponse,
	}

	// --------
	// Properties, constructors, and basic access methods:
	// --------

	// Properties with backing fields.
	public DateTimeOffset TimeReceived { get; }
	public DiscordInteraction Object { get; }
	public DiscordMessage? TargetMessage { get; } = null;
	public DiscordUser? TargetUser { get; } = null;
	public string? ResponseSummary { get; private set; } = null;

	// Private properties.
	private Stopwatch Timer { get; }
	private ConcurrentDictionary<Events, TimeSpan> EventDurations { get; } = new ();

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
	// to be done before calling the actual constructor.
	// The alternative would be a shared Init() method, which is still
	// clunkier than just using factory methods.
	public static Interaction FromCommand(InteractionCreateEventArgs e) =>
		new (e.Interaction);
	public static Interaction FromContextMenu(ContextMenuInteractionCreateEventArgs e) =>
		e.Type switch {
			CommandType.MessageContextMenu =>
				new (e.Interaction, targetMessage: e.TargetMessage),
			CommandType.UserContextMenu =>
				new (e.Interaction, targetUser: e.TargetUser),
			_ => throw new UnclosedEnumException(typeof(CommandType), e.Type),
		};
	public static Interaction FromModal(ModalSubmitEventArgs e) =>
		new (e.Interaction);
	public static Interaction FromComponent(ComponentInteractionCreateEventArgs e) =>
		new (e.Interaction);

	// Methods relating to event time points.
	// RegisterEvent() overwrites any current events of that type.
	public void RegisterEvent(Events id) =>
		EventDurations[id] = Timer.Elapsed;
	public TimeSpan? GetEventDuration(Events id) =>
		EventDurations.TryGetValue(id, out TimeSpan duration)
			? duration
			: null;
	public DateTimeOffset? GetEventTime(Events id) =>
		EventDurations.TryGetValue(id, out TimeSpan duration)
			? (TimeReceived + duration)
			: null;

	public void RegisterInitialResponse() => RegisterEvent(Events.InitialResponse);
	public void RegisterFinalResponse() => RegisterEvent(Events.FinalResponse);

	// Methods relating to response summaries.
	public void SetResponseSummary(string summary) =>
		ResponseSummary = summary;
	public void ClearResponseSummary() =>
		ResponseSummary = null;

	// Convenience method for logging all the aggregated data for this
	// interaction.
	public void LogResponseData() {
		Log.Information(
			"Command processed:{FlagDM} /{CommandName}",
			Object.Channel.IsPrivate ? " [DM]" : "",
			Name
		);
		Log.Debug(
			"  Received: {TimestampReceived:HH:mm:ss.fff}",
			TimeReceived.ToLocalTime()
		);

		double? GetDurationMsec(Events eventType) =>
			GetEventDuration(eventType)
				?.TotalMilliseconds
				?? null;
		double? initialResponse = GetDurationMsec(Events.InitialResponse);
		if (initialResponse is not null) {
			Log.Debug(
				"  Initial response - {DurationInitial,6:F2} msec",
				initialResponse
			);
		}
		double? finalResponse = GetDurationMsec(Events.FinalResponse);
		if (finalResponse is not null) {
			Log.Debug(
				"  Final response   - {DurationFinal,6:F2} msec",
				finalResponse
			);
		}

		if (ResponseSummary is not null) {
			Log.Debug("  Response summary:");
			string[] lines = ResponseSummary.Split("\n");
			foreach (string line in lines)
				Log.Debug("    {ResponseLine}", line);
		}
	}
	
	
	// --------
	// Convenience methods for responding to interactions:
	// --------

	// Convenience method for responding to a component interaction
	// initiated by an unintended user, with a followup (ephemeral)
	// error message explaining what happened.
	public async Task RespondComponentNotOwned(DiscordUser owner) {
		await DeferComponentAsync();
		string message =
			$":confused: Sorry, only {owner.Mention} can use this.";
		await FollowupAsync(message, true);
	}

	// Convenience methods for responding to a command, and registering
	// a final response at the same time.
	public Task RegisterAndRespondAsync(
		string message,
		bool isEphemeral=false
	) =>
		RegisterAndRespondAsync(message, message, isEphemeral);
	public async Task RegisterAndRespondAsync(
		string message,
		string summary,
		bool isEphemeral = false
	) {
		RegisterFinalResponse();
		await RespondCommandAsync(message, isEphemeral);
		SetResponseSummary(summary);
	}
	public async Task RegisterAndRespondAsync(
		DiscordMessageBuilder message,
		string summary,
		bool isEphemeral=false
	) {
		RegisterFinalResponse();
		await RespondCommandAsync(message, isEphemeral);
		SetResponseSummary(summary);
	}
	// Convenience method for deferring a command, and registering an
	// initial response at the same time.
	public async Task RegisterAndDeferAsync(bool isEphemeral=false) {
		RegisterInitialResponse();
		await DeferCommandAsync(isEphemeral);
	}

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
	public Task RespondCommandAsync(string message, bool isEphemeral=false) {
		DiscordMessageBuilder messageBuilder =
			new DiscordMessageBuilder()
			.WithContent(message);
		return RespondCommandAsync(messageBuilder, isEphemeral);
	}
	public Task RespondCommandAsync(DiscordMessageBuilder message, bool isEphemeral=false) =>
		Object.CreateResponseAsync(
			InteractionResponseType.ChannelMessageWithSource,
			new DiscordInteractionResponseBuilder(message)
				.AsEphemeral(isEphemeral)
		);
	public Task DeferCommandAsync(bool isEphemeral=false) =>
		Object.DeferAsync(isEphemeral);

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
	public Task<DiscordMessage> FollowupAsync(string message, bool isEphemeral=false) {
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithContent(message);
		return FollowupAsync(response, isEphemeral);
	}
	public Task<DiscordMessage> FollowupAsync(IDiscordMessageBuilder message, bool isEphemeral=false) =>
		FollowupAsync(new DiscordFollowupMessageBuilder(message), isEphemeral);
	public Task<DiscordMessage> FollowupAsync(DiscordFollowupMessageBuilder message, bool isEphemeral=false) =>
		Object.CreateFollowupMessageAsync(message.AsEphemeral(isEphemeral));

	// Methods for manipulating responses/followups.
	public Task<DiscordMessage> GetResponseAsync() =>
		Object.GetOriginalResponseAsync();
	public Task<DiscordMessage> GetFollowupAsync(ulong id) =>
		Object.GetFollowupMessageAsync(id);
	public Task DeleteResponseAsync() =>
		Object.DeleteOriginalResponseAsync();
	public Task DeleteFollowupAsync(ulong id) =>
		Object.DeleteFollowupMessageAsync(id);
	public Task<DiscordMessage> EditResponseAsync(string message) {
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithContent(message);
		return EditResponseAsync(response);
	}
	public Task<DiscordMessage> EditResponseAsync(IDiscordMessageBuilder message) =>
		EditResponseAsync(new DiscordWebhookBuilder(message));
	public Task<DiscordMessage> EditResponseAsync(DiscordWebhookBuilder message) =>
		Object.EditOriginalResponseAsync(message);


	// --------
	// Convenience methods for accessing response data:
	// --------

	// Data relating to command args.
	public static IList<DiscordArg> GetArgs(Interaction interaction) =>
		GetArgs(interaction.Data);
	public static IList<DiscordArg> GetArgs(DiscordInteractionData data) =>
		(data.Options is not null)
			? new List<DiscordArg>(data.Options)
			: new List<DiscordArg>();
	public static IList<DiscordArg> GetArgs(DiscordArg subcommand) =>
		(subcommand.Options is not null)
			? new List<DiscordArg>(subcommand.Options)
			: new List<DiscordArg>();

	public static ParsedArgs UnpackArgs(Interaction interaction) =>
		UnpackArgs(GetArgs(interaction));
	public static ParsedArgs UnpackArgs(IList<DiscordArg> args) {
		Dictionary<string, object> table = new ();
		foreach (DiscordArg arg in args)
			table.Add(arg.Name, arg.Value);
		return table;
	}

	public static DiscordArg? GetFocusedArg(Interaction interaction) =>
		GetFocusedArg(GetArgs(interaction));
	public static DiscordArg? GetFocusedArg(IList<DiscordArg> args) {
		foreach (DiscordArg arg in args) {
			if (arg.Focused)
				return arg;
		}
		return null;
	}

	// Data relating to modals.
	public IReadOnlyDictionary<string, DiscordComponent> GetModalData() { 
		Dictionary<string, DiscordComponent> components = new ();
		foreach (DiscordComponentRow row in Data.Components) {
			foreach (DiscordComponent component in row.Components) {
				if (component.Type is ComponentType.FormInput)
					components.Add(component.CustomId, component);
			}
		}
		return new Dictionary<string, DiscordComponent>(components);
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
