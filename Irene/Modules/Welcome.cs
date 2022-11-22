namespace Irene.Modules;

class Welcome {
	// A list of all pending welcome notifications / promises, indexed
	// by the ID of the user being greeted.
	private static readonly ConcurrentDictionary<ulong, TaskCompletionSource> _welcomePromises = new ();
	private static readonly ConcurrentDictionary<ulong, Task> _welcomeTasks = new ();

	private const string _pathMessage = @"data/welcome.txt";
	private static readonly TimeSpan _welcomeDelay = TimeSpan.FromSeconds(20);

	static Welcome() {
		Client.GuildMemberAdded += (client, e) => {
			_ = Task.Run(async () => {
				if (Erythro is null)
					throw new InvalidOperationException("Guild not initialized.");

				DiscordMember member = e.Member;
				ulong id = member.Id;

				// Notify recruitment officer.
				Log.Debug("  Notifying recruitment officer.");
				string notify =
					$"""
					{Erythro.Role(id_r.recruiter).Mention} -
					:star: New member {member.Mention} joined the server.
					""";
				DiscordChannel channel =
					Erythro.Channel(id_ch.officerBots);
				await channel.SendMessageAsync(notify);

				// Initialize welcome message.
				string welcome = await File.ReadAllTextAsync(_pathMessage);
				welcome = welcome.Unescape();

				// Send welcome message to new member.
				TaskCompletionSource welcomePromise = new ();
				_welcomePromises.TryAdd(id, welcomePromise);
				Task task = welcomePromise.Task.ContinueWith(
					async (t) => {
						_welcomePromises.TryRemove(id, out _);
						Log.Information("Sending welcome message to new member.");
						Log.Debug($"  {member.Tag()}");
						await member.SendMessageAsync(welcome);
						_welcomeTasks.TryRemove(id, out _);
					}
				);
				_welcomeTasks.TryAdd(id, task);

				// Delay the message slightly to feel more natural.
				_ = Task.Run(async () => {
					await Task.Delay(_welcomeDelay);
					welcomePromise.TrySetResult();
				});
			});
			return Task.CompletedTask;
		};
	}

	// Manually (immediately) trigger all remaining welcome tasks.
	public static async Task WelcomeRemainingAsync() {
		foreach (TaskCompletionSource promise in _welcomePromises.Values)
			promise.TrySetResult();
		await Task.WhenAll(_welcomeTasks.Values);
	}
}
