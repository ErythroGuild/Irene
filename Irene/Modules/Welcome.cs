namespace Irene.Modules;

static class Welcome {
	private const string
		_pathMessage = @"data/welcome.txt",
		_urlRuleOne  = @"https://imgur.com/jxWTK8r",
		_urlMascot   = @"https://imgur.com/5pKJdPh";

	// Force static initializer to run.
	public static void Init() { }
	static Welcome() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		Client.GuildMemberAdded += (irene, e) => {
			_ = Task.Run(async () => {
				DiscordMember member = e.Member;

				// Initialize welcome message.
				StreamReader data = File.OpenText(_pathMessage);
				string welcome = data.ReadToEnd();
				data.Close();

				// Send welcome message to new member.
				Log.Information("Sending welcome message to new member.");
				Log.Debug($"  {member.Tag()}");
				await member.SendMessageAsync(_urlRuleOne);
				await member.SendMessageAsync(welcome);
				await member.SendMessageAsync(_urlMascot);

				// Notify recruitment officer.
				if (Guild is not null) {
					Log.Debug("  Notifying recruitment officer.");
					string text = $"{Roles[id_r.recruiter].Mention} - " +
						$"New member {e.Member.Mention} joined the server. :tada:";
					await Channels[id_ch.officerBots].SendMessageAsync(text);
				}
			});
			return Task.CompletedTask;
		};

		Log.Information("  Initialized module: Welcome");
		Log.Debug("    Registered welcome message handler.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}
}
