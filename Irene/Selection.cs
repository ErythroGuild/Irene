using System;
using System.Collections.Generic;
using System.Timers;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Emzi0767.Utilities;

using static Irene.Program;

namespace Irene {
	using DropdownLambda = AsyncEventHandler<DiscordClient, ComponentInteractionCreateEventArgs>;

	class Selection<T> where T : Enum {
		public record Entry {
			public string label = "";
			public string id = "";
			public DiscordComponentEmoji? emoji = null;
			public string? description = null;
		}

		// protected (overrideable) properties
		protected virtual TimeSpan timeout
			{ get { return TimeSpan.FromMinutes(10); } }

		// private static values
		static readonly List<Selection<T>> handlers = new ();
		const string id = "select";

		// public properties/fields
		public readonly Dictionary<T, Entry> options;
		public readonly string placeholder;
		public readonly bool is_multiple;
		public readonly Action<List<T>, DiscordUser> action;
		public readonly DiscordUser action_user;
		public readonly DiscordUser author;
		public DiscordMessage? msg; // may not be set immediately

		// protected properties/fields
		protected List<T> selected = new ();
		protected readonly Timer timer;
		protected readonly DropdownLambda handler;

		public Selection(
			Dictionary<T, Entry> options,
			Action<List<T>, DiscordUser> action,
			DiscordUser author,
			string placeholder,
			bool is_multiple ) :
			this(options, action, author, author, placeholder, is_multiple) { }
		public Selection(
			Dictionary<T, Entry> options,
			Action<List<T>, DiscordUser> action,
			DiscordUser action_user,
			DiscordUser author,
			string placeholder,
			bool is_multiple ) {
			// Initialize members.
			this.options = options;
			this.placeholder = placeholder;
			this.is_multiple = is_multiple;
			this.action = action;
			this.action_user = action_user;
			this.author = author;

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

				// Update selection state and invoke delegate.
				selected = new ();
				List<string> selected_ids = new (e.Values);
				foreach (T entry in this.options.Keys) {
					string id = this.options[entry].id;
					if (selected_ids.Contains(id)) {
						selected.Add(entry);
					}
				}
				action(selected, action_user);

				// Respond to interaction event.
				await e.Interaction.CreateResponseAsync(
					InteractionResponseType.UpdateMessage,
					new DiscordInteractionResponseBuilder()
					.WithContent(e.Message.Content)
					.AddComponents(get(selected))
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
						.WithContent(msg.Content)
						.AddComponents(get(selected ?? new List<T>(), false))
					);
				}
				handlers.Remove(this);
			};

			// Attach handler to client and start deactivation timer.
			handlers.Add(this);
			irene.ComponentInteractionCreated += handler;
			timer.Start();
		}

		// Returns a message component with the specified selected
		// options.
		public DiscordSelectComponent get(List<T> selected, bool is_enabled = true) {
			// Reset the "selected" options member.
			this.selected = selected;

			// Construct list of options in the correct format.
			List<DiscordSelectComponentOption> options = new ();
			foreach(T option in this.options.Keys) {
				Entry entry = this.options[option];
				DiscordSelectComponentOption option_obj = new (
					entry.label,
					entry.id,
					entry.description,
					selected.Contains(option),
					entry.emoji
				);
				options.Add(option_obj);
			}

			// Return the correctly formatted message component.
			return new DiscordSelectComponent(
				id,
				placeholder,
				options,
				!is_enabled,
				(is_multiple ? 0 : 1),
				(is_multiple ? options.Count : 1)
			);
		}
	}
}
