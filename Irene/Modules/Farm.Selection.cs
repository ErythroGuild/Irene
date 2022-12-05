namespace Irene.Modules;

using System.Timers;

partial class Farm {
	private class Selection {
		// Master table of all Selections to handle, indexed by the message
		// ID of the owning message.
		// This also serves as a way to "hold" fired timers, preventing them
		// from prematurely going out of scope and being destroyed.
		private static readonly ConcurrentDictionary<ulong, Selection> _selections = new ();

		// Static properties / fields.
		// The timeout is allowed to exceed the usual 15 minute interaction
		// limit because the messages are guaranteed to not be ephemeral
		// interaction responses.
		private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(30);
		private const string _idSelect = "farmSelect_pages";
		private const string _placeholder = "Select a page";

		// All events are handled by a single delegate, registered on init.
		// This means there doesn't need to be a large amount of delegates
		// that each event has to filter through until it hits the correct
		// handler.
		static Selection() {
			CheckErythroInit();

			Erythro.Client.ComponentInteractionCreated += (client, e) => {
				_ = Task.Run(async () => {
					ulong id = e.Message.Id;

					// Consume all interactions originating from a registered
					// message, containing this Selection object.
					if (_selections.ContainsKey(id) && e.Id == _idSelect) {
						Selection selection = _selections[id];
						e.Handled = true;

						// Can only update if message was already created.
						if (selection._message is null)
							return;

						// Only respond to interactions created by the owner
						// of the interactable.
						if (e.User != selection._interaction.User)
							return;

						// Acknowledge interaction and update the original
						// message later (inside the callback itself).
						Interaction interaction = Interaction.FromComponent(e);
						await interaction.DeferComponentAsync();

						// Parse the selected Route object and update
						// the message display.
						string routeId = e.Values[0];
						Route? route = null;
						foreach (Route route_i in selection._data.Routes) {
							if (route_i.Id == routeId)
								route = route_i;
						}
						if (route is null)
							return;
						await selection.Update(route);
					}
				});
				return Task.CompletedTask;
			};
		}

		// Instance properties.
		public DiscordSelect Component { get; private set; }
		private readonly Interaction _interaction;
		private DiscordMessage? _message = null;
		private readonly Timer _timer;
		private readonly Material _data;
		private Route _selected;

		// Public factory method constructor.
		// Use this method to instantiate a new interactable.
		public static Selection Create(
			Interaction interaction,
			Task<DiscordMessage> messageTask,
			Material data,
			Route selected
		) {
			Timer timer = Util.CreateTimer(_timeout, false);

			// Construct select component options.
			List<DiscordSelectOption> options = new ();
			foreach (Route route in data.Routes) {
				DiscordSelectOption option = new (
					route.Name,
					route.Id,
					isDefault: route.Id == selected.Id
				);
				options.Add(option);
			}

			// Construct select component.
			DiscordSelect component = new (
				_idSelect,
				"Select a page",
				options
			);

			// Construct partial Selection object.
			Selection selection = new (
				interaction,
				component,
				timer,
				data,
				selected
			);
			messageTask.ContinueWith((messageTask) => {
				DiscordMessage message = messageTask.Result;
				selection._message = message;
				_selections.TryAdd(message.Id, selection);
				selection._timer.Start();
			});
			timer.Elapsed += async (obj, e) => {
				// Run (or schedule to run) cleanup task.
				if (!messageTask.IsCompleted)
					await messageTask.ContinueWith(e => selection.Cleanup());
				else
					await selection.Cleanup();
			};

			return selection;
		}
		private Selection(
			Interaction interaction,
			DiscordSelect component,
			Timer timer,
			Material data,
			Route selected
		) {
			Component = component;
			_interaction = interaction;
			_timer = timer;
			_data = data;
			_selected = selected;
		}

		// Manually time-out the timer (and fire the elapsed handler).
		public async Task Discard() {
			_timer.Stop();
			const double delay = 0.1;
			_timer.Interval = delay; // arbitrarily small interval, must be >0
			_timer.Start();
			await Task.Delay(TimeSpan.FromMilliseconds(delay));
		}
		// Update the selected entries of the select component.
		// Assumes _message has been set; returns immediately if it hasn't.
		public async Task Update(Route selected) {
			CheckErythroInit();
			if (_message is null)
				return;

			// Cancel operation if interactable has been disabled.
			if (!_selections.ContainsKey(_message.Id))
				return;

			// Re-fetch message.
			_message = await Util.RefetchMessage(Erythro.Client, _message);

			// Update message display.
			await UpdateMessageDisplayAsync(selected);
		}

		// Cleanup task to dispose of all resources. Also removes self
		// from the `Farm` global `Farm.Selection` table.
		// Assumes _message has been set; returns immediately if it hasn't.
		private async Task Cleanup() {
			CheckErythroInit();
			if (_message is null)
				return;

			// Remove held references.
			_selections.TryRemove(_message.Id, out _);

			// Remove self from global table.
			Farm._selections.TryRemove(_message.Id, out _);

			// Re-fetch message.
			_message = await Util.RefetchMessage(Erythro.Client, _message);

			// Rebuild message with select component disabled.
			await UpdateMessageDisplayAsync(_selected, false);

			Log.Debug("Cleaned up Farm.Selection interactable.");
			Log.Debug("  Channel ID: {ChannelId}", _message.ChannelId);
			Log.Debug("  Message ID: {MessageId}", _message.Id);
		}

		// Update the message display holding this Selection.
		// Assumes _message has been set; returns immediately if it hasn't.
		private async Task UpdateMessageDisplayAsync(Route selected, bool isEnabled=true) {
			if (_message is null)
				return;

			// Update internal state.
			_selected = selected;

			// Create a list of options with updated "selected" state.
			List<DiscordSelectOption> options = new ();
			foreach (DiscordSelectOption option in Component.Options) {
				// Construct a new option, copied from the original, but
				// with the appropriate "selected" state.
				DiscordSelectOption optionUpdated = new (
					option.Label,
					option.Value,
					isDefault: option.Value == selected.Id
				);
				options.Add(optionUpdated);
			}

			// Construct new select component with the updated options.
			Component = new (
				_idSelect,
				_placeholder,
				options,
				!isEnabled
			);

			// Update the message display.
			DiscordMessageBuilder message = GetMessage(_data, selected, this);
			_message = await _message.ModifyAsync(message);
			// Directly editing the message only works because the original
			// message was not an ephemeral response.
		}
	}
}
