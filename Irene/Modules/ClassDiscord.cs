using static Irene.ClassSpec;

namespace Irene.Modules;

class ClassDiscord {
	private const string
		_urlDH      = @"https://discord.gg/felhammer"     ,
		_urlDK      = @"https://discord.gg/acherus"       ,
		_urlDruid   = @"https://discord.gg/dreamgrove"    ,
		_urlEvoker  = @"https://discord.gg/evoker"        ,
		_urlHunter  = @"https://discord.gg/trueshot"      ,
		_urlMage    = @"https://discord.gg/makGfZA"       ,
		_urlMonk    = @"https://discord.gg/peakofserenity",
		_urlPaladin = @"https://discord.gg/hammerofwrath" ,
		_urlRogue   = @"https://discord.gg/ravenholdt"    ,
		_urlWarlock = @"https://discord.gg/blackharvest"  ,
		_urlWarrior = @"https://discord.gg/skyhold"       ,

		_urlPriest =
			$"""
			https://discord.gg/warcraftpriests
			https://discord.gg/focusedwill (disc-only)
			""",
		_urlShaman =
			$"""
			https://discord.gg/earthshrine
			https://discord.gg/AcTek6e (resto-only)
			""";

	public static string GetInvite(Class @class) => @class switch {
		Class.DH      => _urlDH     ,
		Class.DK      => _urlDK     ,
		Class.Druid   => _urlDruid  ,
		Class.Evoker  => _urlEvoker ,
		Class.Hunter  => _urlHunter ,
		Class.Mage    => _urlMage   ,
		Class.Monk    => _urlMonk   ,
		Class.Paladin => _urlPaladin,
		Class.Priest  => _urlPriest ,
		Class.Rogue   => _urlRogue  ,
		Class.Shaman  => _urlShaman ,
		Class.Warlock => _urlWarlock,
		Class.Warrior => _urlWarrior,
		_ => throw new ArgumentException("Unknown class.", nameof(@class)),
	};
}
