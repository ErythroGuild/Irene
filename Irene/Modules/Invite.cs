using static Irene.Commands.Invite;

namespace Irene.Modules;

class Invite {
	private const string
		_urlErythro = @"https://discord.gg/ADzEwNS",
		_urlLeuko   = @"https://discord.gg/zhadQf59xq";

	public static string GetInvite(string id) => id switch {
		Option_Erythro => _urlErythro,
		Option_Leuko   => _urlLeuko  ,
		_ => throw new ArgumentException("Unknown invite selection.", nameof(id)),
	};
}
