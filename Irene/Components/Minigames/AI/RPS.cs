using static Irene.Components.Minigames.RPS;

namespace Irene.Components.Minigames.AI;

static class RPS {
	public static async Task<Choice> NextChoice(ulong opponent_id) {
		// Select choice.
		Choice choice = (Choice)Random.Shared.Next(3);

		// Fuzzed delay.
		await Task.Delay(Random.Shared.Next(0, 1800));

		// Return result.
		return choice;
	}
}
