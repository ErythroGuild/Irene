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

}
