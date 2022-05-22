namespace Irene.Components.Minigames.AI;

static class Morra {
	public static async Task<(int, int)> NextChoiceGuess(ulong opponent_id) {
		// Select choice.
		int choice = Random.Shared.Next(1, 4);
		int guess = choice + Random.Shared.Next(1, 4);

		// Fuzzed delay.
		await Task.Delay(Random.Shared.Next(0, 1800));

		// Return result.
		return (choice, guess);
	}
}
