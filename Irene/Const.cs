namespace Irene;

using RecurTime = RecurringEvent.RecurTime;

static class Const {
	// Escape sequences.
	// Note 1: the unescaped code should be a single codepoint.
	// Note 2: These MUST begin with '\' or be enclosed in ':'. Codes
	// which begin with '\' can only be one character long.
	// This format allows for huge optimizations when parsing a string
	// to unescape it.
	public static readonly ConstBiMap<string, string> EscapeCodes =
		new (new Dictionary<string, string> {
			[@"\n"    ] = "\n"    ,
			[@"\t"    ] = "\t"    ,

			[@":esc:" ] = "\x1B"  ,
			[@":nbsp:"] = "\u00A0",

			[@":bbul:"] = "\u2022",
			[@":wbul:"] = "\u25E6",
			[@":emsp:"] = "\u2003",
			[@":ensp:"] = "\u2002",
			[@":thsp:"] = "\u2009",
			[@":hrsp:"] = "\u200A",
			[@":l"":" ] = "\u201C",
			[@":r"":" ] = "\u201D",
			[@":l':"  ] = "\u2018",
			[@":r':"  ] = "\u2019",
			[@":n-:"  ] = "\u2013",
			[@":m-:"  ] = "\u2014",
			[@":...:" ] = "\u2026",
			[@":*2:"  ] = "\u2020", // dagger
			[@":*3:"  ] = "\u2021", // double dagger
			[@":deg:" ] = "\u00B0",
			[@":inf:" ] = "\u221E",
			[@":+-:"  ] = "\u00B1",
			[@":!=:"  ] = "\u2260",
			[@":<=:"  ] = "\u2264",
			[@":>=:"  ] = "\u2265",
		});

	// Standard formatting strings.
	public const string
		Format_IsoDate = @"yyyy-MM-dd";

	// US time zones.
	public static readonly TimeZoneInfo
		//TimeZone_PT = TimeZoneInfo.FindSystemTimeZoneById(@"US/Pacific"),
		//TimeZone_MT = TimeZoneInfo.FindSystemTimeZoneById(@"US/Mountain"),
		//TimeZone_CT = TimeZoneInfo.FindSystemTimeZoneById(@"US/Central"),
		//TimeZone_ET = TimeZoneInfo.FindSystemTimeZoneById(@"US/Eastern");
		TimeZone_PT = TimeZoneInfo.FindSystemTimeZoneById(@"America/Denver"),
		TimeZone_MT = TimeZoneInfo.FindSystemTimeZoneById(@"America/Denver"),
		TimeZone_CT = TimeZoneInfo.FindSystemTimeZoneById(@"America/Denver"),
		TimeZone_ET = TimeZoneInfo.FindSystemTimeZoneById(@"America/Denver");
	public static readonly TimeZoneInfo TimeZone_Server = TimeZone_CT;

	// Patch release days.
	// 7:00 PST = 8:00 PDT = 15:00 UTC
	// Server reset time does not change with DST.
	public static readonly RecurTime
		Time_ServerReset = new (new (15, 0), TimeZoneInfo.Utc),
		Time_RaidStart   = new (new (21, 0), TimeZone_Server);
	public static readonly DateOnly
		Date_Patch902 = new (2020, 11, 17),
		Date_Season1  = new (2020, 12,  8),
		Date_Patch905 = new (2021,  3,  9),
		Date_Patch910 = new (2021,  6, 29),
		Date_Season2  = new (2021,  7,  6),
		Date_Patch915 = new (2021, 11,  2),
		Date_Patch920 = new (2022,  2, 22),
		Date_Season3  = new (2022,  3,  1);

	// Convenience function to get the DateTime (in UTC) of a given date.
	public static DateTime UtcResetTime(this DateOnly date) =>
		date.ToDateTime(Time_ServerReset.TimeOnly, DateTimeKind.Utc);

	// Discord entity IDs.
	// (User objects shouldn't be cached.)
	public static class Id {
		public static class Channel {
			public const ulong
			// Broadcast
				rules     =  443002035604815872,
				announce  =  443001903123791873,
				resources = 1058564619804880906,
				pins      =  648461869979271179,
				starboard =  778102974551293962,
				audit     =  778102942141382678,

				// Text
				general  =  317723973968461824,
				sharing  =  443000872968912896,
				kirasath = 1027463743635980378,
				spoilers =  454338639933997068,
				memes    =  543476538125844511,
				lf       = 1047701283815034942,
				bots     =  613034262823698452,
				news     =  612307690613637125,

				// Officer
				officerInfo  = 650603757113049088,
				officer      = 542023200549371906,
				officerBots  = 779385198533804062,
				officerVoice = 542093594618757140,

				// Voice - Social
				hangout = 442986379392319490,
				afk     = 545060010967957534,

				// Voice - Gaming
				dungeon1 = 442986315865128960,
				dungeon2 = 542094075784986654,
				dungeon3 = 783838007454597121,
				raid1    = 443001346678063104,
				raid2    = 670853551869919232,

				// Testing
				test      = 489274692255875091,
				ingest    = 777935219193020426,
				heartwood = 498636532530282512;
		}
		public static class Emoji {
			public const ulong
				erythro = 651475835387248660,

				wooloo   =  588112288628604928,
				cupcake  =  777925283499737138,
				toxic    =  839929134326349825,
				pooh     = 1050218788613800038,
				mrrogers =  777861300423884803,

				// Erythro bear
				eryLove = 860386122566991883,
				eryWoah = 920545959883202620,

				// Noto (animated)
				noto100       = 1047648870160666625,
				notoClover    = 1047621349016875128,
				notoSprout    = 1047621342574420088,
				notoConfetti  = 1047621474103607336,
				notoParty     = 1047621479711383622,
				notoFireworks = 1047648858529861834,
				notoButterfly = 1047621240703168513,
				notoOctopus   = 1047621235930038352,
				notoInnocent  = 1047648956705931385,
				notoMelting   = 1047648977153183783,
				notoMindblown = 1047648943946858566,
				notoEyebrow   = 1047649025886781501,
				notoSmile     = 1047656340182544384,
				notoSob       = 1047656345467371664,

				// Ranks
				rankNone       = 1047313293087359037,
				rankGuest      = 1047313291443175424,
				rankMember     = 1047313289895493834,
				rankOfficer    = 1047313288200978503,
				rankAdmin      = 1047313286544248933,
				rankBot        = 1047313285369839616,
				rankStylist    = 1047313283796971551,
				rankAmbassador = 1047313282442199050,

				// Titles
				beloved    = 1060156214681534495,
				hallowed   = 1060156211380629545,
				chosen     = 1060156219299471401,
				celebrated = 1060156216644481024,

				// Roles
				tank = 708431859369115790,
				heal = 708431859435962418,
				dps  = 708431859385630862,

				// Covenants
				kyrian    = 697164668866658385,
				necrolord = 697164668506079332,
				nightfae  = 697164668975972433,
				venthyr   = 697166461164322927,

				// Classes
				dk      =  676750707759513611,
				dh      =  676750708175011860,
				druid   =  676750708447641611,
				evoker  = 1044773722046873784,
				hunter  =  676750708879523850,
				mage    =  676750710699720736,
				monk    =  676750843286126624,
				paladin =  676750876433711114,
				priest  =  676750889922330665,
				rogue   =  676750902895312901,
				shaman  =  676750915843260436,
				warlock =  676750927889170437,
				warrior =  676750939910045707,

				// Profession quality
				quality1   = 1046997123494838282,
				quality2   = 1046997182240280628,
				quality3   = 1046997219305328701,
				quality4   = 1046997260942196796,
				quality5   = 1046997302486769695,
				qualityAny = 1046997344731799563,

				// Raid
				eranog        = 1047021853815873627,
				terros        = 1047021887596806155,
				primalCouncil = 1047021903480631316,
				sennarth      = 1047021918362030091,
				dathea        = 1047021936212967424,
				kurog         = 1047021950758813786,
				diurna        = 1047021965619232788,
				raszageth     = 1047021981343690782,

				// Dungeons
				algethar        = 1047046693499191308,
				azureVault      = 1047046695323709520,
				brackenhide     = 1047046697169195038,
				hallsOfInfusion = 1047046698863689738,
				neltharus       = 1047046700302336050,
				nokhudOffensive = 1047046702378516480,
				rubyLifePools   = 1047046704114958407,
				uldaman         = 1047046705956257812,

				// Seasonal dungeons
				jadeSerpent  = 1047046971149529249,
				shadowmoon   = 1047046969463422996,
				hallsOfValor = 1047046967311740949,
				courtOfStars = 1047046965692727356,

				// Analysis websites
				raiderio     = 699710975451856967,
				warcraftlogs = 699709399094132884,
				wipefest     = 699647884819169700,
				wowanalyzer  = 699648774099828796,
				lorrgs       = 946214790043410462,

				// Sparkles
				sparkleRed     = 1050217729552044083,
				sparkleOrange  = 1050217716990083082,
				sparkleYellow  = 1050217708723126303,
				sparkleGreen   = 1050217692981895198,
				sparkleCyan    = 1050217676158554112,
				sparkleBlue    = 1050217667174334514,
				sparklePurple  = 1050217657183506473,
				sparkleFuschia = 1050217643480711310,

				// Progress bars
				barEmptyL     = 1047326995886915614,
				barEmptyM     = 1047326997895991316,
				barEmptyR     = 1047326999137488987,
				barFilledL    = 1047327000219627630,
				barFilledM    = 1047327001473732719,
				barFilledR    = 1047327003122081842,
				barFillingL   = 1047327004724318228,
				barFillingLtR = 1047327521592574042,
				barFillingRtL = 1047327540857028610,
				barFillingR   = 1047327007102480404,

				// Progress spinners
				spinnerLines  = 1048081457521897523,
				spinnerDots   = 1047801017049305130,
				spinnerOrbit  = 1048073321964646522,
				spinnerCircle = 1047757059589943356,

				// Coins
				heads = 1022712367999615006,
				tails = 1022712370579124294;
		}
		public static class Guild {
			public const ulong
				erythro     =  317723973968461824,
				erythroDev  =  834820466635046932,
				ireneEmojis = 1047306759083130912;
		}
		public static class Role {
			public const ulong
				// Colors
				ambassador = 724762507612520530,
				stylist    = 777570683159838760,
				bot        = 614642181587599439,
				admin      = 452298394967343105,
				officer    = 542021861727272981,
				member     = 452299909371265042,
				guest      = 452299336123023360,

				// Titles
				beloved    = 780159178202284053,
				hallowed   = 780161029111021630,
				chosen     = 903853996714913834,
				celebrated = 780159754676338700,

				// Officers
				raidOfficer  = 723061867127373874,
				eventPlanner = 723061585156898836,
				recruiter    = 723060994859073607,
				banker       = 723061777213947974,

				// Guilds
				erythro = 542021676884557824,
				glaive  = 529135445712568323,
				sanctum = 830985516639584257,
				angels  = 529135186382946345,
				asgard  = 539515941877448728,

				// Subscriptions
				raid    = 654062159956803664,
				mythics = 653334284299534336,
				ksm     = 665203067767226368,
				gearing = 854879377930453002,
				events  = 771584406724935710,
				herald  = 712469431573544972,

				// Other
				karkun = 1027464062814146570;
		}
		public static class User {
			public const ulong
				admin         =  165557736287764483,
				polybius      =  483340619432067098,
				puck          =  703068724818608138,
				wowheadDigest =  779053723489140759;
		}
	}
}
