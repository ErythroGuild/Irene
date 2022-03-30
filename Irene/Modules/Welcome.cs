namespace Irene.Modules;

static class Welcome {
	const string path_message = @"data/welcome.txt";
	const string url_mascot = @"https://imgur.com/5pKJdPh";
	const ulong ch_notify = id_ch.officerBots;
	const ulong r_recruiter = id_r.recruiter;

	// Force static initializer to run.
	public static void init() { return; }

	static Welcome() {
		Client.GuildMemberAdded += (irene, e) => {
			_ = Task.Run(async () => {
				DiscordMember member = e.Member;

				// Initialize welcome message.
				StreamReader data = File.OpenText(path_message);
				string welcome = data.ReadToEnd();
				data.Close();

				// Send welcome message to new member.
				log.info("Sending welcome message to new member.");
				log.debug($"  {member.Tag()}");
				await member.SendMessageAsync(welcome);
				await member.SendMessageAsync(url_mascot);

				// Notify recruitment officer.
				if (Guild is not null) {
					log.debug("  Notifying recruitment officer.");
					StringWriter text = new ();
					text.WriteLine($"{roles[r_recruiter].Mention} - " +
						$"New member {e.Member.Mention} joined the server. :tada:");
					_ = channels[ch_notify].SendMessageAsync(text.ToString());
				}

				log.endl();
			});
			return Task.CompletedTask;
		};
	}
}
