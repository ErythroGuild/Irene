namespace Irene;

static class ClassSpec {
	public enum Role {
		Tank, Heal, DPS,
	}
	public enum Class {
		DK, DH, Druid, Hunter,
		Mage, Monk, Paladin, Priest,
		Rogue, Shaman, Warlock, Warrior,
	}
	public enum Spec {
		DK_Blood, DK_Frost, DK_Unholy,
		DH_Vengeance, DH_Havoc,
		Druid_Bear, Druid_Resto, Druid_Feral, Druid_Balance,
		Hunter_BM, Hunter_MM, Hunter_SV,
		Mage_Arcane, Mage_Fire, Mage_Frost,
		Monk_BRM, Monk_MW, Monk_WW,
		Paladin_Prot, Paladin_Holy, Paladin_Ret,
		Priest_Disc, Priest_Holy, Priest_Shadow,
		Rogue_Sin, Rogue_Outlaw, Rogue_Sub,
		Shaman_Resto, Shaman_Enh, Shaman_Ele,
		Warlock_Aff, Warlock_Demo, Warlock_Destro,
		Warrior_Prot, Warrior_Arms, Warrior_Fury,
	}

	// Conversion tables.
	private static readonly ReadOnlyDictionary<Role, ulong> _tableRoleEmoji =
		new (new ConcurrentDictionary<Role, ulong>() {
			[Role.Tank] = id_e.tank,
			[Role.Heal] = id_e.heal,
			[Role.DPS ] = id_e.dps ,
		});
	private static readonly ReadOnlyDictionary<Class, ulong> _tableClassEmoji =
		new (new ConcurrentDictionary<Class, ulong>() {
			[Class.DK     ] = id_e.dk     ,
			[Class.DH     ] = id_e.dh     ,
			[Class.Druid  ] = id_e.druid  ,
			[Class.Hunter ] = id_e.hunter ,
			[Class.Mage   ] = id_e.mage   ,
			[Class.Monk   ] = id_e.monk   ,
			[Class.Paladin] = id_e.paladin,
			[Class.Priest ] = id_e.priest ,
			[Class.Rogue  ] = id_e.rogue  ,
			[Class.Shaman ] = id_e.shaman ,
			[Class.Warlock] = id_e.warlock,
			[Class.Warrior] = id_e.warrior,
		});

	private static readonly ReadOnlyDictionary<Spec, Role> _specRoles =
		new (new ConcurrentDictionary<Spec, Role>() {
			[Spec.DK_Blood ] = Role.Tank,
			[Spec.DK_Frost ] = Role.DPS ,
			[Spec.DK_Unholy] = Role.DPS ,

			[Spec.DH_Vengeance] = Role.Tank,
			[Spec.DH_Havoc    ] = Role.DPS ,

			[Spec.Druid_Bear   ] = Role.Tank,
			[Spec.Druid_Resto  ] = Role.Heal,
			[Spec.Druid_Balance] = Role.DPS ,
			[Spec.Druid_Feral  ] = Role.DPS ,

			[Spec.Hunter_BM] = Role.DPS,
			[Spec.Hunter_MM] = Role.DPS,
			[Spec.Hunter_SV] = Role.DPS,

			[Spec.Mage_Arcane] = Role.DPS,
			[Spec.Mage_Fire  ] = Role.DPS,
			[Spec.Mage_Frost ] = Role.DPS,

			[Spec.Monk_BRM] = Role.Tank,
			[Spec.Monk_MW ] = Role.Heal,
			[Spec.Monk_WW ] = Role.DPS ,

			[Spec.Paladin_Prot] = Role.Tank,
			[Spec.Paladin_Holy] = Role.Heal,
			[Spec.Paladin_Ret ] = Role.DPS ,

			[Spec.Priest_Disc  ] = Role.Heal,
			[Spec.Priest_Holy  ] = Role.Heal,
			[Spec.Priest_Shadow] = Role.DPS ,

			[Spec.Rogue_Sin   ] = Role.DPS,
			[Spec.Rogue_Outlaw] = Role.DPS,
			[Spec.Rogue_Sub   ] = Role.DPS,

			[Spec.Shaman_Resto] = Role.Heal,
			[Spec.Shaman_Enh  ] = Role.DPS ,
			[Spec.Shaman_Ele  ] = Role.DPS ,

			[Spec.Warlock_Aff   ] = Role.DPS,
			[Spec.Warlock_Demo  ] = Role.DPS,
			[Spec.Warlock_Destro] = Role.DPS,

			[Spec.Warrior_Prot] = Role.Tank,
			[Spec.Warrior_Arms] = Role.DPS ,
			[Spec.Warrior_Fury] = Role.DPS ,
		});
	private static readonly ReadOnlyDictionary<Class, ReadOnlyCollection<Spec>> _classSpecs =
		new (new ConcurrentDictionary<Class, ReadOnlyCollection<Spec>>() {
			[Class.DK     ] = new (new List<Spec>() { Spec.DK_Blood    , Spec.DK_Frost    , Spec.DK_Unholy      }),
			[Class.DH     ] = new (new List<Spec>() { Spec.DH_Vengeance, Spec.DH_Havoc    }),
			[Class.Druid  ] = new (new List<Spec>() { Spec.Druid_Bear  , Spec.Druid_Resto , Spec.Druid_Feral    , Spec.Druid_Balance }),
			[Class.Hunter ] = new (new List<Spec>() { Spec.Hunter_BM   , Spec.Hunter_MM   , Spec.Hunter_SV      }),
			[Class.Mage   ] = new (new List<Spec>() { Spec.Mage_Arcane , Spec.Mage_Fire   , Spec.Mage_Frost     }),
			[Class.Monk   ] = new (new List<Spec>() { Spec.Monk_BRM    , Spec.Monk_MW     , Spec.Monk_WW        }),
			[Class.Paladin] = new (new List<Spec>() { Spec.Paladin_Prot, Spec.Paladin_Holy, Spec.Paladin_Ret    }),
			[Class.Priest ] = new (new List<Spec>() { Spec.Priest_Disc , Spec.Priest_Holy , Spec.Priest_Shadow  }),
			[Class.Rogue  ] = new (new List<Spec>() { Spec.Rogue_Sin   , Spec.Rogue_Outlaw, Spec.Rogue_Sub      }),
			[Class.Shaman ] = new (new List<Spec>() { Spec.Shaman_Resto, Spec.Shaman_Enh  , Spec.Shaman_Ele     }),
			[Class.Warlock] = new (new List<Spec>() { Spec.Warlock_Aff , Spec.Warlock_Demo, Spec.Warlock_Destro }),
			[Class.Warrior] = new (new List<Spec>() { Spec.Warrior_Prot, Spec.Warrior_Arms, Spec.Warrior_Fury   }),
		});
	private static readonly ReadOnlyDictionary<Spec, Class> _specClasses;

	private static readonly ReadOnlyDictionary<Spec, ReadOnlyCollection<string>> _dictSpecCondensed =
		new (new ConcurrentDictionary<Spec, ReadOnlyCollection<string>>() {
		[Spec.DK_Blood ] = new (new List<string>() { "blood", "bdk" }),
		[Spec.DK_Frost ] = new (new List<string>() { "frost" }),
		[Spec.DK_Unholy] = new (new List<string>() { "unholy", "uh" }),

		[Spec.DH_Vengeance] = new (new List<string>() { "vengeance", "vdh", "veng", "venge" }),
		[Spec.DH_Havoc    ] = new (new List<string>() { "havoc" }),

		[Spec.Druid_Bear   ] = new (new List<string>() { "bear", "guardian" }),
		[Spec.Druid_Resto  ] = new (new List<string>() { "rdruid", "resto", "restoration" }),
		[Spec.Druid_Feral  ] = new (new List<string>() { "feral", "cat" }),
		[Spec.Druid_Balance] = new (new List<string>() { "balance", "moonkin", "boomy", "boomkin" }),

		[Spec.Hunter_BM] = new (new List<string>() { "beast mastery", "bm", "beast master", "beastmastery", "beastmaster" }),
		[Spec.Hunter_MM] = new (new List<string>() { "marksman", "marks", "mm", "marksmanship" }),
		[Spec.Hunter_SV] = new (new List<string>() { "survival", "sv", "surv" }),

		[Spec.Mage_Arcane] = new (new List<string>() { "arcane" }),
		[Spec.Mage_Fire  ] = new (new List<string>() { "fire" }),
		[Spec.Mage_Frost ] = new (new List<string>() { "frost", "ice" }),

		[Spec.Monk_BRM] = new (new List<string>() { "brm", "brewmaster", "brew", "br" }),
		[Spec.Monk_MW ] = new (new List<string>() { "mw", "mistweaver", "mist" }),
		[Spec.Monk_WW ] = new (new List<string>() { "ww", "windwalker" }),

		[Spec.Paladin_Prot] = new (new List<string>() { "protpal", "protection", "prot" }),
		[Spec.Paladin_Holy] = new (new List<string>() { "hpal", "holy" }),
		[Spec.Paladin_Ret ] = new (new List<string>() { "ret", "retribution" }),

		[Spec.Priest_Disc  ] = new (new List<string>() { "disc", "discipline" }),
		[Spec.Priest_Holy  ] = new (new List<string>() { "hpriest", "holy" }),
		[Spec.Priest_Shadow] = new (new List<string>() { "shadow", "spriest" }),

		[Spec.Rogue_Sin   ] = new (new List<string>() { "sin", "ass", "assassination", "assassin" }),
		[Spec.Rogue_Outlaw] = new (new List<string>() { "outlaw", "combat" }),
		[Spec.Rogue_Sub   ] = new (new List<string>() { "sub", "subtlety" }),

		[Spec.Shaman_Resto] = new (new List<string>() { "rsham", "resto", "restoration" }),
		[Spec.Shaman_Enh  ] = new (new List<string>() { "enh", "enhance", "enhancement" }),
		[Spec.Shaman_Ele  ] = new (new List<string>() { "ele", "elemental" }),

		[Spec.Warlock_Aff   ] = new (new List<string>() { "aff", "affliction", "afflock" }),
		[Spec.Warlock_Demo  ] = new (new List<string>() { "demo", "demonology", "demolock" }),
		[Spec.Warlock_Destro] = new (new List<string>() { "destro", "destruction", "destrolock" }),

		[Spec.Warrior_Prot] = new (new List<string>() { "protection", "prot" }),
		[Spec.Warrior_Arms] = new (new List<string>() { "arms" }),
		[Spec.Warrior_Fury] = new (new List<string>() { "fury" }),
	});
	private static readonly ReadOnlyDictionary<string, ReadOnlyCollection<Spec>> _dictSpec;

	private static readonly ReadOnlyDictionary<Class, string> _classNames =
		new (new ConcurrentDictionary<Class, string>() {
			[Class.DK] = "Death Knight",
			[Class.DH] = "Demon Hunter",
			[Class.Druid  ] = "Druid"  ,
			[Class.Hunter ] = "Hunter" ,
			[Class.Mage   ] = "Mage"   ,
			[Class.Monk   ] = "Monk"   ,
			[Class.Paladin] = "Paladin",
			[Class.Priest ] = "Priest" ,
			[Class.Rogue  ] = "Rogue"  ,
			[Class.Shaman ] = "Shaman" ,
			[Class.Warlock] = "Warlock",
			[Class.Warrior] = "Warrior",
		});
	private static readonly Dictionary<Spec, string> _specNames =
		new (new ConcurrentDictionary<Spec, string>() {
			[Spec.DK_Blood ] = "Blood" ,
			[Spec.DK_Frost ] = "Frost" ,
			[Spec.DK_Unholy] = "Unholy",

			[Spec.DH_Vengeance] = "Vengeance",
			[Spec.DH_Havoc    ] = "Havoc"    ,

			[Spec.Druid_Bear   ] = "Guardian"   ,
			[Spec.Druid_Resto  ] = "Restoration",
			[Spec.Druid_Feral  ] = "Feral"      ,
			[Spec.Druid_Balance] = "Balance"    ,

			[Spec.Hunter_BM] = "Beast Mastery",
			[Spec.Hunter_MM] = "Marksmanship" ,
			[Spec.Hunter_SV] = "Survival"     ,

			[Spec.Mage_Arcane] = "Arcane",
			[Spec.Mage_Fire  ] = "Fire"  ,
			[Spec.Mage_Frost ] = "Frost" ,

			[Spec.Monk_BRM] = "Brewmaster",
			[Spec.Monk_MW ] = "Mistweaver",
			[Spec.Monk_WW ] = "Windwalker",

			[Spec.Paladin_Prot] = "Protection" ,
			[Spec.Paladin_Holy] = "Holy"       ,
			[Spec.Paladin_Ret ] = "Retribution",

			[Spec.Priest_Disc  ] = "Discipline",
			[Spec.Priest_Holy  ] = "Holy"      ,
			[Spec.Priest_Shadow] = "Shadow"    ,

			[Spec.Rogue_Sin   ] = "Assassination",
			[Spec.Rogue_Outlaw] = "Outlaw"       ,
			[Spec.Rogue_Sub   ] = "Subtlety"     ,

			[Spec.Shaman_Resto] = "Restoration",
			[Spec.Shaman_Enh  ] = "Enhancement",
			[Spec.Shaman_Ele  ] = "Elemental"  ,

			[Spec.Warlock_Aff   ] = "Affliction" ,
			[Spec.Warlock_Demo  ] = "Demonology" ,
			[Spec.Warlock_Destro] = "Destruction",

			[Spec.Warrior_Prot] = "Protection",
			[Spec.Warrior_Arms] = "Arms"      ,
			[Spec.Warrior_Fury] = "Fury"      ,
		});

	// Force static initializer to run.
	public static void Init() { return; }
	// Initialize dictionary caches with redundant indices
	// (improves performance at the cost of memory space).
	static ClassSpec() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		// Initialize Spec->Class from Class->Specs.
		ConcurrentDictionary<Spec, Class> specClasses = new ();
		foreach (Class @class in _classSpecs.Keys) {
			foreach (Spec spec in _classSpecs[@class])
				specClasses.TryAdd(spec, @class);
		}
		_specClasses = new (specClasses);

		// Initialize _dictSpec from the condensed list.
		ConcurrentDictionary<string, List<Spec>> dictSpec_list = new ();
		foreach (Spec spec in _dictSpecCondensed.Keys) {
			foreach (string token in _dictSpecCondensed[spec]) {
				if (!dictSpec_list.ContainsKey(token))
					dictSpec_list.TryAdd(token, new List<Spec>());
				dictSpec_list[token].Add(spec);
			}
		}
		ConcurrentDictionary<string, ReadOnlyCollection<Spec>> dictSpec = new ();
		foreach (string token in dictSpec_list.Keys) {
			dictSpec.TryAdd(
				token,
				new ReadOnlyCollection<Spec>(dictSpec_list[token])
			);
		}
		_dictSpec = new (dictSpec);

		Log.Information("  Initialized module: Role-Class-Spec");
		Log.Debug("    Conversion cache initialized.");
		stopwatch.LogMsecDebug("    Took {Time} msec.");
	}

	// Returns a list of FullNames of Specs matching the input.
	public static IReadOnlyList<string> ParseSpec(string input) {
		input = input.Replace(" ", "").ToLower();

		HashSet<Spec> specs = new ();
		foreach (string key in _dictSpec.Keys) {
			if (key.Contains(input)) {
				foreach (Spec spec in _dictSpec[key])
					specs.Add(spec);
			}
		}

		List<string> spec_names = new ();
		foreach (Spec spec in specs)
			spec_names.Add(spec.FullName());
		return spec_names;
	}

	// Display names of classes/specs.
	public static string Name(this Class @class) =>
		_classNames[@class];
	public static string Name(this Spec spec) =>
		_specNames[spec];
	public static string FullName(this Spec spec) =>
		$"{spec.Name()} {_classNames[spec.GetClass()]}";

	// Emojis associated with roles/classes.
	public static DiscordEmoji Emoji(this Role role) =>
		Emojis[_tableRoleEmoji[role]];
	public static DiscordEmoji Emoji(this Class @class) =>
		Emojis[_tableClassEmoji[@class]];

	// The categories a Spec falls into.
	public static Class GetClass(this Spec spec) =>
		_specClasses[spec];
	public static Role GetRole(this Spec spec) =>
		_specRoles[spec];
}
