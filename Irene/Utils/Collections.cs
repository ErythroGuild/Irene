using System.Collections.Generic;

namespace Irene.Utils;

static class Collections {
	// Returns the functional inverse of a given Dictionary.
	public static Dictionary<T2, T1> Inverse<T1, T2>(this Dictionary<T1, T2> dict)
		where T1 : notnull
		where T2 : notnull
	{
		Dictionary<T2, T1> dict_inverse = new ();
		foreach (T1 key in dict.Keys)
			dict_inverse.Add(dict[key], key);
		return dict_inverse;
	}
}
