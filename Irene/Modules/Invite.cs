namespace Irene.Modules;

class Invite {
	public const string
		Id_Erythro = "erythro",
		Id_Leuko   = "leuko";
	private const string
		_urlErythro = @"https://discord.gg/ADzEwNS",
		_urlLeuko   = @"https://discord.gg/zhadQf59xq";

	public static string GetInvite(string id) => id switch {
		Id_Erythro => _urlErythro,
		Id_Leuko   => _urlLeuko  ,
		_ => throw new ArgumentException("Unknown invite selection.", nameof(id)),
	};
}
