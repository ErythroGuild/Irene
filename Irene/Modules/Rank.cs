namespace Irene.Modules;

using Option = ISelector.Entry;

class Rank {
	public enum AccessLevel {
		None,
		Guest, Member, Officer,
		Admin,
	};

	// Cached rank role objects, ordered in descending ranks.
	// This allows for more efficient rank checking.
	private static readonly IReadOnlyList<DiscordRole> _rankRoles;
	// Role object conversion tables.
	private static readonly ConstBiMap<AccessLevel, ulong> _rankRoleIds = new (
		new Dictionary<AccessLevel, ulong> {
			[AccessLevel.Admin  ] = id_r.admin  ,
			[AccessLevel.Officer] = id_r.officer,
			[AccessLevel.Member ] = id_r.member ,
			[AccessLevel.Guest  ] = id_r.guest  ,
		}
	);
	// Select menu definitions.
	private static IReadOnlyList<(AccessLevel, Option)> RankOptions =>
		new List<(AccessLevel, Option)> {
			new (AccessLevel.Admin, new Option {
				Label = "Admin",
				Id = _optionRankAdmin,
				Emoji = new (id_e.rankAdmin),
				Description = "Guild Master.",
			}),
			new (AccessLevel.Officer, new Option {
				Label = "Officer",
				Id = _optionRankOfficer,
				Emoji = new (id_e.rankOfficer),
				Description = "Officer / moderator.",
			}),
			new (AccessLevel.Member, new Option {
				Label = "Member",
				Id = _optionRankMember,
				Emoji = new (id_e.rankMember),
				Description = "Trusted member.",
			}),
			new (AccessLevel.Guest, new Option {
				Label = "Guest",
				Id = _optionRankGuest,
				Emoji = new (id_e.rankGuest),
				Description = "Verified member.",
			}),
			new (AccessLevel.None, new Option {
				Label = "No Rank",
				Id = _optionRankNone,
				Emoji = new (id_e.rankNone),
				Description = "No rank assigned.",
			}),
		};

	private const string
		_optionRankAdmin   = "option_admin"  ,
		_optionRankOfficer = "option_officer",
		_optionRankMember  = "option_member" ,
		_optionRankGuest   = "option_guest"  ,
		_optionRankNone    = "option_none"   ;

	static Rank() {
		CheckErythroInit();

		_rankRoles = new List<DiscordRole> {
			Erythro.Role(id_r.guest),
			Erythro.Role(id_r.member),
			Erythro.Role(id_r.officer),
			Erythro.Role(id_r.admin),
		};
	}

	// Returns the "color dot" emoji associated with the given rank.
	public static DiscordEmoji Emoji(AccessLevel rank) {
		CheckErythroInit();
		ulong emoji = rank switch {
			AccessLevel.None    => id_e.rankNone   ,
			AccessLevel.Guest   => id_e.rankGuest  ,
			AccessLevel.Member  => id_e.rankMember ,
			AccessLevel.Officer => id_e.rankOfficer,
			AccessLevel.Admin   => id_e.rankAdmin  ,
			_ => throw new UnclosedEnumException(typeof(AccessLevel), rank),
		};
		return Erythro.Emoji(emoji);
	}

	public static async Task<AccessLevel> GetRank(DiscordUser user) {
		DiscordMember member = await user.ToMember()
			?? throw new ArgumentException("Could not fetch member data for user.", nameof(user));
		return GetRank(member);
	}
	public static AccessLevel GetRank(DiscordMember member) {
		AccessLevel rankMax = AccessLevel.None;
		// Although checking against a cached set of DiscordRoles could
		// be (marginally) more performant, the cache could easily get
		// outdated and result in an inaccurate rank.
		foreach (DiscordRole role in member.Roles) {
			if (_rankRoleIds.Contains(role.Id)) {
				AccessLevel rank = _rankRoleIds[role.Id];
				if (rank > rankMax)
					rankMax = rank;
			}
		}
		return rankMax;
	}

	// Fetches a list of all members with the roles Guest & <Erythro>.
	// Sorted by date joined, with the oldest members listed first.
	public static IReadOnlyList<DiscordMember> GetTrials() {
		CheckErythroInit();

		DiscordRole role = Erythro.Guild.GetRole(id_r.erythro);
		HashSet<DiscordMember> members = new (Erythro.Guild.Members.Values);

		List<DiscordMember> trials = new ();
		foreach (DiscordMember member in members) {
			if (IsTrial(member))
				trials.Add(member);
		}

		trials.Sort((a, b) => a.JoinedAt.CompareTo(b.JoinedAt));

		return trials;
	}
	// It's better to have a separate method for this, because checking
	// the rank of a user already requires a loop, and any other method
	// would always double the performance cost of the call.
	public static bool IsTrial(DiscordMember member) {
		bool isErythro = false;
		bool isGuest = false;

		// Reasoning for not using a cache is the same as the reasoning
		// for GetRank().
		foreach (DiscordRole role in member.Roles) {
			if (role.Id == id_r.erythro) {
				isErythro = true;
				continue;
			}

			if (_rankRoleIds.Contains(role.Id)) {
				AccessLevel rank = _rankRoleIds[role.Id];
				// Return early if rank is above Guest.
				if (rank > AccessLevel.Guest)
					return false;
				else if (rank == AccessLevel.Guest)
					isGuest = true;
			}
		}

		return isErythro && isGuest;
	}

}
