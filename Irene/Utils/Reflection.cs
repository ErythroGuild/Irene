namespace Irene.Utils;

using System.Diagnostics;
using System.Reflection;

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

	// Runs all static constructors, for any type in the specified set
	// that has them.
	public static void RunAllStaticConstructors(IReadOnlySet<Type> types) {
		foreach (Type type in types) {
			// See Microsoft's documentation for `GetConstructors()`.
			// These flags are the exact ones needed to fetch static
			// constructors.
			List<ConstructorInfo> constructors = new (
					type.GetConstructors(
						BindingFlags.Public |
						BindingFlags.Static |
						BindingFlags.NonPublic |
						BindingFlags.Instance
					)
				);

			// Note: calling the constructor directly risks calling
			// the static constructor more than once. Instead, check
			// for the presence of a static constructor, and if one
			// exists, we use the following method to call it (if it
			// hasn't been called yet), and indicate to the runtime
			// that it _has_ been called.
			foreach (ConstructorInfo constructor in constructors) {
				if (constructor.IsStatic) {
					System.Runtime.CompilerServices
						.RuntimeHelpers
						.RunClassConstructor(type.TypeHandle);
					break;
				}
			}
		}
	}
}
