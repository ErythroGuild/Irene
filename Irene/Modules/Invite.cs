namespace Irene.Modules;

class Invite {
	public enum Server { Erythro, Leuko }

	private const string
		_urlErythro = @"https://discord.gg/ADzEwNS",
		_urlLeuko   = @"https://discord.gg/zhadQf59xq";

	public static string GetInvite(Server server) => server switch {
		Server.Erythro => _urlErythro,
		Server.Leuko   => _urlLeuko  ,
		_ => throw new ArgumentException("Unknown invite selection.", nameof(server)),
	};
}
