namespace Irene.Commands;

using Class = ClassSpec.Class;

class ClassDiscords : ICommands {
	static readonly Dictionary<Class, string> invites = new () {
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

	public static string help() {
		StringWriter text = new ();

		text.WriteLine("Link the server invite for the given class discord.");
		text.WriteLine("`@Irene -classdiscord <class>`");

		return text.ToString();
	}

	public static void run(Command cmd) {
		string arg = cmd.args.Trim().ToLower();

		// Parse argument and handle errors.
		Class? class_n = ClassSpec.parse_class(arg);
		if (class_n is null) {
			Log.Information("  No class found in command.");
			StringWriter text = new ();
			text.WriteLine("Could not determine a class from the command.");
			text.WriteLine("See also: `@Irene -help classdiscord`");
			_ = cmd.msg.RespondAsync(text.ToString());
			return;
		}
		Class @class = (Class)class_n;
		if (@class == Class.Multiple) {
			Log.Information("  Multiple classes found in command.");
			StringWriter text = new ();
			text.WriteLine("Found multiple classes in the command; try a more specific term.");
			text.WriteLine("See also: `@Irene -help classdiscord`");
			_ = cmd.msg.RespondAsync(text.ToString());
			return;
		}

		// Add class emojis to response and return.
		string response = invites[@class];
		Log.Information($"  Invite fetched: {response}");
		string emoji = ClassSpec.class_emoji(@class);
		response = $"{emoji} {response}";
		response = response.Replace("\n", $"\n{emoji} ");
		_ = cmd.msg.RespondAsync(response);
	}
}
