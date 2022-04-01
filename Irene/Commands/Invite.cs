namespace Irene.Commands;

class Invite : ICommand {
	private const string
		_optErythro = "erythro",
		_optLeuko   = "leuko";
	private const string
		_urlErythro = @"https://discord.gg/ADzEwNS",
		_urlLeuko   = @"https://discord.gg/zhadQf59xq";

	public static List<string> HelpPages { get =>
		new () { string.Join("\n", new List<string> {
			@"`/invite Erythro` fetches the server invite for this server.",
			@"`/invite Leuko` fetches the server invite for the FFXIV sister server.",
			$@"These invite lines can also be found in {Channels[id_ch.resources]}."
		} )
	}; }

	public static List<InteractionCommand> SlashCommands { get =>
		new () {
			new ( new (
				"invite",
				"Show invite links for the guild discord servers.",
				new List<CommandOption> { new (
					"server",
					"The server to get an invite link to.",
					ApplicationCommandOptionType.String,
					true,
					new List<CommandOptionEnum> {
						new ("Erythro", _optErythro),
						new ("Leuko", _optLeuko)
					} ) },
				true,
				ApplicationCommandType.SlashCommand
			), RunAsync )
		};
	}

	public static List<InteractionCommand> UserCommands    { get => new (); }
	public static List<InteractionCommand> MessageCommands { get => new (); }

	private static async Task RunAsync(DiscordInteraction interaction, Stopwatch stopwatch) {
		// Select the correct invite to return.
		List<DiscordInteractionDataOption> options =
			interaction.GetOptions();
		string server = (string)options[0].Value;
		string invite = server switch {
			_optErythro => _urlErythro,
			_optLeuko   => _urlLeuko,
			_ => throw new ArgumentException("Invalid slash command parameter."),
		};

		// Send invite link.
		Log.Debug("  Sending invite link.");
		stopwatch.LogMsecDebug("    Responded in {Time} msec.", false);
		await interaction.RespondMessageAsync(invite);
		Log.Information("  Invite link for \"{Server}\" sent.", server);
	}
}
