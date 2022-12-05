namespace Irene.Exceptions;

// Base exception class for Irene's custom exceptions.
abstract class IreneException : Exception {
	public abstract void Log();
	public abstract string ResponseMessage { get; }
}

// Thrown when the program reaches a logically impossible state.
class ImpossibleException : IreneException {
	public override void Log() =>
		Serilog.Log.Error("Caught ImpossibleException: {Exception}", this);
	public override string ResponseMessage =>
		$"""
		:dizzy_face: Sorry, I messed up some calculations.
		There is nothing wrong with your command! This is just a bug. :bug:
		Maybe try the same command again?

		*If that still doesn't work, tell {Util.MentionUserId(id_u.admin)}, and he'll sort it out.* :hammer:
		""";
}

// Thrown when trying to access null `GuildData` (`Erythro`).
// This should never happen, since initialization order is only supposed
// to initialize dependent classes after `GuildData` has already been
// populated.
class UninitializedException : IreneException {
	public override void Log() =>
		Serilog.Log.Error("Caught UninitializedException: {Exception}", this);
	public override string ResponseMessage =>
		$"""
		:face_with_spiral_eyes: Sorry, your command was not processed--
		I'm still starting up, but should be done soon. Try again in a minute!
				
		*If this keeps happening, tell {Util.MentionUserId(id_u.admin)}, and he'll sort it out.* :hammer:
		""";
}

// Thrown when an enum value has been passed, where logically all values
// have been handled but the compiler still warns about unhandled values.
// This happens because C# does not have closed enums, so it is possible
// for non-supported values to be cast to enums.
class UnclosedEnumException : IreneException {
	public override void Log() {
		Serilog.Log.Error("Caught UnclosedEnumException: {Exception}", this);
		Serilog.Log.Debug("  Enum type: {Type}", EnumName);
		Serilog.Log.Debug("  Unrecognized value: {Value}", EnumValue);
	}
	public override string ResponseMessage =>
		$"""
		:face_with_spiral_eyes: Sorry, I confused myself with my calculations.
		Your command is fine, so just try again in a minute!

		*If this keeps happening, tell {Util.MentionUserId(id_u.admin)}, and he'll sort it out.* :hammer:
		:notepad_spiral: You can also give him these codes: `{EnumName}`, `{EnumValue}`
		""";

	public string EnumName { get; }
	public string EnumValue { get; }

	// `type` should be `typeof(EnumType)`.
	public UnclosedEnumException(Type type, Enum value) {
		EnumName = type.Name;
		EnumValue = value.ToString();
	}
}

// Thrown if the logic of the provided args doesn't match the documented
// constraints that Discord provides. E.g., an enumerated string option
// somehow returned a value not in the list.
class ImpossibleArgException : IreneException {
	public override void Log() {
		Serilog.Log.Error("Caught ImpossibleArgException: {Exception}", this);
		Serilog.Log.Debug("  Arg name: {Name}", ArgName);
		Serilog.Log.Debug("  Impossible value: {Value}", ArgValue);
	}
	public override string ResponseMessage =>
		$"""
		:face_with_raised_eyebrow: It looks like Discord glitched when sending the command.
		This is probably a bug on their end; maybe just try again?

		*If this keeps happening, tell {Util.MentionUserId(id_u.admin)}. He'll look into it.* :bug:
		:notepad_spiral: You can also give him these codes: `{ArgName}`, `{ArgValue}`
		""";

	public string ArgName { get; }
	public string ArgValue { get; }

	public ImpossibleArgException(string argName, string argValue) {
		ArgName = argName;
		ArgValue = argValue;
	}
}

// Thrown when attempting to handle a command which isn't in the handler
// table.
class UnknownCommandException : IreneException {
	public override void Log() {
		Serilog.Log.Error("Caught UnknownCommandException: {Exception}", this);
		Serilog.Log.Debug("  Command name: {Name}", Command);
	}
	public override string ResponseMessage =>
		$"""
		:anguished: Sorry, I didn't set up that command properly.
		Try the same command again in a minute?

		*If this keeps happening, tell {Util.MentionUserId(id_u.admin)}, and he'll sort it out.* :hammer:
		:notepad_spiral: You can also give him the command: `{Command}`
		""";

	public string Command { get; }
	public UnknownCommandException(string command) {
		Command = command;
	}
}
