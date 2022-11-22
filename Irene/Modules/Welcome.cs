namespace Irene.Modules;

class Welcome {
	private const string _pathMessage = @"data/welcome.txt";

	static Welcome() {
		Client.GuildMemberAdded += (client, e) => {
			_ = Task.Run(async () => {
				if (Erythro is null)
					throw new InvalidOperationException("Guild not initialized.");

				DiscordMember member = e.Member;

				// Initialize welcome message.
				string welcome = await File.ReadAllTextAsync(_pathMessage);
				welcome = welcome.Unescape();

				// Send welcome message to new member.
				Log.Information("Sending welcome message to new member.");
				Log.Debug($"  {member.Tag()}");
				await member.SendMessageAsync(welcome);

				// Notify recruitment officer.
				Log.Debug("  Notifying recruitment officer.");
				string notify =
					$"""
					{Erythro.Role(id_r.recruiter).Mention} -
					New member {member.Mention} joined the server. :star:
					""";
				DiscordChannel channel =
					Erythro.Channel(id_ch.officerBots);
				await channel.SendMessageAsync(notify);
			});
			return Task.CompletedTask;
		};
	}
}
