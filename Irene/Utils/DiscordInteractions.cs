using Irene.Commands;

namespace Irene.Utils;

static partial class Util {
	// Convenience method for getting the target user of a user command.
	// A list of users is provided, but there should only be one.
	// (This method fetches the first one found.)
	public static DiscordMember GetTargetMember(this DiscordInteraction interaction) =>
		new List<DiscordMember>(interaction.Data.Resolved.Members.Values)[0];

	// Convenience method for fetching command options.
	public static List<DiscordInteractionDataOption> GetArgs(this DeferrerHandler handler) =>
		handler.Interaction.GetArgs();
	public static List<DiscordInteractionDataOption> GetArgs(this TimedInteraction interaction) =>
		interaction.Interaction.GetArgs();
	public static List<DiscordInteractionDataOption> GetArgs(this DiscordInteraction interaction) =>
		(interaction.Data.Options is not null)
			? new (interaction.Data.Options)
			: new ();
	public static List<DiscordInteractionDataOption> GetArgs(this DiscordInteractionDataOption option) =>
		(option.Options is not null)
			? new (option.Options)
			: new ();

	public static object? GetArg(this List<DiscordInteractionDataOption> options, string name) {
		foreach (DiscordInteractionDataOption option in options) {
			if (option.Name == name)
				return option.Value;
		}
		return null;
	}

	// Convenience method for fetching modal field values.
	public static Dictionary<string, TextInputComponent> GetModalComponents(this DiscordInteraction interaction) {
		Dictionary<string, TextInputComponent> components = new ();
		foreach (DiscordActionRowComponent actionRow in interaction.Data.Components) {
			foreach (DiscordComponent component in actionRow.Components) {
				if (component is TextInputComponent text_component)
					components.Add(component.CustomId, text_component);
			}
		}
		return components;
	}

	// Convenience methods for responding to interactions.
	public static Task DeferMessageAsync(
		this DiscordInteraction interaction,
		bool isEphemeral=false
	) =>
		interaction.CreateResponseAsync(
			InteractionResponseType.DeferredChannelMessageWithSource,
			new DiscordInteractionResponseBuilder()
				.AsEphemeral(isEphemeral)
		);
	public static Task UpdateMessageAsync(
		this DiscordInteraction interaction,
		string message
	) {
		DiscordWebhookBuilder builder =
			new DiscordWebhookBuilder()
			.WithContent(message);
		return interaction.EditOriginalResponseAsync(builder);
	}
	public static Task DeleteMessageAsync(
		this DiscordInteraction interaction
	) =>
		interaction.DeleteOriginalResponseAsync();

	public static Task AcknowledgeComponentAsync(
		this DiscordInteraction interaction
	) =>
		interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

	public static Task AutoCompleteResultsAsync(
		this TimedInteraction interaction,
		IReadOnlyList<string> choices
	) =>
		interaction.Interaction.AutoCompleteResultsAsync(choices);
	public static Task AutoCompleteResultsAsync(
		this DiscordInteraction interaction,
		IReadOnlyList<string> choices
	) {
		// Convert list of strings to list of discord choices.
		List<DiscordAutoCompleteChoice> choices_discord = new ();
		foreach (string choice in choices)
			choices_discord.Add(new (choice, choice));

		return interaction.CreateResponseAsync(
			InteractionResponseType.AutoCompleteResult,
			new DiscordInteractionResponseBuilder()
				.AddAutoCompleteChoices(choices_discord)
		);
	}

	// Syntax sugar for getting the access level of the user who invoked
	// the interaction.
	public static async Task<AccessLevel> AccessLevel(this TimedInteraction interaction) =>
		await Command.GetAccessLevel(interaction.Interaction);
	public static AccessLevel AccessLevel(this DiscordMember member) =>
		Command.GetAccessLevel(member);
	// Syntax sugar for checking if the user who invoked the interaction
	// has the specified access level.
	public static async Task<bool> HasAccess(this TimedInteraction interaction, AccessLevel level) {
		AccessLevel level_user = await
			Command.GetAccessLevel(interaction.Interaction);
		return level_user >= level;
	}
	public static bool HasAccess(this DiscordMember member, AccessLevel level) {
		AccessLevel level_user =
			Command.GetAccessLevel(member);
		return level_user >= level;
	}
	// Checks if the user who invoked the interaction has the specified
	// access level, logs, then sends a response.
	public static async Task<bool> CheckAccessAsync(
		this DeferrerHandler handler,
		AccessLevel level
	) =>
		await handler.Interaction.CheckAccessAsync(handler.IsDeferrer, level);
	public static async Task<bool> CheckAccessAsync(
		this TimedInteraction interaction,
		bool isDeferrer,
		AccessLevel level
	) {
		bool hasAccess = await interaction.HasAccess(level);
		if (hasAccess) {
			return true;
		} else {
			if (isDeferrer) {
				await Command.DeferAsync(interaction, true);
				return false;
			}
			string response =
				$"Sorry, you don't have the permissions ({level}) to run that command.\n" +
				$"See `/help {interaction.Interaction.Data.Name}` for more info.";
			await Command.SubmitResponseAsync(
				interaction,
				response,
				"Access denied (insufficient permissions).",
				LogLevel.Information,
				"Information sent. ({Level} required)".AsLazy(),
				level
			);
			return false;
		}
	}
}
