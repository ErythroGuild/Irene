namespace Irene;

static class ClassSpec {
	public enum Role { Tank, Heal, DPS, }
	public enum Class {
		DK, DH, Druid , Evoker , Hunter ,
		Mage  , Monk  , Paladin, Priest ,
		Rogue , Shaman, Warlock, Warrior,
	}
	public enum Spec {
		DK_Blood    , DK_Frost    , DK_Unholy     ,
		DH_Vengeance, DH_Havoc    ,
		Druid_Bear  , Druid_Resto , Druid_Feral   , Druid_Balance,
		Evoker_Dev  , Evoker_Pres ,
		Hunter_SV   , Hunter_BM   , Hunter_MM     ,
		Mage_Arcane , Mage_Fire   , Mage_Frost    ,
		Monk_BRM    , Monk_MW     , Monk_WW       ,
		Paladin_Prot, Paladin_Holy, Paladin_Ret   ,
		Priest_Disc , Priest_Holy , Priest_Shadow ,
		Rogue_Sin   , Rogue_Outlaw, Rogue_Sub     ,
		Shaman_Resto, Shaman_Enh  , Shaman_Ele    ,
		Warlock_Aff , Warlock_Demo, Warlock_Destro,
		Warrior_Prot, Warrior_Arms, Warrior_Fury  ,
	}

	// Conversion tables.
	// `ConstBiMap` can't used here because the input isn't one-to-one.
	private static readonly IReadOnlyDictionary<Class, IReadOnlyList<Spec>> _classSpecs =
		new ConcurrentDictionary<Class, IReadOnlyList<Spec>>() {
			[Class.DK     ] = new List<Spec> { Spec.DK_Blood    , Spec.DK_Frost    , Spec.DK_Unholy      },
			[Class.DH     ] = new List<Spec> { Spec.DH_Vengeance, Spec.DH_Havoc    },
			[Class.Druid  ] = new List<Spec> { Spec.Druid_Bear  , Spec.Druid_Resto , Spec.Druid_Feral    , Spec.Druid_Balance },
			[Class.Evoker ] = new List<Spec> { Spec.Evoker_Pres , Spec.Evoker_Dev  },
			[Class.Hunter ] = new List<Spec> { Spec.Hunter_SV   , Spec.Hunter_BM   , Spec.Hunter_MM      },
			[Class.Mage   ] = new List<Spec> { Spec.Mage_Arcane , Spec.Mage_Fire   , Spec.Mage_Frost     },
			[Class.Monk   ] = new List<Spec> { Spec.Monk_BRM    , Spec.Monk_MW     , Spec.Monk_WW        },
			[Class.Paladin] = new List<Spec> { Spec.Paladin_Prot, Spec.Paladin_Holy, Spec.Paladin_Ret    },
			[Class.Priest ] = new List<Spec> { Spec.Priest_Disc , Spec.Priest_Holy , Spec.Priest_Shadow  },
			[Class.Rogue  ] = new List<Spec> { Spec.Rogue_Sin   , Spec.Rogue_Outlaw, Spec.Rogue_Sub      },
			[Class.Shaman ] = new List<Spec> { Spec.Shaman_Resto, Spec.Shaman_Enh  , Spec.Shaman_Ele     },
			[Class.Warlock] = new List<Spec> { Spec.Warlock_Aff , Spec.Warlock_Demo, Spec.Warlock_Destro },
			[Class.Warrior] = new List<Spec> { Spec.Warrior_Prot, Spec.Warrior_Arms, Spec.Warrior_Fury   },
		};
	// Class <-> spec conversion is more common and has a more limited
	// number of keys, so it is being cached while the class-/spec-parsing
	// dictionaries are not.
	private static readonly IReadOnlyDictionary<Spec, Class> _specClasses;

	// Parsing tables.
	private static readonly IReadOnlyDictionary<Class, IReadOnlyList<string>> _dictClass =
		new ConcurrentDictionary<Class, IReadOnlyList<string>>() {
			[Class.DK     ] = new List<string> { "death knight", "deathknight", "dk" },
			[Class.DH     ] = new List<string> { "demon hunter", "demonhunter", "dh" },
			[Class.Druid  ] = new List<string> { "druid", "drood" },
			[Class.Evoker ] = new List<string> { "evoker", "voke" },
			[Class.Hunter ] = new List<string> { "hunter", "hunt" },
			[Class.Mage   ] = new List<string> { "mage" },
			[Class.Monk   ] = new List<string> { "monk" },
			[Class.Paladin] = new List<string> { "paladin", "pally", "pal" },
			[Class.Priest ] = new List<string> { "priest" },
			[Class.Rogue  ] = new List<string> { "rogue" },
			[Class.Shaman ] = new List<string> { "shaman", "shammy", "sham" },
			[Class.Warlock] = new List<string> { "warlock", "lock" },
			[Class.Warrior] = new List<string> { "warrior", "warr", "war" },
		};
	private static readonly IReadOnlyDictionary<Spec, IReadOnlyList<string>> _dictSpec =
		new ConcurrentDictionary<Spec, IReadOnlyList<string>>() {
			[Spec.DK_Blood ] = new List<string> { "blood", "bdk" },
			[Spec.DK_Frost ] = new List<string> { "frost" },
			[Spec.DK_Unholy] = new List<string> { "unholy", "uh" },

			[Spec.DH_Vengeance] = new List<string> { "vengeance", "vdh", "veng", "venge" },
			[Spec.DH_Havoc    ] = new List<string> { "havoc" },

			[Spec.Druid_Bear   ] = new List<string> { "bear", "guardian" },
			[Spec.Druid_Resto  ] = new List<string> { "rdruid", "resto", "restoration" },
			[Spec.Druid_Feral  ] = new List<string> { "feral", "cat" },
			[Spec.Druid_Balance] = new List<string> { "balance", "moonkin", "boomy", "boomkin" },

			[Spec.Evoker_Pres] = new List<string> { "preservation", "preserve", "pres", "prez" },
			[Spec.Evoker_Dev ] = new List<string> { "devastation", "devastate", "dev", "deva" },
			
			[Spec.Hunter_SV] = new List<string> { "survival", "sv", "surv" },
			[Spec.Hunter_BM] = new List<string> { "beast mastery", "bm", "beast master", "beastmastery", "beastmaster" },
			[Spec.Hunter_MM] = new List<string> { "marksman", "marks", "mm", "marksmanship" },

			[Spec.Mage_Arcane] = new List<string> { "arcane" },
			[Spec.Mage_Fire  ] = new List<string> { "fire" },
			[Spec.Mage_Frost ] = new List<string> { "frost", "ice" },

			[Spec.Monk_BRM] = new List<string> { "brm", "brewmaster", "brew", "br" },
			[Spec.Monk_MW ] = new List<string> { "mw", "mistweaver", "mist" },
			[Spec.Monk_WW ] = new List<string> { "ww", "windwalker" },

			[Spec.Paladin_Prot] = new List<string> { "protpal", "protection", "prot" },
			[Spec.Paladin_Holy] = new List<string> { "hpal", "holy" },
			[Spec.Paladin_Ret ] = new List<string> { "ret", "retribution" },

			[Spec.Priest_Disc  ] = new List<string> { "disc", "discipline" },
			[Spec.Priest_Holy  ] = new List<string> { "hpriest", "holy" },
			[Spec.Priest_Shadow] = new List<string> { "shadow", "spriest" },

			[Spec.Rogue_Sin   ] = new List<string> { "sin", "ass", "assassination", "assassin" },
			[Spec.Rogue_Outlaw] = new List<string> { "outlaw", "combat" },
			[Spec.Rogue_Sub   ] = new List<string> { "sub", "subtlety" },

			[Spec.Shaman_Resto] = new List<string> { "rsham", "resto", "restoration" },
			[Spec.Shaman_Enh  ] = new List<string> { "enh", "enhance", "enhancement" },
			[Spec.Shaman_Ele  ] = new List<string> { "ele", "elemental" },

			[Spec.Warlock_Aff   ] = new List<string> { "aff", "affliction", "afflock" },
			[Spec.Warlock_Demo  ] = new List<string> { "demo", "demonology", "demolock" },
			[Spec.Warlock_Destro] = new List<string> { "destro", "destruction", "destrolock" },

			[Spec.Warrior_Prot] = new List<string>() { "protection", "prot" },
			[Spec.Warrior_Arms] = new List<string>() { "arms" },
			[Spec.Warrior_Fury] = new List<string>() { "fury" },
		};

	// Initialize dictionary caches with redundant indices. (This improves
	// performance at the cost of memory usage.)
	static ClassSpec() {
		// Index with specs as keys, and copy their classes as values.
		ConcurrentDictionary<Spec, Class> specClasses = new ();
		foreach (Class @class in _classSpecs.Keys) {
			foreach (Spec spec in _classSpecs[@class])
				specClasses.TryAdd(spec, @class);
		}
		_specClasses = specClasses;
	}

	// Display names of classes/specs.
	public static string Name(this Class @class) => @class switch {
		Class.DK      => "Death Knight",
		Class.DH      => "Demon Hunter",
		Class.Druid   => "Druid"  ,
		Class.Evoker  => "Evoker" ,
		Class.Hunter  => "Hunter" ,
		Class.Mage    => "Mage"   ,
		Class.Monk    => "Monk"   ,
		Class.Paladin => "Paladin",
		Class.Priest  => "Priest" ,
		Class.Rogue   => "Rogue"  ,
		Class.Shaman  => "Shaman" ,
		Class.Warlock => "Warlock",
		Class.Warrior => "Warrior",
		_ => throw new ArgumentException("Unknown class.", nameof(@class)),
	};
	public static string Name(this Spec spec) => spec switch {
		Spec.DK_Blood  => "Blood" ,
		Spec.DK_Frost  => "Frost" ,
		Spec.DK_Unholy => "Unholy",

		Spec.DH_Vengeance => "Vengeance",
		Spec.DH_Havoc     => "Havoc"    ,

		Spec.Druid_Bear    => "Guardian"   ,
		Spec.Druid_Resto   => "Restoration",
		Spec.Druid_Feral   => "Feral"      ,
		Spec.Druid_Balance => "Balance"    ,

		Spec.Evoker_Pres => "Preservation",
		Spec.Evoker_Dev  => "Devastation" ,

		Spec.Hunter_BM => "Beast Mastery",
		Spec.Hunter_MM => "Marksmanship" ,
		Spec.Hunter_SV => "Survival"     ,

		Spec.Mage_Arcane => "Arcane",
		Spec.Mage_Fire   => "Fire"  ,
		Spec.Mage_Frost  => "Frost" ,

		Spec.Monk_BRM => "Brewmaster",
		Spec.Monk_MW  => "Mistweaver",
		Spec.Monk_WW  => "Windwalker",

		Spec.Paladin_Prot => "Protection" ,
		Spec.Paladin_Holy => "Holy"       ,
		Spec.Paladin_Ret  => "Retribution",

		Spec.Priest_Disc   => "Discipline",
		Spec.Priest_Holy   => "Holy"      ,
		Spec.Priest_Shadow => "Shadow"    ,

		Spec.Rogue_Sin    => "Assassination",
		Spec.Rogue_Outlaw => "Outlaw"       ,
		Spec.Rogue_Sub    => "Subtlety"     ,

		Spec.Shaman_Resto => "Restoration",
		Spec.Shaman_Enh   => "Enhancement",
		Spec.Shaman_Ele   => "Elemental"  ,

		Spec.Warlock_Aff    => "Affliction" ,
		Spec.Warlock_Demo   => "Demonology" ,
		Spec.Warlock_Destro => "Destruction",

		Spec.Warrior_Prot => "Protection",
		Spec.Warrior_Arms => "Arms"      ,
		Spec.Warrior_Fury => "Fury"      ,

		_ => throw new ArgumentException("Unknown spec.", nameof(spec)),
	};
	public static string FullName(this Spec spec) =>
		$"{spec.Name()} {spec.GetClass().Name()}";

	// Class colors.
	public static DiscordColor Color(this Class @class) => @class switch {
		Class.DK      => new ("#C41F3B"),
		Class.DH      => new ("#A330C9"),
		Class.Druid   => new ("#FF7D0A"),
		Class.Evoker  => new ("#33937F"),
		Class.Hunter  => new ("#ABD473"),
		Class.Mage    => new ("#3FC7EB"),
		Class.Monk    => new ("#00FF96"),
		Class.Paladin => new ("#F58CBA"),
		Class.Priest  => new ("#FFFFFF"),
		Class.Rogue   => new ("#FFF569"),
		Class.Shaman  => new ("#0070DE"),
		Class.Warlock => new ("#8788EE"),
		Class.Warrior => new ("#C79C6E"),
		_ => throw new ArgumentOutOfRangeException(nameof(@class), "Unrecognized class name."),
	};

	// Emojis associated with roles/classes.
	public static DiscordEmoji Emoji(this Role role, GuildData erythro) {
		ulong id = role switch {
			Role.Tank => id_e.tank,
			Role.Heal => id_e.heal,
			Role.DPS  => id_e.dps ,
			_ => throw new ArgumentException("Unknown role.", nameof(role)),
		};
		return erythro.Emoji(id);
	}
	public static DiscordEmoji Emoji(this Class @class, GuildData erythro) {
		ulong id = @class switch {
			Class.DK      => id_e.dk     ,
			Class.DH      => id_e.dh     ,
			Class.Druid   => id_e.druid  ,
			Class.Evoker  => id_e.evoker ,
			Class.Hunter  => id_e.hunter ,
			Class.Mage    => id_e.mage   ,
			Class.Monk    => id_e.monk   ,
			Class.Paladin => id_e.paladin,
			Class.Priest  => id_e.priest ,
			Class.Rogue   => id_e.rogue  ,
			Class.Shaman  => id_e.shaman ,
			Class.Warlock => id_e.warlock,
			Class.Warrior => id_e.warrior,
			_ => throw new ArgumentException("Unknown class.", nameof(@class)),
		};
		return erythro.Emoji(id);
	}

	// The categories a Spec falls into.
	public static Class GetClass(this Spec spec) =>
		_specClasses[spec];
	public static Role GetRole(this Spec spec) => spec switch {
		Spec.DK_Blood  => Role.Tank,
		Spec.DK_Frost  => Role.DPS ,
		Spec.DK_Unholy => Role.DPS ,

		Spec.DH_Vengeance => Role.Tank,
		Spec.DH_Havoc     => Role.DPS ,

		Spec.Druid_Bear    => Role.Tank,
		Spec.Druid_Resto   => Role.Heal,
		Spec.Druid_Balance => Role.DPS ,
		Spec.Druid_Feral   => Role.DPS ,

		Spec.Evoker_Pres => Role.Heal,
		Spec.Evoker_Dev  => Role.DPS ,

		Spec.Hunter_SV => Role.DPS,
		Spec.Hunter_BM => Role.DPS,
		Spec.Hunter_MM => Role.DPS,

		Spec.Mage_Arcane => Role.DPS,
		Spec.Mage_Fire   => Role.DPS,
		Spec.Mage_Frost  => Role.DPS,

		Spec.Monk_BRM => Role.Tank,
		Spec.Monk_MW  => Role.Heal,
		Spec.Monk_WW  => Role.DPS ,

		Spec.Paladin_Prot => Role.Tank,
		Spec.Paladin_Holy => Role.Heal,
		Spec.Paladin_Ret  => Role.DPS ,

		Spec.Priest_Disc   => Role.Heal,
		Spec.Priest_Holy   => Role.Heal,
		Spec.Priest_Shadow => Role.DPS ,

		Spec.Rogue_Sin    => Role.DPS,
		Spec.Rogue_Outlaw => Role.DPS,
		Spec.Rogue_Sub    => Role.DPS,

		Spec.Shaman_Resto => Role.Heal,
		Spec.Shaman_Enh   => Role.DPS ,
		Spec.Shaman_Ele   => Role.DPS ,

		Spec.Warlock_Aff    => Role.DPS,
		Spec.Warlock_Demo   => Role.DPS,
		Spec.Warlock_Destro => Role.DPS,

		Spec.Warrior_Prot => Role.Tank,
		Spec.Warrior_Arms => Role.DPS ,
		Spec.Warrior_Fury => Role.DPS ,

		_ => throw new ArgumentException("Unknown spec.", nameof(spec)),
	};
}
