namespace Irene.Utils;

static partial class Util {
	public static bool ImplementsInterface(this Type type, Type @interface) =>
		new List<Type>(type.GetInterfaces()).Contains(@interface);
}
