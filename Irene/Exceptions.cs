namespace Irene.Exceptions;

// Thrown when the program reaches a logically impossible state.
class ImpossibleException : Exception { }

// Thrown when trying to access null `GuildData` (`Erythro`).
// This should never happen, since initialization order is only supposed
// to initialize dependent classes after `GuildData` has already been
// populated.
class UninitializedException : Exception { }

// Thrown when an enum value has been passed, where logically all values
// have been handled but the compiler still warns about unhandled values.
// This happens because C# does not have closed enums, so it is possible
// for non-supported values to be cast to enums.
class UnclosedEnumException : Exception {
	public string EnumName { get; }
	public string EnumValue { get; }

	// `type` should be `typeof(EnumType)`.
	public UnclosedEnumException(Type type, Enum value) {
		EnumName = type.Name;
		EnumValue = value.ToString();
	}
}

// Thrown when attempting to handle a command which isn't in the handler
// table.
class UnknownCommandException : Exception {
	public string Command { get; }
	public UnknownCommandException(string command) {
		Command = command;
	}
}
