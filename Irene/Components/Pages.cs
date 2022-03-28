using System.Timers;

using Emzi0767.Utilities;

namespace Irene.Components;

using ButtonLambda = AsyncEventHandler<DiscordClient, ComponentInteractionCreateEventArgs>;

class Pages {
	// protected (overrideable) properties
	protected virtual int list_page_size
		{ get { return 8; } }
	protected virtual TimeSpan timeout
		{ get { return TimeSpan.FromMinutes(10); } }

	// private static values
	static readonly List<Pages> handlers = new ();
	const string
		id_button_prev = "list_prev",
		id_button_next = "list_next",
		id_button_page = "list_page";
	const string
		label_prev = "\u25B2",
		label_next = "\u25BC";

	// public properties/fields
	public int page { get; protected set; }
	public readonly int page_count;
	public readonly List<string> list;
	public readonly DiscordUser author;
	public DiscordMessage? msg;	// may not be set immediately

	// protected properties/fields
	protected readonly Timer timer;
	protected readonly ButtonLambda handler;

	public Pages(List<string> list, DiscordUser author) {
		// Initialize members.
		this.author = author;
		this.list = list;
		page = 0;

		double page_count_d = (double)list.Count / list_page_size;
		page_count = (int)Math.Round(Math.Ceiling(page_count_d));

		timer = new Timer(timeout.TotalMilliseconds) {
			AutoReset = false,
		};

		handler = async (irene, e) => {
			// Ignore triggers from the wrong message.
			if (e.Message != msg) {
				return;
			}

			// Ignore people who aren't the original user.
			if (e.User != author) {
				await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
				return;
			}

			// Update page buttons.
			switch (e.Id) {
			case id_button_prev:
				page--;
				break;
			case id_button_next:
				page++;
				break;
			}
			page = Math.Max(page, 0);
			page = Math.Min(page, page_count);

			// Update page content.
			await e.Interaction.CreateResponseAsync(
				InteractionResponseType.UpdateMessage,
				new DiscordInteractionResponseBuilder()
				.WithContent(paginate(page, list_page_size, this.list))
				.AddComponents(buttons(page, page_count))
			);

			// Mark event as handled.
			e.Handled = true;

			// Refresh deactivation timer.
			timer.Stop();
			timer.Start();
		};

		// Configure timeout event listener.
		timer.Elapsed += async (s, e) => {
			irene.ComponentInteractionCreated -= handler;
			if (msg is not null) {
				await msg.ModifyAsync(
					new DiscordMessageBuilder()
					.WithContent(paginate(page, list_page_size, this.list))
					.AddComponents(buttons(page, page_count, false))
				);
			}
			handlers.Remove(this);
		};

		// Attach handler to client and start deactivation timer.
		handlers.Add(this);
		irene.ComponentInteractionCreated += handler;
		timer.Start();
	}

	// Returns the first page of content (used for initial response).
	public DiscordMessageBuilder first_page() {
		DiscordMessageBuilder msg =
			new DiscordMessageBuilder()
			.WithContent(paginate(0, list_page_size, list))
			.AddComponents(buttons(page, page_count));
		return msg;
	}

	// Return the paginated page content.
	// Assumes all arguments are within bounds.
	static string paginate(int page, int page_size, List<string> list) {
		StringWriter text = new ();

		int i_start = page * page_size;
		for (int i = i_start; i < i_start + page_size && i < list.Count; i++) {
			text.WriteLine(list[i]);
		}

		return text.ToString();
	}

	// Returns a row of buttons for paginating the data.
	static DiscordComponent[] buttons(
		int page,
		int total,
		bool is_enabled = true ) {
		return new DiscordComponent[] {
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				id_button_prev,
				label_prev,
				(page + 1 == 1) || !is_enabled
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				id_button_page,
				$"{page + 1} / {total}",
				!is_enabled
			),
			new DiscordButtonComponent(
				ButtonStyle.Secondary,
				id_button_next,
				label_next,
				(page + 1 == total) || !is_enabled
			),
		};
	}
}
