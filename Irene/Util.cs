using System.Collections.Generic;

namespace Irene {
	static class Util {
		static Dictionary<string, string> escape_codes = new () {
			{ @"\n"    , "\n"     },
			{ @":bbul:", "\u2022" },
			{ @":wbul:", "\u25E6" },
			{ @":emsp:", "\u2003" },
			{ @":ensp:", "\u2022" },
			{ @":nbsp:", "\u00A0" },
			{ @":+-:"  , "\u00B1" },
		};

		// Extension methods for converting discord messages to/from
		// single-line easily parseable text.
		public static string escape(this string str) {
			string text = str;
			foreach (string escape_code in escape_codes.Keys) {
				string codepoint = escape_codes[escape_code];
				text = text.Replace(codepoint, escape_code);
			}
			return text;
		}
		public static string unescape(this string str) {
			string text = str;
			foreach (string escape_code in escape_codes.Keys) {
				string codepoint = escape_codes[escape_code];
				text = text.Replace(escape_code, codepoint);
			}
			return text;
		}
	}
}
