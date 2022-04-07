namespace Irene.Utils;

static partial class Util {
	// Convenience method for fetching command options.
	public static List<DiscordInteractionDataOption> GetArgs(this DiscordInteraction interaction) =>
		(interaction.Data.Options is not null)
			? new (interaction.Data.Options)
			: new ();

	// Convenience functions for responding to interactions.
	public static Task RespondMessageAsync(this DiscordInteraction interaction, string message, bool isEphemeral=false) =>
		interaction.CreateResponseAsync(
			InteractionResponseType.ChannelMessageWithSource,
			new DiscordInteractionResponseBuilder(
				new DiscordMessageBuilder().WithContent(message)
			).AsEphemeral(isEphemeral)
		);

	public static Task AutoCompleteResultsAsync(
		this DiscordInteraction interaction,
		List<string> choices
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
	public static async Task<AccessLevel> AccessLevel(this DiscordInteraction interaction) =>
		await Command.GetAccessLevel(interaction);
	// Syntax sugar for checking if the user who invoked the interaction
	// has the specified access level.
	public static async Task<bool> HasAccess(this DiscordInteraction interaction, AccessLevel level) {
		AccessLevel level_user = await Command.GetAccessLevel(interaction);
		return level_user >= level;
	}

	// Checks if the user who invoked the interaction has the specified
	// access level, logs, then sends a response.
	public static async Task<bool> CheckAccessAsync(
		this DiscordInteraction interaction,
		Stopwatch stopwatch,
		AccessLevel level
	) {
		bool hasAccess = await interaction.HasAccess(level);
		if (hasAccess) {
			return true;
		} else {
			Log.Information("  Access denied (insufficient permissions).");
			stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
			string response =
				$"Sorry, you don't have the permissions ({level}) to run that command.\n" +
				$"See `/help {interaction.Data.Name}` for more info.";
			await interaction.RespondMessageAsync(response, true);
			Log.Information("  Response sent.");
			return false;
		}
	}
}
