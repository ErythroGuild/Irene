using System.Diagnostics;

namespace Irene;

class ClassSpec {
	// "Multiple" is public-only. Private methods will always
	// return a list instead.
	public enum Role {
		Multiple,
		Tank, Heal, DPS,
	}
	public enum Class {
		Multiple,
		DK, DH, Druid, Hunter,
		Mage, Monk, Paladin, Priest,
		Rogue, Shaman, Warlock, Warrior,
	}
	public enum Spec {
		Multiple,
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
	static readonly Dictionary<Role, ulong> dict_emoji_role = new () {
		{ Role.Tank, id_e.tank },
		{ Role.Heal, id_e.heal },
		{ Role.DPS , id_e.dps  },
	};
	static readonly Dictionary<Class, ulong> dict_emoji_class = new () {
		{ Class.DK     ,id_e.dk      },
		{ Class.DH     ,id_e.dh      },
		{ Class.Druid  ,id_e.druid   },
		{ Class.Hunter ,id_e.hunter  },
		{ Class.Mage   ,id_e.mage    },
		{ Class.Monk   ,id_e.monk    },
		{ Class.Paladin,id_e.paladin },
		{ Class.Priest ,id_e.priest  },
		{ Class.Rogue  ,id_e.rogue   },
		{ Class.Shaman ,id_e.shaman  },
		{ Class.Warlock,id_e.warlock },
		{ Class.Warrior,id_e.warrior },
	};

	static readonly Dictionary<Spec, Role> spec_to_role = new () {
		{ Spec.DK_Blood,  Role.Tank },
		{ Spec.DK_Frost,  Role.DPS  },
		{ Spec.DK_Unholy, Role.DPS  },

		{ Spec.DH_Vengeance, Role.Tank },
		{ Spec.DH_Havoc,     Role.DPS  },

		{ Spec.Druid_Bear,    Role.Tank },
		{ Spec.Druid_Resto,   Role.Heal },
		{ Spec.Druid_Balance, Role.DPS  },
		{ Spec.Druid_Feral,   Role.DPS  },

		{ Spec.Hunter_BM, Role.DPS },
		{ Spec.Hunter_MM, Role.DPS },
		{ Spec.Hunter_SV, Role.DPS },

		{ Spec.Mage_Arcane, Role.DPS },
		{ Spec.Mage_Fire,   Role.DPS },
		{ Spec.Mage_Frost,  Role.DPS },

		{ Spec.Monk_BRM, Role.Tank },
		{ Spec.Monk_MW,  Role.Heal },
		{ Spec.Monk_WW,  Role.DPS  },

		{ Spec.Paladin_Prot, Role.Tank },
		{ Spec.Paladin_Holy, Role.Heal },
		{ Spec.Paladin_Ret,  Role.DPS  },

		{ Spec.Priest_Disc,   Role.Heal },
		{ Spec.Priest_Holy,   Role.Heal },
		{ Spec.Priest_Shadow, Role.DPS  },

		{ Spec.Rogue_Sin,    Role.DPS },
		{ Spec.Rogue_Outlaw, Role.DPS },
		{ Spec.Rogue_Sub,    Role.DPS },

		{ Spec.Shaman_Resto, Role.Heal },
		{ Spec.Shaman_Enh,   Role.DPS  },
		{ Spec.Shaman_Ele,   Role.DPS  },

		{ Spec.Warlock_Aff,    Role.DPS },
		{ Spec.Warlock_Demo,   Role.DPS },
		{ Spec.Warlock_Destro, Role.DPS },

		{ Spec.Warrior_Prot, Role.Tank },
		{ Spec.Warrior_Arms, Role.DPS  },
		{ Spec.Warrior_Fury, Role.DPS  },
	};
	static readonly Dictionary<Role, List<Spec>> role_to_specs;

	static readonly Dictionary<Class, List<Spec>> class_to_specs = new () {
		{ Class.DK,      new () { Spec.DK_Blood, Spec.DK_Frost, Spec.DK_Unholy } },
		{ Class.DH,      new () { Spec.DH_Vengeance, Spec.DH_Havoc } },
		{ Class.Druid,   new () { Spec.Druid_Bear, Spec.Druid_Resto, Spec.Druid_Feral, Spec.Druid_Balance } },
		{ Class.Hunter,  new () { Spec.Hunter_BM, Spec.Hunter_MM, Spec.Hunter_SV } },
		{ Class.Mage,    new () { Spec.Mage_Arcane, Spec.Mage_Fire, Spec.Mage_Frost } },
		{ Class.Monk,    new () { Spec.Monk_BRM, Spec.Monk_MW, Spec.Monk_WW } },
		{ Class.Paladin, new () { Spec.Paladin_Prot, Spec.Paladin_Holy, Spec.Paladin_Ret } },
		{ Class.Priest,  new () { Spec.Priest_Disc, Spec.Priest_Holy, Spec.Priest_Shadow } },
		{ Class.Rogue,   new () { Spec.Rogue_Sin, Spec.Rogue_Outlaw, Spec.Rogue_Sub } },
		{ Class.Shaman,  new () { Spec.Shaman_Resto, Spec.Shaman_Enh, Spec.Shaman_Ele } },
		{ Class.Warlock, new () { Spec.Warlock_Aff, Spec.Warlock_Demo, Spec.Warlock_Destro } },
		{ Class.Warrior, new () { Spec.Warrior_Prot, Spec.Warrior_Arms, Spec.Warrior_Fury } },
	};
	static readonly Dictionary<Spec, Class> spec_to_class;

	static readonly Dictionary<Role, List<string>> dict_role = new () {
		{ Role.Tank, new () { "tank", "tanking", "tanks" } },
		{ Role.Heal, new () { "heal", "healing", "heals", "healer", "healers" } },
		{ Role.DPS,  new () { "dps", "deeps", "damage", "dd" } },
	};
	static readonly Dictionary<Class, List<string>> dict_class = new () {
		{ Class.DK,      new () { "death knight", "dk", "deathknight", "ebon blade", "acherus", "ebon hold" } },
		{ Class.DH,      new () { "demon hunter", "dh", "demonhunter", "illidari", "fel hammer", "mardum" } },
		{ Class.Druid,   new () { "druid", "druidess", "dreamgrove", "emerald dream", "dreamway" } },
		{ Class.Hunter,  new () { "hunter", "huntress", "trueshot lodge", "trueshot" } },
		{ Class.Mage,    new () { "mage", "tirisgarde", "tirisfal", "hall of the guardian", "violet citadel", "altered time" } },
		{ Class.Monk,    new () { "monk", "temple of five dawns", "wandering isle", "peak of serenity", "peak of xuen" } },
		{ Class.Paladin, new () { "paladin", "pally", "pal", "silver hand", "sanctum of light", "light's hope chapel", "light's hope", "hammer of wrath" } },
		{ Class.Priest,  new () { "priest", "conclave", "netherlight temple", "focused will" } },
		{ Class.Rogue,   new () { "rogue", "hall of shadows", "ravenholdt" } },
		{ Class.Shaman,  new () { "shaman", "shammy", "sham", "heart of azeroth", "earthshrine", "ancestral guidance" } },
		{ Class.Warlock, new () { "warlock", "lock", "black harvest", "dreadscar rift", "dreadscar" } },
		{ Class.Warrior, new () { "warrior", "warr", "skyhold" } },
	};
	static readonly Dictionary<Spec, List<string>> dict_spec = new () {
		{ Spec.DK_Blood,  new () { "blood", "bdk" } },
		//{ Spec.DK_Frost,  new () { } }, // "frost": ambiguous
		{ Spec.DK_Unholy, new () { "unholy", "uh" } },

		{ Spec.DH_Vengeance, new () { "vengeance", "vdh", "veng", "venge" } },
		{ Spec.DH_Havoc,     new () { "havoc" } },

		{ Spec.Druid_Bear,    new () { "bear", "guardian" } },
		{ Spec.Druid_Resto,   new () { "rdruid" } }, // "resto", "restoration": ambiguous
		{ Spec.Druid_Feral,   new () { "feral", "cat" } },
		{ Spec.Druid_Balance, new () { "balance", "moonkin", "boomy", "boomkin" } },

		{ Spec.Hunter_BM, new () { "beast mastery", "bm", "beast master", "beastmastery", "beastmaster" } },
		{ Spec.Hunter_MM, new () { "marksman", "marks", "mm", "marksmanship" } },
		{ Spec.Hunter_SV, new () { "survival", "sv", "surv" } },

		{ Spec.Mage_Arcane, new () { "arcane" } },
		{ Spec.Mage_Fire,   new () { "fire" } },
		//{ Spec.Mage_Frost,  new () { } }, // "frost": ambiguous

		{ Spec.Monk_BRM, new () { "brm", "brewmaster", "brew", "br" } },
		{ Spec.Monk_MW,  new () { "mw", "mistweaver", "mist" } },
		{ Spec.Monk_WW,  new () { "ww", "windwalker" } },

		{ Spec.Paladin_Prot, new () { "protpal" } }, // "protection", "prot": ambiguous
		{ Spec.Paladin_Holy, new () { "hpal" } }, // "holy": ambiguous
		{ Spec.Paladin_Ret,  new () { "ret", "retribution" } },

		{ Spec.Priest_Disc,   new () { "disc", "discipline" } },
		{ Spec.Priest_Holy,   new () { "hpriest" } }, // "holy": ambiguous
		{ Spec.Priest_Shadow, new () { "shadow", "spriest" } },

		{ Spec.Rogue_Sin,    new () { "sin", "ass", "assassination", "assassin" } },
		{ Spec.Rogue_Outlaw, new () { "outlaw", "combat" } },
		{ Spec.Rogue_Sub,    new () { "sub", "subtlety" } },

		{ Spec.Shaman_Resto, new () { "rsham" } }, // "resto", "restoration": ambiguous
		{ Spec.Shaman_Enh,   new () { "enh", "enhance", "enhancement" } },
		{ Spec.Shaman_Ele,   new () { "ele", "elemental" } },

		{ Spec.Warlock_Aff,    new () { "aff", "affliction", "afflock" } },
		{ Spec.Warlock_Demo,   new () { "demo", "demonology", "demolock" } },
		{ Spec.Warlock_Destro, new () { "destro", "destruction", "destrolock" } },

		//{ Spec.Warrior_Prot, new () { } }, // "protection", "prot": ambiguous
		{ Spec.Warrior_Arms, new () { "arms" } },
		{ Spec.Warrior_Fury, new () { "fury" } },
	};

	static readonly Dictionary<string, List<Spec>> dict_multi = new () {
		{ "frost",       new () { Spec.DK_Frost, Spec.Mage_Frost } },
		{ "resto",       new () { Spec.Druid_Resto, Spec.Shaman_Resto } },
		{ "restoration", new () { Spec.Druid_Resto, Spec.Shaman_Resto } },
		{ "holy",        new () { Spec.Paladin_Holy, Spec.Priest_Holy } },
		{ "prot",        new () { Spec.Paladin_Prot, Spec.Warrior_Prot } },
		{ "protection",  new () { Spec.Paladin_Prot, Spec.Warrior_Prot } },
	};

	private static readonly Dictionary<Class, string> _classNames = new () {
		{ Class.DK, "Death Knight" },
		{ Class.DH, "Demon Hunter" },
		{ Class.Druid  , "Druid"   },
		{ Class.Hunter , "Hunter"  },
		{ Class.Mage   , "Mage"    },
		{ Class.Monk   , "Monk"    },
		{ Class.Paladin, "Paladin" },
		{ Class.Priest , "Priest"  },
		{ Class.Rogue  , "Rogue"   },
		{ Class.Shaman , "Shaman"  },
		{ Class.Warlock, "Warlock" },
		{ Class.Warrior, "Warrior" },
	};

	// The maximum number of separate tokens allowed.
	// Any further tokens past the last one will be treated as a
	// single token.
	private const int token_cap = 8;

	// Initialize dictionary caches with redundant indices
	// (improves performance at the cost of memory space).
	static ClassSpec() {
		Log.Debug("Initializing Role/Spec/Class conversion cache.");
		Stopwatch stopwatch = Stopwatch.StartNew();

		// Initialize Spec->Class from Class->Specs.
		spec_to_class = new Dictionary<Spec, Class>();
		foreach (Class @class in class_to_specs.Keys) {
			foreach (Spec spec in class_to_specs[@class]) {
				spec_to_class.Add(spec, @class);
			}
		}

		// Initialize Role->Specs from Spec->Role.
		role_to_specs = new Dictionary<Role, List<Spec>> {
			{ Role.Tank, new List<Spec>() },
			{ Role.Heal, new List<Spec>() },
			{ Role.DPS,  new List<Spec>() },
		};
		foreach (Spec spec in spec_to_role.Keys) {
			Role role = spec_to_role[spec];
			List<Spec> list = role_to_specs[role];
			list.Add(spec);
		}

		stopwatch.Stop();
		Log.Debug("  Conversion cache initialized.");
		Log.Debug($"  Took {stopwatch.ElapsedMilliseconds} msec.");
	}

	public static string Name(Class @class) =>
		_classNames[@class];

	// Get the emoji associated with the Role.
	// Returns an empty string if `Role.Multiple` is passed, or
	// if emojis haven't been initialized yet.
	public static string role_emoji(Role role) {
		if (role == Role.Multiple)
			return "";

		if (Guild is null)
			return "";

		ulong id = dict_emoji_role[role];
		DiscordEmoji emoji = Emojis[id];
		return emoji.ToString();
	}

	// Get the emoji associated with the Class.
	// Returns an empty string if `Class.Multiple` is passed, or
	// if emojis haven't been initialized yet.
	public static string class_emoji(Class @class) {
		if (@class == Class.Multiple)
			return "";

		if (Guild is null)
			return "";

		ulong id = dict_emoji_class[@class];
		DiscordEmoji emoji = Emojis[id];
		return emoji.ToString();
	}

	// Get the Class of the Spec.
	// Returns Class.Multiple for Spec.Multiple.
	public static Class get_class(Spec spec) => spec switch {
		Spec.Multiple => Class.Multiple,
		_ => spec_to_class[spec],
	};

	// Get the Role of the Spec.
	// Returns Role.Multiple for Spec.Multiple.
	public static Role get_role(Spec spec) => spec switch {
		Spec.Multiple => Role.Multiple,
		_ => spec_to_role[spec],
	};

	// Find the closest exact match to the given input if possible.
	// May return none (null) or Role.Multiple.
	public static Role? parse_role(string arg) {
		List<string> tokens = process_arg(arg);

		// Search input for roles.
		HashSet<Role> roles = find_roles(tokens);
		// Return if exactly 1 match is found.
		if (roles.Count == 1) {
			return get_hashset_value(roles);
		}

		// Search input for specs.
		HashSet<Spec> specs = find_specs(tokens);
		// Convert specs to roles.
		HashSet<Role> roles_spec = new ();
		foreach (Spec spec in specs) {
			roles_spec.Add(spec_to_role[spec]);
		}
		// Combine spec data with role data.
		roles = combine_sets(roles, roles_spec);
		// Return if exactly 1 match is found.
		if (roles.Count == 1) {
			return get_hashset_value(roles);
		}

		// Search input for classes.
		HashSet<Class> classes = find_classes(tokens);
		// Convert classes to specs, then to roles.
		HashSet<Role> roles_class = new ();
		foreach (Class @class in classes) {
			List<Spec> list = class_to_specs[@class];
			foreach (Spec spec in list) {
				roles_class.Add(spec_to_role[spec]);
			}
		}
		// Combine class data with role and spec data.
		roles = combine_sets(roles, roles_class);

		// Decide what to return.
		if (roles.Count == 1) {
			return get_hashset_value(roles);
		} else if (roles.Count == 0) {
			return null;
		} else {
			return Role.Multiple;
		}
	}
	// Will never return Role.Multiple.
	static HashSet<Role> find_roles(List<string> tokens) {
		HashSet<Role> roles = new ();
		foreach (string token in tokens) {
			foreach (Role role in dict_role.Keys) {
				if (dict_role[role].Contains(token)) {
					roles.Add(role);
					break;
				}
			}
		}
		return roles;
	}

	// Find the closest exact match to the given input if possible.
	// May return none (null) or Class.Multiple.
	public static Class? parse_class(string arg) {
		List<string> tokens = process_arg(arg);

		// Search input for classes.
		HashSet<Class> classes = find_classes(tokens);
		// Return if exactly 1 match is found.
		if (classes.Count == 1) {
			return get_hashset_value(classes);
		}

		// Search input for specs.
		HashSet<Spec> specs = find_specs(tokens);
		// Convert specs to classes.
		HashSet<Class> classes_spec = new ();
		foreach (Spec spec in specs) {
			classes_spec.Add(spec_to_class[spec]);
		}
		// Combine spec data with class data.
		classes = combine_sets(classes, classes_spec);
		// Return if exactly 1 match is found.
		if (classes.Count == 1) {
			return get_hashset_value(classes);
		}

		// Search input for roles.
		HashSet<Role> roles = find_roles(tokens);
		// Convert roles to specs, then to classes.
		HashSet<Class> classes_role = new ();
		foreach (Role role in roles) {
			List<Spec> list = role_to_specs[role];
			foreach (Spec spec in list) {
				classes_role.Add(spec_to_class[spec]);
			}
		}
		// Combine role data with class and spec data.
		classes = combine_sets(classes, classes_role);

		// Decide what to return.
		if (classes.Count == 1) {
			return get_hashset_value(classes);
		} else if (classes.Count == 0) {
			return null;
		} else {
			return Class.Multiple;
		}
	}
	// Will never return Class.Multiple.
	static HashSet<Class> find_classes(List<string> tokens) {
		HashSet<Class> classes = new ();
		foreach (string token in tokens) {
			foreach (Class @class in dict_class.Keys) {
				if (dict_class[@class].Contains(token)) {
					classes.Add(@class);
					break;
				}
			}
		}
		return classes;
	}

	// Find the closest exact match to the given input if possible.
	// May return none (null) or Spec.Multiple.
	public static Spec? parse_spec(string arg) {
		List<string> tokens = process_arg(arg);

		// Search input for specs.
		HashSet<Spec> specs = find_specs(tokens);
		// Return if exactly 1 match is found.
		if (specs.Count == 1) {
			return get_hashset_value(specs);
		}

		// Search input for classes.
		HashSet<Class> classes = find_classes(tokens);
		// Convert classes to specs.
		HashSet<Spec> specs_class = new ();
		foreach (Class @class in classes) {
			specs_class.UnionWith(class_to_specs[@class]);
		}
		// Combine spec data with class data.
		specs = combine_sets(specs, specs_class);
		// Return if exactly 1 match is found.
		if (specs.Count == 1) {
			return get_hashset_value(specs);
		}

		// Search input for roles.
		HashSet<Role> roles = find_roles(tokens);
		// Convert roles to specs.
		HashSet<Spec> specs_role = new ();
		foreach (Role role in roles) {
			List<Spec> list = role_to_specs[role];
			specs_role.UnionWith(list);
		}
		// Combine role data with class and spec data.
		specs = combine_sets(specs, specs_role);

		// Decide what to return.
		if (specs.Count == 1) {
			return get_hashset_value(specs);
		} else if (specs.Count == 0) {
			return null;
		} else {
			return Spec.Multiple;
		}
	}
	// Will never return Spec.Multiple.
	static HashSet<Spec> find_specs(List<string> tokens) {
		HashSet<Spec> specs = new ();
		foreach (string token in tokens) {
			foreach (Spec spec in dict_spec.Keys) {
				if (dict_spec[spec].Contains(token)) {
					specs.Add(spec);
					break;
				}
			}
			if (dict_multi.ContainsKey(token)) {
				specs.UnionWith(dict_multi[token]);
			}
		}
		return specs;
	}

	// Splits an arg on spaces, respecting the token cap, and
	// then returns the partitioned (permuted) list.
	static List<string> process_arg(string arg) {
		List<string> tokens = new (arg.Split(' ', token_cap));
		return permute_tokens(tokens);
	}
	
	// Generates a List of (partitioned) tokens, given a simple
	// list of tokens. Number of tokens is capped.
	// O(N) running time. The search itself may be more than O(N).
	static List<string> permute_tokens(List<string> tokens) {
		// Append tokens that go over cap onto the last token.
		if (tokens.Count > token_cap) {
			for (int i = token_cap; i < tokens.Count; i++) {
				tokens[token_cap - 1] += $" {tokens[i]}";
			}
			tokens.RemoveRange(token_cap, tokens.Count - token_cap);
		}

		// Permute the list.
		List<string> output = new ();
		int count = tokens.Count;
		// `n`: number of tokens to concatenate
		for (int n = 0; n < count; n++) {
			// `a`: the term to concatenate to
			for (int a = 0; a < count - n; a++) {
				string token = tokens[a];
				// `i`: the token being concatenated
				for (int i = 0; i < n; i++) {
					token += $" {token[a + n]}";
				}
				output.Add(token);
			}
		}

		return output;
	}

	// Extracts the singular value from a HashSet.
	// If the HashSet does not contain exactly one value, throws
	// an ArgumentException.
	static T get_hashset_value<T>(HashSet<T> set) {
		if (set.Count != 1) {
			throw new ArgumentException("HashSet must have exactly 1 member.");
		}
		List<T> list = new (set);
		return list[0];
	}

	// If the base set is empty, return just the add set.
	// If the base set is not empty, return the intersect of the two.
	static HashSet<T> combine_sets<T>(HashSet<T> @base, HashSet<T> add) {
		if (@base.Count == 0) {
			@base = add;
		} else {
			@base.IntersectWith(add);
		}
		return @base;
	}
}
