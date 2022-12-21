namespace Irene.Interactables;

// `ModalButtonOptions` can be individually set and passed to the static
// `ModalButton` factory constructor. Any unspecified options default
// to the specified values.
class ModalButtonOptions : ActionButtonOptions {
	public static TimeSpan DefaultTimeoutModal => ModalOptions.DefaultTimeout;

	// The duration each `Modal` lasts before being discarded (and no
	// more responses accepted).
	public TimeSpan TimeoutModal { get; init; } = DefaultTimeoutModal;
}

class ModalButton : ActionButton {
	// The `TextInitializer` delegate is called and needs to provide
	// a dictionary of all pre-filled values for the modal's textboxes.
	// The dictionary should be indexed on the custom IDs of each textbox.
	public delegate Task<IReadOnlyDictionary<string, string>> TextInitializer();
	// The `CallbackModal` delegate is called when the modal is submitted,
	// and provides the values in the modal, indexed on their custom IDs.
	public delegate Task CallbackModal(IReadOnlyDictionary<string, string> values);


	// --------
	// Instance properties and fields:
	// --------

	// Public properties.
	public string ModalId => $"modal_{CustomId}";

	// Private fields.
	private readonly TextInitializer _initializer;
	private readonly CallbackModal _callbackModal;
	private readonly TimeSpan _timeoutModal;
	private readonly string _title;
	private readonly IReadOnlyList<DiscordTextInput> _textInputs;


	// --------
	// Factory method and constructor:
	// --------

	// The interactable is registered to the table of `ModalButton`s
	// (and the auto-discard timer starts running) only when the message
	// promise is fulfilled.
	// At least one of `label` or `emoji` must be non-null.
	public static ModalButton Create(
		Interaction interaction,
		MessagePromise promise,
		TextInitializer initializer,
		CallbackModal callback,
		string customId,
		string? label,
		DiscordComponentEmoji? emoji,
		string title,
		IReadOnlyList<DiscordTextInput> textInputs,
		ModalButtonOptions? options=null
	) {
		options ??= new ();

		// Construct partial (uninitialized) object.
		ModalButton button = new (
			interaction,
			initializer,
			callback,
			customId,
			label,
			emoji,
			title,
			textInputs,
			options
		);

		// Set up registration and auto-discard.
		button.FinalizeInstance(promise);

		return button;
	}

	// Since the protected constructor only partially constructs the
	// object, it should never be called directly. Always use the public
	// factory method instead.
	protected ModalButton(
		Interaction interaction,
		TextInitializer initializer,
		CallbackModal callback,
		string customId,
		string? label,
		DiscordComponentEmoji? emoji,
		string title,
		IReadOnlyList<DiscordTextInput> textInputs,
		ModalButtonOptions options
	) : base (
		interaction,
		null!,
		customId,
		label,
		emoji,
		options
	) {
		_initializer = initializer;
		_callbackModal = callback;
		_timeoutModal = options.TimeoutModal;
		_title = title;
		_textInputs = textInputs;

		_callback = SendModal;
	}


	// --------
	// Private helper methods:
	// --------

	// An `ActionButton.Callback` delegate that activates the associated
	// modal when the button is pressed.
	private async Task SendModal(Interaction interaction) {
		Modal modal = await GetModal(interaction);
		await modal.Send();
	}

	// Returns a newly-instantiated `Modal`, with newly-fetched values
	// prefilled for all text inputs.
	private async Task<Modal> GetModal(Interaction interaction) {
		// Update pre-filled values of all text inputs.
		IReadOnlyDictionary<string, string> values =
			await _initializer.Invoke();
		foreach (DiscordTextInput textInput in _textInputs)
			textInput.Value = values[textInput.CustomId];

		// Create and return modal.
		Modal modal = Modal.Create(
			interaction,
			(d, i) => {
				i.DeferComponentAsync();
				return _callbackModal.Invoke(d);
			},
			ModalId,
			_title,
			_textInputs,
			new ModalOptions() { Timeout = _timeoutModal }
		);
		return modal;
	}
}
