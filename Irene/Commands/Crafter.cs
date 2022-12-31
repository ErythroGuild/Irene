namespace Irene.Commands;

using static Modules.Crafter.Types;

using Completers = Modules.Crafter.Completer;
using Responders = Modules.Crafter.Responder;

class Crafter : CommandHandler {
	public const string
		CommandCrafter = "crafter",
		CommandFind    = "find"   ,
		CommandList    = "list"   ,
		CommandSet     = "set"    ,
		CommandRemove  = "remove" ,
		ArgItem       = "item"      ,
		ArgProfession = "profession",
		ArgSelfOnly   = "self-only" ,
		ArgCharacter  = "character" ,
		ArgServer     = "server"    ;

	public override string HelpText =>
		$"""
		{RankIcon(AccessLevel.Guest)}{Mention($"{CommandCrafter} {CommandFind}")} `<{ArgItem}>` finds crafters who can craft an item,
		{RankIcon(AccessLevel.Guest)}{Mention($"{CommandCrafter} {CommandList}")} `[{ArgProfession}] [{ArgSelfOnly}]` lists registered crafters.
		{RankIcon(AccessLevel.Guest)}{Mention($"{CommandCrafter} {CommandSet}")} `<{ArgCharacter}> [{ArgServer}]` registers a crafter,
		{_t}You can only manually refresh your own crafters.
		{_t}(All data gets refreshed every 90 minutes.)
		{RankIcon(AccessLevel.Guest)}{Mention($"{CommandCrafter} {CommandRemove}")} `<{ArgCharacter}> [{ArgServer}]` removes a crafter.
		{_t}If unspecified, `[{ArgServer}]` defaults to Moon Guard.
		""";

	public override CommandTree CreateTree() => new (
		new (
			CommandCrafter,
			"Find guildies to craft things."
		),
		new List<CommandTree.GroupNode>(),
		new List<CommandTree.LeafNode> {
			new (
				AccessLevel.Guest,
				new (
					CommandFind,
					"Lists potential crafters.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> { new (
						ArgItem,
						"The item to craft.",
						ArgType.String,
						required: true,
						autocomplete: true
					) }
				),
				new (
					FindAsync,
					new Dictionary<string, Completer> {
						[ArgItem] = Completers.Items,
					}
				)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandList,
					"Lists all crafters.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							ArgProfession,
							"The profession of the crafters.",
							ArgType.String,
							required: false,
							choices: GetOptionsProfessions()
						),
						new (
							ArgSelfOnly,
							"Whether to only show your crafters.",
							ArgType.Boolean,
							required: false
						),
					}
				),
				new (ListAsync)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandSet,
					"Register a crafter.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							ArgCharacter,
							"The character to register.",
							ArgType.String,
							required: true,
							autocomplete: true
						),
						new (
							ArgServer,
							"The server of the character.",
							ArgType.String,
							required: false,
							autocomplete: true
						),
					}
				),
				new (
					SetAsync,
					new Dictionary<string, Completer> {
						[ArgCharacter] = Completers.Roster,
						[ArgServer] = Completers.Servers,
					}
				)
			),
			new (
				AccessLevel.Guest,
				new (
					CommandRemove,
					"Un-register a crafter.",
					ArgType.SubCommand,
					options: new List<DiscordCommandOption> {
						new (
							ArgCharacter,
							"The crafter to remove.",
							ArgType.String,
							required: true,
							autocomplete: true
						),
						new (
							ArgServer,
							"The server of the crafter.",
							ArgType.String,
							required: false,
							autocomplete: true
						),
					}
				),
				new (
					RemoveAsync,
					new Dictionary<string, Completer> {
						[ArgCharacter] = Completers.Crafters,
						[ArgServer] = Completers.Servers,
					}
				)
			),
		}
	);
	private static List<DiscordCommandOptionEnum> GetOptionsProfessions() {
		List<DiscordCommandOptionEnum> options = new ();
		foreach (string profession in Enum.GetNames<Profession>())
			options.Add(new (profession, profession));
		return options;
	}

	private async Task FindAsync(Interaction interaction, ParsedArgs args) {
		string item = (string)args[ArgItem];
		await Responders.RespondFindAsync(interaction, item);
	}

	private async Task ListAsync(Interaction interaction, ParsedArgs args) {
		Profession? profession =
			args.TryGetValue(ArgProfession, out object? argProfession)
				? Enum.Parse<Profession>((string)argProfession)
				: null;
		bool isSelfOnly =
			args.TryGetValue(ArgSelfOnly, out object? argSelfOnly)
				? (bool)argSelfOnly
				: false;

		await Responders.RespondListAsync(interaction, profession, isSelfOnly);
	}

	private async Task SetAsync(Interaction interaction, ParsedArgs args) {
		string name = (string)args[ArgCharacter];
		string server = args.ContainsKey(ArgServer)
			? (string)args[ArgServer]
			: ServerGuild;

		Character? character = ValidateCharacter(name, server);
		if (character is null) {
			string errorCharacter =
				$"""
				Sorry, `{name}-{server}` isn't a valid character.
				Maybe double-check and try again?
				""";
			await interaction.RegisterAndRespondAsync(errorCharacter, true);
			return;
		}

		await Responders.RespondSetAsync(interaction, character.Value);
	}

	private async Task RemoveAsync(Interaction interaction, ParsedArgs args) {
		string name = (string)args[ArgCharacter];
		string server = args.ContainsKey(ArgServer)
			? (string)args[ArgServer]
			: ServerGuild;

		Character? character = ValidateCharacter(name, server);
		if (character is null) {
			string errorCharacter =
				$"""
				Sorry, `{name}-{server}` isn't a valid character.
				Maybe double-check and try again?
				""";
			await interaction.RegisterAndRespondAsync(errorCharacter, true);
			return;
		}

		await Responders.RespondRemoveAsync(interaction, character.Value);
	}

	// Checks if the character server combination is well-formed, and
	// returns the created `Character` if so. Returns null otherwise.
	private static Character? ValidateCharacter(string name, string server) {
		Character? character = null;
		try {
			character = new (name, server);
		} catch (ArgumentException) { }
		return character;
	}
}
