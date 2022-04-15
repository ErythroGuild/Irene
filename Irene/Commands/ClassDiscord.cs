using static Irene.ClassSpec;

namespace Irene.Commands;

class ClassDiscord : ICommand {
	private static readonly Dictionary<Class, string> _options = new () {
		{ Class.DK, "death-knight" },
		{ Class.DH, "demon-hunter" },
		{ Class.Druid  , "druid"   },
		{ Class.Hunter , "hunter"  },
		{ Class.Mage   , "mage"    },
		{ Class.Monk   , "monk"    },
		{ Class.Paladin, "paladin" },
		{ Class.Priest , "priest"  },
		{ Class.Rogue  , "rogue"   },
		{ Class.Shaman , "shaman"  },
		{ Class.Warlock, "warlock" },
		{ Class.Warrior, "warrior" },
	};
	private static readonly Dictionary<Class, string> _invites = new () {
		{ Class.DK     , @"https://discord.gg/acherus"        },
		{ Class.DH     , @"https://discord.gg/felhammer"      },
		{ Class.Druid  , @"https://discord.gg/dreamgrove"     },
		{ Class.Hunter , @"https://discord.gg/trueshot"       },
		{ Class.Mage   , @"https://discord.gg/makGfZA"        },
		{ Class.Monk   , @"https://discord.gg/peakofserenity" },
		{ Class.Paladin, @"https://discord.gg/hammerofwrath"  },
		{ Class.Rogue  , @"https://discord.gg/ravenholdt"     },
		{ Class.Warlock, @"https://discord.gg/blackharvest"   },
		{ Class.Warrior, @"https://discord.gg/skyhold"        },

		{ Class.Priest , @"https://discord.gg/warcraftpriests" + "\n" + @"https://discord.gg/focusedwill (disc-only)" },
		{ Class.Shaman , @"https://discord.gg/earthshrine"     + "\n" + @"https://discord.gg/AcTek6e (resto-only)"    },
	};
	private static readonly Dictionary<string, string> _optionsToInvites;

	// Force static initializer to run.
	public static void Init() { return; }
	static ClassDiscord() {
		_optionsToInvites = new ();
		foreach (Class @class in _options.Keys)
			_optionsToInvites.Add(_options[@class], _invites[@class]);
	}

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			"`/class-discord <class>` displays the class discord invite.",
			"Multiple invites are given if available."
		} ) };
	}

	public static List<InteractionCommand> SlashCommands { get {
		// Compile list of all options.
		List<CommandOptionEnum> options = new ();
		foreach (Class @class in _options.Keys)
			options.Add(new (Name(@class), _options[@class]));

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
			), RunAsync )
		};
	} }

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }
	public static List<AutoCompleteHandler> AutoComplete   { get => new (); }

	public static async Task RunAsync(TimedInteraction interaction) {
		// Select the correct invite to return.
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string @class = (string)args[0].Value;
		string invite = _optionsToInvites[@class];

		// Send invite link.
		await Command.RespondAsync(
			interaction,
			invite, false,
			"Sending invite link.",
			LogLevel.Debug,
			new Lazy<string>(() => {
				string invite_line = invite.FirstLineElided();
				return $"Invite link for \"{@class}\": {invite_line}";
			})
		);
	}
}
