namespace Irene.Commands;

class Invite : AbstractCommand {
	private const string
		_optionErythro = "erythro",
		_optionLeuko   = "leuko";
	private const string
		_urlErythro = @"https://discord.gg/ADzEwNS",
		_urlLeuko   = @"https://discord.gg/zhadQf59xq";

	public override List<string> HelpPages =>
		new () { new List<string> {
			@"`/invite erythro` fetches the server invite for this server.",
			@"`/invite leuko` fetches the server invite for the FFXIV sister server.",
			$"These invite links can also be found in {Channels![id_ch.resources].Mention}."
		}.ToLines() };

	public override List<InteractionCommand> SlashCommands =>
		new () {
			new ( new (
				"invite",
				"Show invite links for the guild discord servers.",
				new List<CommandOption> { new (
					"server",
					"The server to get an invite link to.",
					ApplicationCommandOptionType.String,
					required: false,
					new List<CommandOptionEnum> {
						new ("Erythro", _optionErythro),
						new ("Leuko", _optionLeuko),
					} ) },
				defaultPermission: true,
				ApplicationCommandType.SlashCommand
			), Command.DeferVisibleAsync, RunAsync )
		};

	public static async Task RunAsync(TimedInteraction interaction) {
		// Select the correct invite to return.
		List<DiscordInteractionDataOption> args =
			interaction.GetArgs();
		string server = (args.Count > 0)
			? (string)args[0].Value
			: _optionErythro;
		string invite = server switch {
			_optionErythro => _urlErythro,
			_optionLeuko   => _urlLeuko,
			_ => throw new ArgumentException("Invalid slash command parameter."),
		};

		// Send invite link.
		await Command.SubmitResponseAsync(
			interaction,
			invite,
			"Sending invite link.",
			LogLevel.Debug,
			"Invite link for \"{Server}\": {Link}".AsLazy(),
			server, invite
		);
	}
}
