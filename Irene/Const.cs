using RecurTime = Irene.RecurringEvent.RecurTime;

namespace Irene;

static class Const {
	// Standard formatting strings.
	public const string
		Format_IsoDate = @"yyyy-MM-dd";

	public static readonly TimeZoneInfo
		TimeZone_PT = TimeZoneInfo.FindSystemTimeZoneById(@"America/Los_Angeles"),
		TimeZone_MT = TimeZoneInfo.FindSystemTimeZoneById(@"America/Denver"),
		TimeZone_CT = TimeZoneInfo.FindSystemTimeZoneById(@"America/Chicago"),
		TimeZone_ET = TimeZoneInfo.FindSystemTimeZoneById(@"America/New_York");
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
			rules     = 443002035604815872,
			announce  = 443001903123791873,
			resources = 542093130502111252,
			pins      = 648461869979271179,
			starboard = 778102974551293962,
			audit     = 778102942141382678,

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

			wooloo   = 588112288628604928,
			cupcake  = 777925283499737138,
			toxic    = 839929134326349825,
			pooh     = 756560918208446514,
			mrrogers = 777861300423884803,

			// Erythro bear
			eryLove = 860386122566991883,
			eryWoah = 920545959883202620,

			// Analysis websites
			raiderio     = 699710975451856967,
			warcraftlogs = 699709399094132884,
			wipefest     = 699647884819169700,
			wowanalyzer  = 699648774099828796,
			lorrgs       = 946214790043410462,

			// Coins
			heads = 1022712367999615006,
			tails = 1022712370579124294,

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

			// Raid
			eranog        = 1047021853815873627,
			terros        = 1047021887596806155,
			primalCouncil = 1047021903480631316,
			sennarth      = 1047021918362030091,
			dathea        = 1047021936212967424,
			kurog         = 1047021950758813786,
			diurna        = 1047021965619232788,
			raszageth     = 1047021981343690782,

			// Ranks
			rankNone      = 1047313293087359037,
			rankGuest     = 1047313291443175424,
			rankMember    = 1047313289895493834,
			rankOfficer   = 1047313288200978503,
			rankAdmin     = 1047313286544248933,
			rankBot       = 1047313285369839616,
			rankStylist   = 1047313283796971551,
			rankAmbassador= 1047313282442199050;
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
			acclaimed  = 780161029111021630,
			celebrated = 780159754676338700,
			chosen     = 903853996714913834,

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
			public const ulong admin = 165557736287764483;
		}
	}
}
