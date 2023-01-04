namespace Irene.Modules.Crafter;

using static Database;
using static Types;

using ProfessionData = Types.CharacterData.ProfessionData;
using TierSkill = Types.CharacterData.TierSkill;

class Responder {

	// Master table of all active crafter management menus, indexed by
	// the user ID of the owner (users can only manage their own crafters).
	private static ConcurrentDictionary<ulong, SelectorPages> _menus = new ();

	// The duration to 
	private static readonly TimeSpan _durationMenu = TimeSpan.FromMinutes(10);

	// The prefix for profession summaries' text inputs' custom IDs.
	private const string _prefixTextInput = "summary_";

	// Formatting tokens.
	private const string
		_zwsp = "\u200B",
		_hrsp = "\u200A", _thsp = "\u2009",
		_ensp = "\u2002", _emsp = "\u2003",
		_enDash = "\u2013";


	// --------
	// Public interaction methods:
	// --------

	// Returns all registered characters if `profession` is null.
	public static async Task ListAsync(
		Interaction interaction,
		Profession? profession,
		bool isSelfOnly
	) {
		CheckErythroInit();

		// Prepare master lists to compile crafter list from.
		HashSet<Character> crafterPool = new ();
		if (profession is null) {
			foreach (Profession profession_i in Enum.GetValues<Profession>())
				crafterPool.UnionWith(GetCrafters(profession_i));
		} else {
			crafterPool = new (GetCrafters(profession.Value));
		}
		IReadOnlySet<Character>? craftersOwned = isSelfOnly
			? GetCrafters(interaction.User.Id)
			: null;

		// Compile list of crafters matching the request.
		if (isSelfOnly)
			crafterPool.IntersectWith(craftersOwned!);
		List<CharacterData> crafters = new ();
		foreach (Character crafter in crafterPool)
			crafters.Add(GetCrafterData(crafter));

		// Format title appropriately.
		string title = profession is null
			? "Crafter"
			: profession.Value.Title();
		if (crafters.Count != 1)
			title += "s";

		// Handle empty case.
		if (crafters.Count == 0) {
			string commandSet = Dispatcher.Mention(
				Commands.Crafter.CommandCrafter,
				Commands.Crafter.CommandSet
			);
			string responseNone = isSelfOnly
				? $"""
					:desert: Sorry, you don't have any registered {title} yet.
					(You can register crafters with {commandSet}.)
					"""
				: $"""
					:desert: Sorry, there aren't any registered {title} yet.
					If you know one, maybe ask them to register?
					""";
			await interaction.RegisterAndRespondAsync(responseNone, true);
			return;
		}

		// Sort characters.
		crafters.Sort((c1, c2) =>
			string.Compare(c1.Character.Name, c2.Character.Name)
		);

		// Convert list to strings.
		DiscordEmoji emoji = Erythro.Emoji(id_e.qualityAny);
		string heading = $"{emoji}{_ensp}{_hrsp}__**{title}**__{_ensp}{emoji}";
		List<string> lines = new ();
		foreach (CharacterData crafter in crafters) {
			string name = crafter.Character.LocalName();
			string entry =
				$"""
				{_thsp}{crafter.Class.Emoji()}{_ensp}{name}
				{_thsp}{_emsp}{crafter.UserId.MentionUserId()}
				""";
			lines.Add(entry);
		}

		// Respond with list of crafters.
		MessagePromise messagePromise = new ();
		StringPages pages = StringPages.Create(
			interaction,
			messagePromise,
			lines,
			new StringPagesOptions {
				PageSize = 6,
				Header = heading,
			}
		);
		DiscordMessageBuilder response = pages
			.GetContentAsBuilder()
			.WithAllowedMentions(Mentions.None);

		string summary = "Crafter list sent.";
		await interaction.RegisterAndRespondAsync(response, summary, true);

		DiscordMessage message = await interaction.GetResponseAsync();
		messagePromise.SetResult(message);
	}

	public static async Task FindAsync(
		Interaction interaction,
		string item
	) {
		// Send initial loading bar.
		string loading =
			$"""
			Searching database...
			{ProgressBar.Get(2, 6)}
			""";
		await interaction.RegisterAndRespondAsync(loading, true);

		// Handle empty case.
		if (!HasItemCrafter(item)) {
			string responseNone =
				$"""
				:desert: Sorry, I couldn't find any registered crafters for **{item}**.
				If you know anyone with the profession, maybe ask them to register?
				""";
			await interaction.EditResponseAsync(responseNone);
			return;
		}

		ItemData itemData = GetItemData(item);

		// Format header.
		string tier = itemData.ProfessionTier;
		string timestamp = LastUpdated.Timestamp(Util.TimestampStyle.Relative);
		string heading =
			$"""
			__**{item}**__ {_enDash} {tier}
			{_emsp}*refreshed {timestamp}*

			""";

		// Update progress bar.
		await UpdateProgressBarAsync(interaction, "Checking recipes...", 4, 6);

		// Check for recipe ranks (and cache them).
		// This is also needed for the crafter sorting comparer later.
		bool hasRanks = false;
		foreach (Character crafter in itemData.Crafters) {
			long recipe = itemData.GetCrafterRecipeId(crafter);
			if (!IsRecipeRankCached(recipe))
				await CacheRecipeRankAsync(recipe);
			hasRanks |= GetRecipeRankCached(recipe, true) is not null;
		}

		// Update progress bar.
		await UpdateProgressBarAsync(interaction, "Compiling results...", 5, 6);

		// Sort results.
		List<Character> crafters = new (itemData.Crafters);
		crafters.Sort((c1, c2) =>
			CrafterRankComparer(c1, c2, hasRanks, itemData)
		);

		// Compile results.
		List<string> entries = new (); ;
		foreach (Character crafter in crafters) {
			// Character data.
			CharacterData crafterData = GetCrafterData(crafter);
			DiscordEmoji @class = crafterData.Class.Emoji();
			string mention = Util.MentionUserId(crafterData.UserId);

			// Profession data.
			ProfessionData professionData =
				crafterData.GetProfessionData(itemData.Profession);
			TierSkill skill = professionData.GetSkill(tier);
			double skillFraction =
				skill.Skill / (double)skill.SkillMax;
			string bar = ProgressBar.Get(skillFraction, 5);

			// Recipe rank data (cached from earlier).
			long recipe = itemData.GetCrafterRecipeId(crafter);
			int? recipeRank = GetRecipeRankCached(recipe, true);
			string stars = hasRanks
				? $"\n{_emsp}{RenderRankStars(recipeRank)}"
				: "";

			// Summary string.
			string summary = (professionData.Summary == "")
				? ""
				: $"\n> *{professionData.Summary.EscapeDiscordFormatting()}*";

			// Format output.
			string entry =
				$"""
				{@class} **{crafter.LocalName()}** {_enDash} {mention}{stars}
				{bar}{_ensp}{skill}{summary}

				""";
			entries.Add(entry);
		}

		// Respond with results.
		MessagePromise messagePromise = new ();
		StringPages pages = StringPages.Create(
			interaction,
			messagePromise,
			entries,
			new StringPagesOptions {
				PageSize = 3,
				Header = heading,
			}
		);

		DiscordMessageBuilder response = pages
			.GetContentAsBuilder()
			.WithAllowedMentions(Mentions.None);
		DiscordMessage message = await
			interaction.EditResponseAsync(response);

		messagePromise.SetResult(message);

		Log.Information("  Crafter search results sent.");
	}

	// This method checks to ensure the character is valid, and hasn't
	// already been registered to someone else.
	public static async Task SetAsync(
		Interaction interaction,
		Character character
	) {
		CheckErythroInit();
		ulong userId = interaction.User.Id;

		// Check that the character isn't already registered.
		// This also determines if the user is just updating one of their
		// registered crafters.
		bool isUpdate = false;
		if (HasCrafter(character)) {
			if (GetCrafters(userId).Contains(character)) {
				isUpdate = true;
			} else {
				string mentionAdmin = Util.MentionUserId(id_u.admin);
				string errorOverwrite =
					$"""
					:blossom: Sorry, **{character.LocalName()}** is already registered.
					If this character actually belongs to you, contact {mentionAdmin}.
					(The registration can be quickly fixed!)
					""";
				await interaction.RegisterAndRespondAsync(errorOverwrite, true);
				return;
			}
		}

		// Check that the character is valid (according to API results).
		if (!await ApiRequest.CheckIsValidCharacter(character)) {
			string errorNoCharacter =
				$"""
				Sorry, I couldn't find info for **{character.LocalName()}**.
				If the name is correct, Blizzard servers may be out of date.
				:stopwatch: Relog, wait a few minutes, and try again?
				""";
			await interaction.RegisterAndRespondAsync(errorNoCharacter, true);
			return;
		}

		// Notify user that command was received.
		string emojiLoading = Erythro.Emoji(id_e.spinnerDots).ToString();
		string responseLoading = emojiLoading +
			( isUpdate
				? "Updating character..."
				: "Registering new character..."
			);
		//await interaction.RegisterAndRespondAsync(responseLoading, true);
		await interaction.RegisterAndRespondAsync(responseLoading);

		// Initial registration/refresh of the character.
		if (!isUpdate)
			await AddCharacterAsync(userId, character);
		else
			await RefreshCharacterAsync(character);

		// The rest of this method sets up the final response to the
		// interaction. After constructing all the required components,
		// an interactable is constructed and returned.

		// Prepare (and format) list of owned characters.
		List<SelectorPages.EntryData> characterList = new ();
		foreach (Character character_i in GetCrafters(userId)) {
			string name = character_i.LocalName();
			CharacterData data = GetCrafterData(character_i);
			DiscordEmoji emoji = data.Class.Emoji();
			string professionList = DisplayProfessions(data);
			characterList.Add(new (
				new (name, name, new (emoji), professionList),
				character_i
			));
		}
		characterList.Sort((c1, c2) =>
			string.Compare(c1.Entry.Label, c2.Entry.Label)
		);

		// Set up page renderer (with captured variables).
		MessagePromise messagePromise = new ();
		IDiscordMessageBuilder Renderer(object characterRaw, bool isEnabled) =>
			RenderCharacterPage(
				interaction,
				messagePromise,
				(Character)characterRaw,
				isEnabled
			);

		// Set up `SelectorPages` interactable.
		SelectorPages pages = SelectorPages.Create(
			interaction,
			messagePromise,
			characterList,
			character.LocalName(),
			Renderer,
			new SelectorPagesOptions() { Timeout = _durationMenu }
		);

		// Track each user's active `/crafter set` interactable (only
		// allow one active interactable person at a time).
		if (_menus.TryRemove(userId, out SelectorPages? pagesOld))
			await pagesOld.Discard();
		_menus.TryAdd(userId, pages);

		// Activate `SelectorPages` interactable.
		await interaction.EditResponseAsync(pages.GetContentAsBuilder());
		messagePromise.SetResult(await interaction.GetResponseAsync());
	}

	// Refreshes the entire database if `character` is null.
	// This method assumes that permissions have been checked already
	// (e.g. for updating the entire database).
	public static async Task RefreshAsync(
		Interaction interaction,
		Character? character=null
	) {
		CheckErythroInit();

		// Handle entire database rebuild.
		if (character is null) {
			string responseAll =
				$"{Erythro.Emoji(id_e.spinnerDots)} Refreshing item databases...";
			await interaction.RegisterAndRespondAsync(responseAll, true);

			await InitItemDatabaseAsync();

			await interaction.EditResponseAsync(":books: Update complete!");
			return;
		}

		// Handle unregistered character.
		if (!HasCrafter(character.Value)) {
			string commandSet = Dispatcher.Mention(
				Commands.Crafter.CommandCrafter,
				Commands.Crafter.CommandSet
			);
			string responseNotFound =
				$"""
				:desert: Sorry, that character hasn't been registered yet.
				If it's your character, you can register it with {commandSet}!
				If it's someone else's, you can ask them to register it.
				""";
			await interaction.RegisterAndRespondAsync(responseNotFound, true);
			return;
		}
		Character crafter = character.Value;

		// Update specific character.
		string loading =
			$"{Erythro.Emoji(id_e.spinnerDots)} Refreshing data...";
		await interaction.RegisterAndRespondAsync(loading, true);

		await RefreshCharacterAsync(crafter);

		// Update message with fresh data.
		CharacterData data = GetCrafterData(crafter);
		string response =
			$"""
			*Refreshed crafter data.*

			{RenderCharacterData(data)}
			""";
		await interaction.EditResponseAsync(response);
	}

	// This method assumes that character ownership has already been
	// checked (this also includes the character's existence).
	public static async Task RemoveAsync(
		Interaction interaction,
		Character character
	) {
		// Preview crafter data.
		CharacterData data = GetCrafterData(character);
		string summary = RenderCharacterData(data);
		string response = $"*Removing data for:*\n{summary}";
		await interaction.RegisterAndRespondAsync(response, true);

		// Define confirmation callback (remove character).
		Task RemoveCrafter(bool doRemove) =>
			Task.Run(async () => {
				string intro;
				if (doRemove) {
					await RemoveCharacterAsync(character);
					intro = "*Removed data for:*";
				} else {
					intro = "*No data removed. (No changes made.)*";
				}
				string message = $"{intro}\n{summary}";
				await interaction.EditResponseAsync(message);
			});

		// Create and send confirmation.
		string prompt =
			$"Are you sure you want to remove data for **{character.LocalName()}**?";
		Confirm confirm = Confirm.Create(
			interaction,
			RemoveCrafter,
			new ConfirmOptions() {
				Prompt = prompt,
				LabelYes = "Remove data",
			}
		);
		await confirm.Prompt();
	}


	// --------
	// Formatting helper methods:
	// --------

	// Replace the (already responded-to!) interaction with a message
	// consisting of a labeled progress bar.
	private static Task UpdateProgressBarAsync(
		Interaction interaction,
		string label,
		int progress,
		int progressMax
	) {
		string message =
			$"""
			{label}
			{ProgressBar.Get(progress, progressMax)}
			""";
		return interaction.EditResponseAsync(message);
	}

	// Creates a string of 3 full/empty stars, for a recipe's rank.
	private static string RenderRankStars(int? rank) {
		rank ??= 0;
		const string
			starFull  = "\u2605",
			starEmpty = "\u2606";

		StringBuilder output = new ();
		for (int i=0; i<3; i++) {
			string star = (i <= rank-1)
				? starFull
				: starEmpty;
			output.Append(star);
		}

		return output.ToString();
	}

	// Concatenates the character's crafting professions.
	private static string DisplayProfessions(CharacterData data) {
		List<Profession> professions = new (data.Professions);
		professions.Sort();
		return string.Join(", ", professions);
	}

	// Render out a character's saved data (tier skills not included).
	private static string RenderCharacterData(CharacterData data) {
		CheckErythroInit();
		StringBuilder content = new ();

		// Render heading (character name/class).
		string name = data.Character.LocalName();
		string heading =
			$"{_zwsp}{_emsp}**{name}**{_ensp}{data.Class.Emoji()}";
		content.AppendLine(heading);

		// Render professions (in order).
		List<Profession> professions = new (data.Professions);
		professions.Sort();
		DiscordEmoji bullet = Erythro.Emoji(id_e.quality5);
		foreach (Profession profession in professions) {
			ProfessionData data_i = data.GetProfessionData(profession);
			content.AppendLine($"{bullet} __{profession}__");
			if (data_i.Summary != "") {
				string summary =
					data_i.Summary.EscapeDiscordFormatting();
				content.AppendLine($"> *{summary}*");
			}
		}

		return content.ToString();
	}

	// `data` -> `character`
	// pages promise is needed to refresh
	private static IDiscordMessageBuilder RenderCharacterPage(
		Interaction interaction,
		MessagePromise messagePromise,
		Character character,
		bool isEnabled
	) {
		CheckErythroInit();

		// Display notice if no data exists for the character.
		if (!HasCrafter(character)) {
			string responseDeleted =
				$"""
				{_zwsp}{_emsp}~~**{character.LocalName()}**~~
				*The data for this crafter has been removed.*
				""";
			return new DiscordMessageBuilder()
				.WithContent(responseDeleted);
		}

		// Render character info preview.
		CharacterData characterData = GetCrafterData(character);
		string content = RenderCharacterData(characterData);

		// Create refresh button.
		const string iconRefresh = "\u21BB"; // clockwise open circle arrow
		ActionButton buttonRefresh = ActionButton.Create(
			interaction,
			messagePromise,
			i => Task.Run(async () => await Task.WhenAll(
				i.DeferComponentAsync(),
				RefreshCharacterAsync(character)
			)),
			$"button_refresh_{character.LocalName()}",
			iconRefresh,
			null,
			new ActionButtonOptions() {
				IsEnabled = isEnabled,
				Timeout = _durationMenu,
			}
		);

		// Create text inputs for set-summaries button.
		List<DiscordTextInput> textInputs = new ();
		List<Profession> professions = new (characterData.Professions);
		professions.Sort();
		foreach (Profession profession in professions) {
			textInputs.Add(new (
				profession.ToString(),
				$"{_prefixTextInput}{profession}",
				"add a short comment",
				required: false,
				max_length: MaxSummaryLength
			));
		}

		// Create set-summaries button.
		ModalButton buttonSet = ModalButton.Create(
			interaction,
			messagePromise,
			() => Task.Run(() => FetchSummaryPrefills(character)),
			d => Task.Run(() => SetSummaries(character, d)),
			$"button_set_{character.LocalName()}",
			"Update comments",
			null,
			character.LocalName(),
			textInputs,
			new ModalButtonOptions() {
				IsEnabled = isEnabled && professions.Count > 0,
				Timeout = _durationMenu,
				TimeoutModal = _durationMenu,
			}
		);

		// Combine rendered content and buttons.
		DiscordMessageBuilder response =
			new DiscordMessageBuilder()
			.WithContent(content)
			.AddComponents(buttonRefresh.GetButton(), buttonSet.GetButton());
		return response;
	}


	// --------
	// Other internal helper methods:
	// --------

	// Sorts crafters by the following criteria:
	//   - recipe rank, descending (if recipe has ranks)
	//   - tier skill, descending
	//   - default server before other servers
	//   - alphabetically, ascending
	// Note: *Ignores* recipe ranks if they aren't cached! (Does NOT
	// check the data's TTL.)
	private static int CrafterRankComparer(
		Character c1, Character c2,
		bool hasRanks,
		ItemData item
	) {
		// Sort by rank, descending (if available).
		if (hasRanks) {
			long recipe_a = item.GetCrafterRecipeId(c1);
			long recipe_b = item.GetCrafterRecipeId(c2);
			try {
				int rank_a = GetRecipeRankCached(recipe_a, true) ?? 0;
				int rank_b = GetRecipeRankCached(recipe_b, true) ?? 0;
				if (rank_a != rank_b)
					return rank_b - rank_a;
			} catch (InvalidOperationException) { }
		}

		// Sort by tier skill, descending.
		Profession profession = item.Profession;
		string tier = item.ProfessionTier;
		int skill_a = GetCrafterData(c1)
			.GetSkill(profession, tier)
			.Skill;
		int skill_b = GetCrafterData(c2)
			.GetSkill(profession, tier)
			.Skill;
		if (skill_a != skill_b)
			return skill_b - skill_a;

		// Sort default server before other servers.
		bool isDefaultServer_a = (c1.Server == ServerGuild);
		bool isDefaultServer_b = (c2.Server == ServerGuild);
		if (isDefaultServer_a != isDefaultServer_b)
			return isDefaultServer_a ? -1 : 1;

		// Sort by name, alphabetically.
		return string.Compare(c1.Name, c2.Name);
	}

	//
	private static IReadOnlyDictionary<string, string> FetchSummaryPrefills(Character character) {
		Dictionary<string, string> prefills = new ();

		// Handle edge case where character data was removed.
		if (!HasCrafter(character))
			return prefills;

		// Populate prefill table with summaries.
		CharacterData characterData = GetCrafterData(character);
		foreach (Profession profession in characterData.Professions) {
			string summary = characterData.GetSummary(profession);
			prefills.Add(
				$"{_prefixTextInput}{profession}",
				summary
			);
		}

		return prefills;
	}

	//
	private static async Task SetSummaries(
		Character character,
		IReadOnlyDictionary<string, string> data
	) {
		// Parse submitted data.
		HashSet<Task> tasks = new ();
		foreach (string inputId in data.Keys) {
			string summary = data[inputId];

			string professionString =
				inputId.Replace(_prefixTextInput, "");
			Profession profession = Enum.Parse<Profession>(professionString);

			tasks.Add(SetSummary(character, profession, summary));
		}

		// Wait for all data to be written.
		await Task.WhenAll(tasks);

		// Refresh page display.
		ulong ownerId = GetCrafterData(character).UserId;
		await _menus[ownerId].Update();
	}
}
