namespace Irene.Utils;

static partial class Util {
	// Returns the functional inverse of a given Dictionary.
	public static Dictionary<T2, T1> Invert<T1, T2>(Dictionary<T1, T2> dict)
		where T1 : notnull
		where T2 : notnull
	{
		Dictionary<T2, T1> dict_inverse = new ();
		foreach (T1 key in dict.Keys)
			dict_inverse.Add(dict[key], key);
		return dict_inverse;
	}
	public static ConcurrentDictionary<T2, T1> Invert<T1, T2>(ConcurrentDictionary<T1, T2> dict)
		where T1 : notnull
		where T2 : notnull
	{
		ConcurrentDictionary<T2, T1> dict_inverse = new ();
		foreach (T1 key in dict.Keys)
			dict_inverse.TryAdd(dict[key], key);
		return dict_inverse;
	}
}
