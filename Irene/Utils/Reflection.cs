namespace Irene.Utils;

using System.Diagnostics;

static partial class Util {
	// Whether or not the current program is a debug build.
	public static bool IsDebug { get {
		bool isDebug = false;
		// `CheckDebug()` will be skipped over when not in debug mode.
		CheckDebug(ref isDebug);
		return isDebug;
	} }
	// Helper method to calculate the `IsDebug` property.
	[Conditional("DEBUG")]
	private static void CheckDebug(ref bool isDebug) => isDebug = true;

	// Convenience method for checking interfaces during reflection.
	public static bool ImplementsInterface(this Type type, Type @interface) =>
		new List<Type>(type.GetInterfaces()).Contains(@interface);
}
