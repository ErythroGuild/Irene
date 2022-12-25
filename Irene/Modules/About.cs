namespace Irene.Modules;

using System.Diagnostics;

class About {
	public enum Status { Good, Idle, Error }

	public static readonly string
		StringVersion,
		StringBuild;
	public static int SlashCommandCount   { get; private set; } = 0;
	public static int ContextCommandCount { get; private set; } = 0;

	// Private fields / constants.
	private const string
		_pathVersion = @"config/tag.txt",
		_pathBuild   = @"config/commit.txt";
	private static readonly DiscordColor
		_colorOnline = new ("#57F287"),
		_colorIdle   = new ("#FEE75C"),
		_colorBusy   = new ("#ED4245");
	private const double
		_memoryLowerLimit = 300.0,
		_memoryUpperLimit = 360.0;
	private const ulong _idMaintainer = 165557736287764483;
	private const string
		_linkSourceCode      = @"https://github.com/ErythroGuild/irene",
		_linkAcknowledgments = @"https://github.com/ErythroGuild/irene/blob/master/Acknowledgments.md";
	private const string
		_charSpaceN = "\u2002",     // en space
		_charSpaceM = "\u2003",     // em space
		_charBullet = "\u2022",     // black bullet
		_charLove   = "\U0001F49C", // :purple_heart:
		_charHeart  = "\U0001FAF6"; // :heart_hands:

	// Version and build strings can be initialized in the static constructor,
	// since they cannot change without restarting the process.
	static About() {
		// No need to lock file because static constructors run sequentially.
		StreamReader file;

		file = File.OpenText(_pathVersion);
		StringVersion = file.ReadLine() ?? "";
		file.Close();

		file = File.OpenText(_pathBuild);
		StringBuild = file.ReadLine() ?? "";
		if (StringBuild.Length > 7)
			StringBuild = StringBuild[..7];
		file.Close();
	}

	// This is the main function that collates and returns a formatted
	// `DiscordEmbed`.
	public static DiscordEmbed CollateStatusEmbed() {
		CheckErythroInit();

		string title = $"__**Irene {StringVersion}**__{_charSpaceM}build `{StringBuild}`";

		// Change embed color depending on current user status.
		DiscordColor color = GetBotStatus() switch {
				UserStatus.Online => _colorOnline,
				UserStatus.Idle   => _colorIdle,
				UserStatus.DoNotDisturb => _colorBusy,
				_ => _colorBusy,
			};

		string statusAvailableCommands =
			StatusCircle(GetAvailableCommandsStatus());
		string helpLink =
			Dispatcher.Table[Commands.Help.CommandHelp]
			.Command
			.Mention(Commands.Help.CommandHelp);
		string statusMemoryUsage =
			StatusCircle(GetMemoryUsageStatus());

		string bodyText =
			$"""
			{Erythro.Emoji(id_e.erythro)} **<Erythro>**'s community admin bot.

			{statusAvailableCommands} **Available commands:** {SlashCommandCount} [+{ContextCommandCount}], {helpLink}
			{statusMemoryUsage} **Memory usage:** {GetMemoryUsageMB():F0} MB

			Maintained by {GetMaintainerMention()} with {_charLove}
			[Source Code]({_linkSourceCode}){_charSpaceN}{_charHeart}{_charSpaceN}[Acknowledgments]({_linkAcknowledgments})
			{_charBullet} *Hosted by Linode* {_charBullet}
			""";

		string uptime = @$"uptime: {GetUptime():d\d\ h\h\ m\m}";

		return new DiscordEmbedBuilder()
			.WithTitle(title)
			.WithColor(color)
			.WithDescription(bodyText)
			.WithFooter(uptime)
			.Build();
	}

	// Fetch various kinds of status information.
	public static UserStatus GetBotStatus() =>
		Erythro?.Client.CurrentUser.Presence.Status
			?? throw new UninitializedException();
	// Returns MB (1000 kB).
	public static double GetMemoryUsageMB() {
		Process irene = Process.GetCurrentProcess();
		irene.Refresh();

		GC.Collect(2, GCCollectionMode.Aggressive);
		GC.WaitForPendingFinalizers();
		GC.Collect(2, GCCollectionMode.Aggressive);

		//long bytes = irene.PrivateMemorySize64;
		long bytes = irene.WorkingSet64;
		// bytes -> kilobytes -> megabytes
		double megabytes = (double)bytes / 1000 / 1000;
		// This gives a more conservative (i.e. higher) result.
		return megabytes;
	}
	public static string GetMaintainerMention() =>
		_idMaintainer.MentionUserId();
	public static TimeSpan GetUptime() {
		DateTimeOffset startTime = Process.GetCurrentProcess()
			.StartTime
			.ToUniversalTime();
		return DateTimeOffset.UtcNow - startTime;
	}

	// Status indication methods.
	public static Status GetMemoryUsageStatus() {
		double usage = GetMemoryUsageMB();
		return usage switch {
			<_memoryLowerLimit => Status.Good,
			<_memoryUpperLimit => Status.Idle,
			_ => Status.Error,
		};
	}
	public static Status GetAvailableCommandsStatus() {
		int slashCount = SlashCommandCount;
		int contextCount = ContextCommandCount;
		int registeredCount = slashCount + contextCount;
		// The table is the more direct (efficient) representation.
		int definedCount = Dispatcher.Table.Count;

		if (registeredCount == definedCount) {
			return (definedCount == 0)
				? Status.Idle
				: Status.Good;
		}

		if (registeredCount < definedCount) {
			return (registeredCount == 0)
				? Status.Error
				: Status.Idle;
		}

		return Status.Error;
	}

	// Public methods for setting status information.
	public static void SetRegisteredCommands(int slashCount, int contextCount) {
		SlashCommandCount = slashCount;
		ContextCommandCount = contextCount;
	}

	// Private helper methods.
	private static string StatusCircle(Status status) => status switch {
		Status.Good  => "\U0001F7E2", // :green_circle:
		Status.Idle  => "\U0001F7E1", // :yellow_circle:
		Status.Error => "\U0001F534", // :red_circle:
		_ => "\u26AA", // :white_circle:
	};
}
