namespace Irene;

// A wrapper class for DiscordInteraction that also handles some related
// functionality (e.g. timers).
class Interaction {
	// List of allowed events to register time points at.
	// These aren't required, e.g., none may be registered.
	public enum Events {
		InitialResponse,
		FinalResponse,
	}

	// Properties with backing fields.
	public DateTimeOffset TimeReceived { get; }
	public Dictionary<Events, TimeSpan> TimeOffsets { get; } = new ();
	public DiscordInteraction Object { get; }

	// Calculated properties.
	// These are provided as syntax sugar for common properties.
	public InteractionType Type { get => Object.Type; }
	public string Name { get => Object.Data.Name; }
	public string CustomId { get => Object.Data.CustomId; }
	public DiscordUser User { get => Object.User; }
	public DiscordInteractionData Data { get => Object.Data; }

	// Timer is automatically managed and doesn't need to be public.
	private Stopwatch Timer { get; }
	// Constructor is hidden, forcing usage of static factory method--
	// this implies that the initialization has side effects.
	private Interaction(DiscordInteraction interaction) {
		Timer = Stopwatch.StartNew();
		TimeReceived = DateTimeOffset.UtcNow;
		Object = interaction;
	}

	// Public factory constructor.
	public static Interaction Register(DiscordInteraction interaction) =>
		new (interaction);

	// Methods relating to event time points.
	// RegisterEvent() overwrites any current events of that type.
	public void RegisterEvent(Events id) {
		TimeOffsets[id] = Timer.Elapsed;
	}
	public TimeSpan GetEventDuration(Events id) => TimeOffsets[id];
	public DateTimeOffset GetEventTime(Events id) =>
		TimeReceived + TimeOffsets[id];



	// Convenience methods for responding to interactions.
	
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
			list.Add(new(pair.Item1, pair.Item2));

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
			new DiscordInteractionResponseBuilder(message)
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
}
