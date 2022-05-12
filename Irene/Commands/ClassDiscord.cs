using static Irene.ClassSpec;

namespace Irene.Commands;

class ClassDiscord : AbstractCommand, IInit {
	private static readonly ReadOnlyDictionary<Class, string> _options =
		new (new ConcurrentDictionary<Class, string>() {
			[Class.DK] = "death-knight",
			[Class.DH] = "demon-hunter",
			[Class.Druid  ] = "druid"  ,
			[Class.Hunter ] = "hunter" ,
			[Class.Mage   ] = "mage"   ,
			[Class.Monk   ] = "monk"   ,
			[Class.Paladin] = "paladin",
			[Class.Priest ] = "priest" ,
			[Class.Rogue  ] = "rogue"  ,
			[Class.Shaman ] = "shaman" ,
			[Class.Warlock] = "warlock",
			[Class.Warrior] = "warrior",
		});
	private static readonly ReadOnlyDictionary<Class, string> _invites =
		new (new ConcurrentDictionary<Class, string>() {
			[Class.DK     ] = @"https://discord.gg/acherus"       ,
			[Class.DH     ] = @"https://discord.gg/felhammer"     ,
			[Class.Druid  ] = @"https://discord.gg/dreamgrove"    ,
			[Class.Hunter ] = @"https://discord.gg/trueshot"      ,
			[Class.Mage   ] = @"https://discord.gg/makGfZA"       ,
			[Class.Monk   ] = @"https://discord.gg/peakofserenity",
			[Class.Paladin] = @"https://discord.gg/hammerofwrath" ,
			[Class.Rogue  ] = @"https://discord.gg/ravenholdt"    ,
			[Class.Warlock] = @"https://discord.gg/blackharvest"  ,
			[Class.Warrior] = @"https://discord.gg/skyhold"       ,

			[Class.Priest] = @"https://discord.gg/warcraftpriests" + "\n" + @"https://discord.gg/focusedwill (disc-only)",
			[Class.Shaman] = @"https://discord.gg/earthshrine"     + "\n" + @"https://discord.gg/AcTek6e (resto-only)"   ,
		});
	private static readonly ReadOnlyDictionary<string, string> _optionsToInvites;

	public static void Init() { }
	static ClassDiscord() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		ConcurrentDictionary<string, string> optionsToInvites = new ();
		foreach (Class @class in _options.Keys)
			optionsToInvites.TryAdd(_options[@class], _invites[@class]);
		_optionsToInvites = new (optionsToInvites);

		Log.Information("  Initialized command: /class-discord");
		Log.Debug("    Class discord invite cache initialized.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	public override List<string> HelpPages =>
		new () { string.Join("\n", new List<string> {
			"`/class-discord <class>` displays the class discord invite.",
			"Multiple invites are given if available."
		} ) };

	public override List<InteractionCommand> SlashCommands { get {
		// Compile list of all options.
		List<CommandOptionEnum> options = new ();
		foreach (Class @class in _options.Keys)
			options.Add(new (@class.Name(), _options[@class]));

		// Construct slash command object.
		return new () {
			new ( new (
				"class-discord",
				"Get the invite link for a class discord server.",
				new List<CommandOption> { new (
					"class",
					"The class to get an invite for.",
					ApplicationCommandOptionType.String,
					required: true,
					options
				) },
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferVisibleAsync, RunAsync )
		};
	} }

	public static async Task RunAsync(TimedInteraction interaction) {
		// Select the correct invite to return.
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string @class = (string)args[0].Value;
		string invite = _optionsToInvites[@class];

		// Send invite link.
		await Command.SubmitResponseAsync(
			interaction,
			invite,
			"Sending invite link.",
			LogLevel.Debug,
			new Lazy<string>(() => {
				string invite_line = invite.FirstLineElided();
				return $"Invite link for \"{@class}\": {invite_line}";
			})
		);
	}
}
