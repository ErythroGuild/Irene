namespace Irene.Modules;

class Invite {
	public enum Server { Erythro, Leuko, Bnet }

	private const string
		_urlErythro = @"https://discord.gg/ADzEwNS",
		_urlLeuko   = @"https://discord.gg/zhadQf59xq",
		_urlBnet    = @"https://blizzard.com/invite/YexbxZph70R";

	public static string GetInvite(Server server) => server switch {
		Server.Erythro => _urlErythro,
		Server.Leuko   => _urlLeuko  ,
		Server.Bnet    => _urlBnet   ,
		_ => throw new UnclosedEnumException(typeof(Server), server),
	};
}
