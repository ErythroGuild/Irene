namespace Irene.Modules;

class Secret {
	private record class Stage {
		public int Id { get; }
		public IReadOnlySet<string> Passphrases { get; }
		public string Data { get; }
		public IReadOnlySet<int> Prerequisites { get; }

		// The private constructor requires already parsed data.
		// Use the public static factory method instead.
		private Stage(
			int id,
			List<string> passphrases,
			string data,
			List<int> prerequisites
		) {
			Id = id;
			Passphrases = new HashSet<string>(passphrases);
			Data = data;
			Prerequisites = new HashSet<int>(prerequisites);
		}

		// Standard format for a Stage string:
		// #<-#+#+#:::xxxxx||xxxxx:::xxxxx
		public static Stage FromString(string line) {
			string[] split;
			
			split = line.Split(":::", 3);
			List<string> passphrases = ParsePassphrases(split[1]);
			string data = split[2];

			split = split[0].Split("<-", 2);
			int id = int.Parse(split[0]);
			List<int> prerequisites = ParsePrerequisites(split[1]);

			return new (id, passphrases, data, prerequisites);
		}

		// Helper methods for the Stage parser.
		private static List<string> ParsePassphrases(string input) {
			string[] tokens = input.Split("||");

			List<string> passphrases = new ();
			foreach (string token in tokens) {
				if (token != "")
					passphrases.Add(token);
			}

			return passphrases;
		}
		private static List<int> ParsePrerequisites(string input) {
			string[] tokens = input.Split("+");

			List<int> prerequisites = new ();
			foreach (string token in tokens) {
				if (token != "")
					prerequisites.Add(int.Parse(token));
			}

			return prerequisites;
		}
	}
	private record class MemberData {
		public int Index { get; }
		public ulong Id { get; }
		public IReadOnlySet<int> Progress { get; }

		// The private constructor requires already parsed data.
		// Use the public static fetching method instead.
		private MemberData(int index, ulong id, List<int> progress) {
			Index = index;
			Id = id;
			Progress = new HashSet<int>(progress);
		}

		public static async Task<MemberData> ReadAsync(ulong id) {
			List<string> lines = await _queueProgress.Run(
				new Task<Task<List<string>>>(async () => {
					return new List<string>(await File.ReadAllLinesAsync(_pathProgress));
				})
			);

			// format:
			// #########:#,#,#,#
			int? index = null;
			List<int> progress = new ();
			for (int i=0; i<lines.Count; i++) {
				string[] split = lines[i].Split(":");
				if (split[0] == id.ToString()) {
					index = i;
					progress = ParseProgress(split[1]);
					break;
				}
			}

			// create user data entry if it doesn't exist
			index ??= (await WriteProgressAsync(id, new HashSet<int>())) - 1; // 0-indexed!

			return new (index.Value, id, progress);
		}

		// helper methods for parsing input
		private static List<int> ParseProgress(string input) {
			string[] tokens = input.Split(",");

			List<int> progress = new ();
			foreach (string token in tokens) {
				if (token != "")
					progress.Add(int.Parse(token));
			}

			return progress;
		}

		// this is a static method because writing to the file always reads
		// and modifies the entire file, so it corresponds better to the
		// underlying operation to make it static
		// returns count of lines written
		public static async Task<int> WriteProgressAsync(ulong id, IReadOnlySet<int> progress) {
			List<string> lines = await _queueProgress.Run(
				new Task<Task<List<string>>>(async () => {
					return new List<string>(await File.ReadAllLinesAsync(_pathProgress));
				})
			);

			bool didUpdate = false;
			for (int i=0; i<lines.Count; i++) {
				if (lines[i].StartsWith($"{id}:")) {
					didUpdate = true;
					lines[i] = $"{id}:{string.Join(",", progress)}";
					break;
				}
			}

			if (!didUpdate)
				lines.Add($"{id}:{string.Join(",", progress)}");


			await _queueProgress.Run(new Task<Task>(async () => {
				await File.WriteAllLinesAsync(_pathProgress, lines);
			}));

			return lines.Count;
		}
	}

	private static readonly IReadOnlyList<Stage> _stages;
	private static readonly TaskQueue
		_queueResponses = new (),
		_queueProgress  = new ();
	private const string
		_pathResponses = @"data/secret-responses.txt",
		_pathProgress  = @"data/secret-progress.txt";
		//_pathPersonalNotes = @"data/secret/";
	private const string
		_responseNotReady = "What do we have here? The time is not yet ripe...",
		_responseIncorrect = "That is not what I was looking for...";
	private const ulong
		_idAdmin = 165557736287764483;

	static Secret() {
		Util.CreateIfMissing(_pathResponses);
		Util.CreateIfMissing(_pathProgress);

		// Cache all the responses on startup--then only the file with
		// per-user progress needs to be read when a query is received.
		List<Stage> stages = new ();
		List<string> lines = new (File.ReadAllLines(_pathResponses));
		foreach (string line in lines) {
			if (line == "")
				continue;
			stages.Add(Stage.FromString(line.Trim()));
		}
		_stages = stages;

		// Register handlers for admin "hint" commands.
		Client.MessageCreated += async (irene, e) => {
			if (e.Author.Id == _idAdmin && e.Channel.IsPrivate) {
				string command = e.Message.Content;
				const string prefix = "kirasath: ";
				if (command.StartsWith(prefix)) {
					e.Handled = true;
					string message = command.Replace(prefix, null);
					DiscordChannel kirasath = await
						irene.GetChannelAsync(id_ch.kirasath);
					await kirasath.SendMessageAsync(message);
				}
			}
		};
	}

	public static async Task<string> Respond(
		DateTimeOffset dateTime,
		string attempt,
		DiscordMember member,
		GuildData erythro
	) {
		// Check date once we have more than one secret hunt
		if (dateTime < new DateTimeOffset(2022, 10, 6, 0, 0, 0, new (0)))
			return _responseNotReady;

		// Normalize input.
		attempt = attempt.Trim().ToLower();

		// Check input against stored keys
		Stage? stage = null;
		foreach (Stage stage_i in _stages) {
			if (stage_i.Passphrases.Contains(attempt)) {
				stage = stage_i;
				break;
			}
		}

		// incorrect response, otherwise continue
		if (stage is null)
			return _responseIncorrect;

		// Check all prerequisites
		MemberData memberData = await MemberData.ReadAsync(member.Id);
		if (!memberData.Progress.IsSupersetOf(stage.Prerequisites))
			return _responseIncorrect;

		// Now we must have correct response + met prerequisites
		// so we can save the progress
		HashSet<int> progress = new (memberData.Progress);
		progress.Add(stage.Id);
		// this can happen in the background
		_ = MemberData.WriteProgressAsync(member.Id, progress);

		// handle special stages
		string response;
		switch (stage.Id) {
		case 7:
		case 8:
		case 11:
		case 13:
			List<string> options = new (stage.Data.Split(" ;; "));
			response = options[memberData.Index % options.Count];
			break;
		default:
			response = stage.Data.Unescape();
			break;
		}

		if (stage.Id == 7)
			_ = member.GrantRoleAsync(erythro.Role(id_r.karkun));

		return response;
	}
}
