namespace Irene.Modules;

static class Chatbot {
	// Heuristic configs for various decisions.
	private const int
		_greetingCharLimit = 50;

	public static async Task RespondAsync(DiscordMessage message) {
		CheckErythroInit();
		DiscordUser irene = Erythro.Client.CurrentUser;
		string text = message.Content.Trim();

		List<DiscordUser> usersMentioned = new (message.MentionedUsers);
		if (usersMentioned.Contains(irene) &&
			text.Length < _greetingCharLimit
		) {
			if (IsGreeting(text)) {
				await TypeResponseAsync(message, ":wave: hello!");
				return;
			}
		}
	}

	private static async Task TypeResponseAsync(DiscordMessage message, string response) {
		await message.Channel.TriggerTypingAsync();
		await Task.Delay(1500);
	}

	private static bool IsGreeting(string text) {
		CheckErythroInit();

		// Assume greetings must be relatively short.
		text = text.Trim().ToLower();
		if (text.Length > _greetingCharLimit)
			return false;

		// Check through a list of "greeting-related" keywords.
		List<string> keywords = new () {
			"\U0001F44B",
			":wave:",
			"hello",
		};
		foreach (string keyword in keywords) {
			if (text.Contains(keyword))
				return true;
		}

		// Return false if no indications of a greeting were found.
		return false;
	}
}
