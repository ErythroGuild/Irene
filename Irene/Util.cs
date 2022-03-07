using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using static Irene.Program;

namespace Irene;

static class Util {
	static readonly Dictionary<string, string> escape_codes = new () {
		{ @"\n"    , "\n"     },
		{ @":bbul:", "\u2022" },
		{ @":wbul:", "\u25E6" },
		{ @":emsp:", "\u2003" },
		{ @":ensp:", "\u2022" },
		{ @":nbsp:", "\u00A0" },
		{ @":+-:"  , "\u00B1" },
	};
	static readonly Dictionary<Permissions, string> perms_descriptions = new () {
		// General permissions
		{ Permissions.AccessChannels, "View channels"   },
		{ Permissions.ManageChannels, "Manage channels" },
		{ Permissions.ManageRoles   , "Manage roles"    },
		{ Permissions.ManageEmojis  , "Manage emojis & stickers" },
		{ Permissions.ViewAuditLog  , "View audit logs" },
		{ Permissions.ManageWebhooks, "Manage webhooks" },
		{ Permissions.ManageGuild   , "Manage server"   },

		// Membership permissions
		{ Permissions.CreateInstantInvite, "Create invites" },
		{ Permissions.ChangeNickname , "Change own nickname" },
		{ Permissions.ManageNicknames, "Manage nicknames"    },
		{ Permissions.KickMembers    , "Kick members"        },
		{ Permissions.BanMembers     , "Ban members"         },

		// Text channel permissions
		{ Permissions.SendMessages       , "Send messages"         },
		{ Permissions.UsePublicThreads   , "Use public threads"    },
		{ Permissions.UsePrivateThreads  , "Use private threads"   },
		{ Permissions.EmbedLinks         , "Embed links"           },
		{ Permissions.AttachFiles        , "Attach files"          },
		{ Permissions.AddReactions       , "Add reactions"         },
		{ Permissions.UseExternalEmojis  , "Use external emojis"   },
		{ Permissions.UseExternalStickers, "Use external stickers" },
		{ Permissions.MentionEveryone    , "Mention all roles"     },
		{ Permissions.ManageMessages     , "Manage messages"       },
		{ Permissions.ManageThreads      , "Manage threads"        },
		{ Permissions.ReadMessageHistory , "Read message history"  },
		{ Permissions.SendTtsMessages    , "Send TTS messages"     },
		{ Permissions.UseSlashCommands   , "Use slash commands"    },

		// Voice channel permissions
		{ Permissions.UseVoice         , "Connect to voice chat" },
		{ Permissions.Speak            , "Speak"                 },
		{ Permissions.Stream           , "Stream video"          },
		{ Permissions.UseVoiceDetection, "Use voice activity"    },
		{ Permissions.PrioritySpeaker  , "Priority speaker"      },
		{ Permissions.MuteMembers      , "Mute members"          },
		{ Permissions.DeafenMembers    , "Deafen members"        },
		{ Permissions.MoveMembers      , "Move members"          },

		// Stage channel permissions
		{ Permissions.RequestToSpeak, "Request to speak" },

		// Special permissions
		{ Permissions.Administrator, "Administrator" },
		{ Permissions.All , "All"  },
		{ Permissions.None, "None" },
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

	// Returns a discord formatted timestamp from a given DateTimeOffset.
	// Valid format strings are currently found through trial-and-error.
	public static string timestamp(this DateTimeOffset time, string format) {
		return $"<t:{time.ToUnixTimeSeconds()}:{format}>";
	}

	// Returns the date of the next weekday (at 0:00), using local time.
	// Returns the same day if the day of the week is the same.
	// (This means it can return a time in the past.)
	public static DateTimeOffset next_weekday(this DateTimeOffset time, DayOfWeek day) {
		DateTime date = time.LocalDateTime;
		int days_added = (int) day - (int) date.DayOfWeek;
		days_added = (days_added + 7) % 7;	// ensure result falls in [0,6]
		date = (date - date.TimeOfDay).AddDays(days_added);
		return new DateTimeOffset(date);
	}

	// Directly returns the TimeOnly from a TimeSpan of a DateTimeOffset.
	public static TimeOnly TimeOnly(this DateTimeOffset dateTime) {
		return System.TimeOnly.FromTimeSpan(dateTime.TimeOfDay);
	}

	// Create a blank file at the given path, if it doesn't exist.
	// Returns true if file was created, false otherwise.
	public static bool ensure_file_exists(string path, ref object @lock) {
		bool did_create = false;
		lock (@lock) {
			if (!File.Exists(path)) {
				File.Create(path).Close();
				did_create = true;
			}
		}
		return did_create;
	}

	// Returns the functional inverse of a given Dictionary.
	public static Dictionary<T2, T1> inverse<T1, T2>(this Dictionary<T1, T2> dict)
		where T1 : notnull
		where T2 : notnull
	{
		Dictionary<T2, T1> dict_inverse = new ();
		foreach(T1 key in dict.Keys) {
			dict_inverse.Add(dict[key], key);
		}
		return dict_inverse;
	}

	// Convenience extension method to implicitly flush StringWriter.
	public static string output(this StringWriter writer) {
		writer.Flush();
		return writer.ToString();
	}

	// Returns a list of permission flags.
	public static List<Permissions> permissions_flags() {
		return new List<Permissions> (perms_descriptions.Keys);
	}
	// Returns the human readable display string for the permission.
	public static string description(this Permissions perms) {
		if (perms_descriptions.ContainsKey(perms)) {
			return perms_descriptions[perms];
		} else {
			return "Unknown";
		}
	}

	// Prints a DiscordColor in "#RRGGBB" format.
	public static string color_code(this DiscordColor color) {
		return $"#{color:X6}";
	}

	// Prints the "user#tag" of the user.
	// (Also works for `DiscordMember`s, of course.)
	public static string tag(this DiscordUser user) {
		return $"{user.Username}#{user.Discriminator}";
	}

	// Returns the DiscordMember equivalent of the DiscordUser.
	// Returns null if the conversion wasn't possible.
	public static async Task<DiscordMember?> member(this DiscordUser user) {
		// Check if trivially convertible.
		DiscordMember? member_n = user as DiscordMember;
		if (member_n is not null)
			{ return member_n; }

		// Check if guild is loaded (to convert users with).
		if (!is_guild_loaded)
			{ return null; }

		// Fetch the member by user ID.
		DiscordGuild erythro = await irene.GetGuildAsync(id_g_erythro);
		try {
			DiscordMember member = await erythro.GetMemberAsync(user.Id);
			return member;
		} catch (ServerErrorException) {
			return null;
		}
	}

	// Fetches audit log entries, but wrapping the call in a
	// try/catch block to handle exceptions.
	public static async Task<DiscordAuditLogEntry?> last_audit_entry(
		this DiscordGuild guild,
		AuditLogActionType? type ) {
		try {
			List<DiscordAuditLogEntry> entry =
				new (await guild.GetAuditLogsAsync(1, null, type));
			return (entry.Count < 1) ? null : entry[0];
		} catch {
			return null;
		}
	}
}
