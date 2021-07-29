using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;

using static Irene.Program;
using static Irene.Util;

namespace Irene.Modules {
	using AuditLogEntryTable = Dictionary<AuditLogActionType, DiscordAuditLogEntry?>;
	using id_r = RoleIDs;
	using id_ch = ChannelIDs;
	using id_e = EmojiIDs;

	static class AuditLog {
		static AuditLogEntryTable audit_log_base = new ();
		static bool is_audit_log_loaded = false;

		const string t = "\u2003\u2003";
		const string r = "\u21A6";
		const string n = "`N/A`";

		// Force static initializer to run.
		public static void init() { return; }

		static AuditLog() {
			// Get a baseline for most recent audit log entries of each type.
			irene.GuildDownloadCompleted += (irene, e) => {
				_ = Task.Run(init_audit_log_base);
				return Task.CompletedTask;
			};

			// New member joined server.
			// (Includes bots being added to the server.)
			irene.GuildMemberAdded += (irene, e) => {
				_ = Task.Run(() => {
					DiscordMember member = e.Member;
					log_entry($"**Member joined:** {member_string(member)}");
				});
				return Task.CompletedTask;
			};
			// Member left server.
			// (Includes member pruning and members being kicked.)
			irene.GuildMemberRemoved += (irene, e) => {
				_ = Task.Run(() => {
					DiscordMember member = e.Member;
					log_entry($"**Member left:** {member_string(member)}");
				});
				return Task.CompletedTask;
			};

			// User banned.
			irene.GuildBanAdded += (irene, e) => {
				_ = Task.Run(() => {
					DiscordMember member = e.Member;

					// Fetch additional data.
					DiscordAuditLogBanEntry? entry =
						find_entry(AuditLogActionType.Ban)
						as DiscordAuditLogBanEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**User banned:** {member_string(member)}");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
			// User unbanned.
			irene.GuildBanRemoved += (irene, e) => {
				_ = Task.Run(() => {
					DiscordMember member = e.Member;

					// Fetch additional data.
					DiscordAuditLogBanEntry? entry =
						find_entry(AuditLogActionType.Unban)
						as DiscordAuditLogBanEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**User unbanned:** {member_string(member)}");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};

			// User info/roles updated.
			irene.GuildMemberUpdated += (irene, e) => {
				_ = Task.Run(() => {
					DiscordMember member = e.Member;

					// Fetch additional data.
					DiscordAuditLogMemberUpdateEntry? entry =
						find_entry(AuditLogActionType.MemberUpdate)
						as DiscordAuditLogMemberUpdateEntry;
					DiscordAuditLogMemberUpdateEntry? entry_roles =
						find_entry(AuditLogActionType.MemberRoleUpdate)
						as DiscordAuditLogMemberUpdateEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**Member info updated:** {member_string(member)}");
					if (entry is not null) {
						print_changes(ref text, entry);
						try_add_data(ref text, entry);
					}
					if (entry_roles is not null) {
						print_changes(ref text, entry);
						try_add_data(ref text, entry_roles);
					}
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};

			// Member was disconnected from channel.
			// Member was moved from channel.
			// ^ Both of these events are wrapped into "voice state" event,
			//   but there is no easy way to distinguish user-initiated ones.

			// Guild updated.
			irene.GuildUpdated += (irene, e) => {
				_ = Task.Run(() => {
					// Fetch additional data.
					DiscordAuditLogGuildEntry? entry =
						find_entry(AuditLogActionType.GuildUpdate)
						as DiscordAuditLogGuildEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine("**Server settings updated.**");
					print_changes(ref text, entry);
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};

			// Role created.
			irene.GuildRoleCreated += (irene, e) => {
				_ = Task.Run(() => {
					DiscordRole role = e.Role;

					// Fetch additional data.
					DiscordAuditLogRoleUpdateEntry? entry =
						find_entry(AuditLogActionType.RoleCreate)
						as DiscordAuditLogRoleUpdateEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**New role created:** {role.Name} (`{role.Id}`)");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
			// Role deleted.
			irene.GuildRoleDeleted += (irene, e) => {
				_ = Task.Run(() => {
					DiscordRole role = e.Role;

					// Fetch additional data.
					DiscordAuditLogRoleUpdateEntry? entry =
						find_entry(AuditLogActionType.RoleDelete)
						as DiscordAuditLogRoleUpdateEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**Role deleted:** {role.Name} (`{role.Id}`)");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
			// Role updated.
			irene.GuildRoleUpdated += (irene, e) => {
				_ = Task.Run(() => {
					DiscordRole role_before = e.RoleBefore;
					DiscordRole role_after = e.RoleAfter;

					// Fetch additional data.
					DiscordAuditLogRoleUpdateEntry? entry =
						find_entry(AuditLogActionType.RoleUpdate)
						as DiscordAuditLogRoleUpdateEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**Role settings updated:** {role_after.Name} (`{role_after.Id}`)");
					print_changes(ref text, entry);
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};

			// Channel created.
			irene.ChannelCreated += (irene, e) => {
				_ = Task.Run(() => {
					DiscordChannel ch = e.Channel;

					// Do not log if channel is private.
					if (ch.IsPrivate)
						{ return; }

					// Fetch additional data.
					DiscordAuditLogChannelEntry? entry =
						find_entry(AuditLogActionType.ChannelCreate)
						as DiscordAuditLogChannelEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**New channel{(ch.IsCategory ? " category " : " ")}created:** {ch.Mention}");
					text.WriteLine($"{ch.Name} (type: {ch.Type}): `{ch.Id}`");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
			// Channel deleted.
			irene.ChannelDeleted += (irene, e) => {
				_ = Task.Run(() => {
					DiscordChannel ch = e.Channel;

					// Do not log if channel is private.
					if (ch.IsPrivate)
						{ return; }

					// Fetch additional data.
					DiscordAuditLogChannelEntry? entry =
						find_entry(AuditLogActionType.ChannelDelete)
						as DiscordAuditLogChannelEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**Channel{(ch.IsCategory ? " category " : " ")}deleted:** {ch.Mention}");
					text.WriteLine($"{ch.Name} (type: {ch.Type}): `{ch.Id}`");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
			// Channel settings updated.
			// (Includes updating channel permission overwrites.)
			irene.ChannelUpdated += (irene, e) => {
				_ = Task.Run(() => {
					DiscordChannel ch = e.ChannelAfter;

					// Do not log if channel is private.
					if (ch.IsPrivate)
						{ return; }

					// Fetch additional data.
					DiscordAuditLogChannelEntry? entry_channel =
						find_entry(AuditLogActionType.ChannelUpdate)
						as DiscordAuditLogChannelEntry;
					DiscordAuditLogOverwriteEntry? entry_perms_create =
						find_entry(AuditLogActionType.OverwriteCreate)
						as DiscordAuditLogOverwriteEntry;
					DiscordAuditLogOverwriteEntry? entry_perms_delete =
						find_entry(AuditLogActionType.OverwriteDelete)
						as DiscordAuditLogOverwriteEntry;
					DiscordAuditLogOverwriteEntry? entry_perms_update =
						find_entry(AuditLogActionType.OverwriteUpdate)
						as DiscordAuditLogOverwriteEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**Channel{(ch.IsCategory ? " category " : " ")}settings updated:** {ch.Mention}");
					text.WriteLine($"{ch.Name} (type: {ch.Type}): `{ch.Id}`");
					if (entry_channel is not null) {
						print_changes(ref text, entry_channel);
						try_add_data(ref text, entry_channel);
					}
					if (entry_perms_create is not null) {
						print_changes(ref text, entry_perms_create);
						try_add_data(ref text, entry_perms_create);
					}
					if (entry_perms_delete is not null) {
						print_changes(ref text, entry_perms_delete);
						try_add_data(ref text, entry_perms_delete);
					}
					if (entry_perms_update is not null) {
						print_changes(ref text, entry_perms_update);
						try_add_data(ref text, entry_perms_update);
					}
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};

			// Emoji created, deleted, or updated.
			irene.GuildEmojisUpdated += (irene, e) => {
				_ = Task.Run(() => {
					// Diff the two lists of emojis.
					HashSet<ulong> emojis_before = new (e.EmojisBefore.Keys);
					HashSet<ulong> emojis_after  = new (e.EmojisAfter.Keys);

					HashSet<ulong> emojis_added = new (emojis_after);
					emojis_added.ExceptWith(emojis_before);

					HashSet<ulong> emojis_removed = new (emojis_before);
					emojis_removed.ExceptWith(emojis_after);

					// Fetch additional data.
					DiscordAuditLogEmojiEntry? entry_create =
						find_entry(AuditLogActionType.EmojiCreate)
						as DiscordAuditLogEmojiEntry;
					DiscordAuditLogEmojiEntry? entry_delete =
						find_entry(AuditLogActionType.EmojiDelete)
						as DiscordAuditLogEmojiEntry;
					DiscordAuditLogEmojiEntry? entry_update =
						find_entry(AuditLogActionType.EmojiUpdate)
						as DiscordAuditLogEmojiEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine("**Server emojis updated.**");
					if (emojis_added.Count > 0) {
						text.WriteLine($"{t}Emoji added:");
						foreach (ulong id in emojis_added) {
							DiscordEmoji emoji = e.EmojisAfter[id];
							text.WriteLine($"{t}{t}{emoji_string(emoji)}`");
						}
					}
					if (emojis_removed.Count > 0) {
						text.WriteLine($"{t}Emoji added:");
						foreach (ulong id in emojis_removed) {
							DiscordEmoji emoji = e.EmojisBefore[id];
							text.WriteLine($"{t}{t}{emoji_string(emoji)}`");
						}
					}
					if (entry_create is not null) {
						// No need to print additions; already displayed.
						try_add_data(ref text, entry_create);
					}
					if (entry_delete is not null) {
						// No need to print removals; already displayed.
						try_add_data(ref text, entry_delete);
					}
					if (entry_update is not null) {
						print_changes(ref text, entry_update);
						try_add_data(ref text, entry_update);
					}
					log_entry(text.output());

				});
				return Task.CompletedTask;
			};

			// sticker create/delete/update

			// Invite created.
			irene.InviteCreated += (irene, e) => {
				_ = Task.Run(() => {
					DiscordInvite inv = e.Invite;
					DiscordUser user = inv.Inviter;
					TimeSpan expiry = TimeSpan.FromSeconds(inv.MaxAge);

					// Fetch additional data.
					DiscordAuditLogInviteEntry? entry =
						find_entry(AuditLogActionType.InviteCreate)
						as DiscordAuditLogInviteEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**Invite created:** `{inv.Code}`");
					text.WriteLine($"Created by {user.tag()}, can be used {inv.MaxUses} times, expires in {expiry:g}.`");
					text.WriteLine($"This invite grants {(inv.IsTemporary ? "temporary" : "normal")} access.");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
			// Invite deleted.
			irene.InviteDeleted += (irene, e) => {
				_ = Task.Run(() => {
					DiscordInvite inv = e.Invite;
					DiscordUser user = inv.Inviter;
					TimeSpan expiry = TimeSpan.FromSeconds(inv.MaxAge);

					// Fetch additional data.
					DiscordAuditLogInviteEntry? entry =
						find_entry(AuditLogActionType.InviteCreate)
						as DiscordAuditLogInviteEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**Invite deleted:** `{inv.Code}`");
					text.WriteLine($"Created by {user.tag()}, expired in {expiry:g}.`");
					text.WriteLine($"This invite granted {(inv.IsTemporary ? "temporary" : "normal")} access.");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};

			// webhook create/delete/update

			// integration create/delete/update

			// Message deleted.
			irene.MessageDeleted += (irene, e) => {
				_ = Task.Run(() => {
					DiscordChannel ch = e.Channel;
					if (ch.IsPrivate)
						{ return; }
					DiscordMessage msg = e.Message;

					// Fetch additional data.
					DiscordAuditLogMessageEntry? entry =
						find_entry(AuditLogActionType.MessageDelete)
						as DiscordAuditLogMessageEntry;

					// Only log this event if the author isn't the user to
					// delete the message.
					// (Be overly-conservative: exit early if entry not found.)
					if (entry is null)
						{ return; }
					if (entry.UserResponsible == msg.Author)
						{ return; }

					// Format output.
					StringWriter text = new ();
					text.WriteLine($"**Message deleted.** `{msg.Id}`");
					string timestamp = $"<t:{msg.Timestamp.ToUnixTimeSeconds()}:f>";
					text.WriteLine($"Originally posted in {msg.Channel.Mention}, on {timestamp}.");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
			// Messages bulk deleted.
			irene.MessagesBulkDeleted += (irene, e) => {
				_ = Task.Run(() => {
					List<DiscordMessage> messages = new (e.Messages);

					// Fetch additional data.
					DiscordAuditLogMessageEntry? entry =
						find_entry(AuditLogActionType.MessageBulkDelete)
						as DiscordAuditLogMessageEntry;

					// Format output.
					StringWriter text = new ();
					text.WriteLine("**Messages bulk deleted.**");
					text.WriteLine($"Removed `{messages.Count}` message(s).");
					try_add_data(ref text, entry);
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};

			// All reactions cleared on message.
			irene.MessageReactionsCleared += (irene, e) => {
				_ = Task.Run(() => {
					DiscordMessage msg = e.Message;

					// Do not log if channel is private.
					DiscordChannel ch =
						irene.GetChannelAsync(msg.ChannelId).Result;
					if (ch.IsPrivate) {
						return;
					}

					StringWriter text = new ();
					text.WriteLine("**All reactions cleared from message.**");
					text.WriteLine($"{t}message ID:`{msg.Id}`");
					text.WriteLine($"{t}<{msg.JumpLink}>");
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
			// All reactions of a specific emoji cleared on message.
			irene.MessageReactionRemovedEmoji += (irene, e) => {
				_ = Task.Run(() => {
					DiscordMessage msg = e.Message;
					DiscordEmoji emoji = e.Emoji;

					// Do not log if channel is private.
					DiscordChannel ch =
						irene.GetChannelAsync(msg.ChannelId).Result;
					if (ch.IsPrivate) {
						return;
					}

					StringWriter text = new ();
					text.WriteLine($"**Specific emoji reactions cleared from message:** `{emoji.GetDiscordName()}`");
					text.WriteLine($"{t}message ID:`{msg.Id}`");
					text.WriteLine($"{t}<{msg.JumpLink}>");
					log_entry(text.output());
				});
				return Task.CompletedTask;
			};
		}

		// Convenience function for outputting a log message.
		static void log_entry(string data) {
			const string arrow = "\u2B9A";

			// Log data to console.
			log.info("Audit log entry added.");
			log.debug($"  {data}");
			log.endl();

			// Log data to audit log channel.
			if (is_guild_loaded) {
				DateTimeOffset time = DateTimeOffset.UtcNow;
				string time_str = $"<t:{time.ToUnixTimeSeconds()}:f>";
				string msg = $"{arrow} {time_str}\n{data}";
				_ = channels[id_ch.audit].SendMessageAsync(
					new DiscordMessageBuilder()
					.WithContent(msg)
					.WithAllowedMentions(Mentions.None)
				);
			}
		}

		// Takes a StringWriter and adds DiscordMember / reason data
		// to it, if they exist / can be found.
		static void try_add_data(ref StringWriter text, DiscordAuditLogEntry? entry) {
			if (entry is null)
				{ return; }

			// DiscordMember data
			if (is_guild_loaded) {
				DiscordGuild erythro =
							irene.GetGuildAsync(id_g_erythro).Result;
				DiscordUser user = entry.UserResponsible;
				DiscordMember member =
							erythro.GetMemberAsync(user.Id).Result;
				text.WriteLine($"Action by: {member_string(member)}");
			}

			// Reason data
			string reason = entry.Reason?.Trim() ?? "";
			if (reason != "") {
				text.WriteLine($"> {reason}");
			}
		}

		// String representation of a user, without needing to ping them.
		static string member_string(DiscordMember member) {
			return $"{member.DisplayName} ({member.tag()})";
		}
		// String representation of an emoji.
		static string emoji_string(DiscordEmoji emoji) {
			return $"{emoji} ({emoji.GetDiscordName()}): `{emoji.Id}`";
		}

		// Syntax sugar for checking if a property change needs to be printed.
		static bool was_changed<T>(PropertyChange<T> property) {
			return property.After?.Equals(property.Before)
				?? true;
		}
		
		// Prints the list of permissions (indented twice).
		static void print_perms(
			ref StringWriter text,
			Permissions perms ) {
			List<Permissions> perms_list = permissions_flags();
			foreach (Permissions perms_i in perms_list) {
				// Do not print "None" ever, as a changed field.
				if (perms_i == Permissions.None)
					{ continue; }

				if (perms.HasPermission(perms_i)) {
					text.WriteLine($"{t}{t}{perms_i.description()}");
				}
			}
		}
		
		// Internal function used to print a stringified PropertyChange<T>.
		static void print_change_string(
			ref StringWriter text,
			string label,
			string before,
			string after ) {
			text.WriteLine($"{t}{label}: {before} {r} {after}");
		}

		// Print the changes from a single property change item.
		static void print_change_val<T>(
			ref StringWriter text,
			PropertyChange<T> property,
			string label ) {
			print_change_string(
				ref text,
				label,
				property.Before?.ToString() ?? n,
				property.After ?.ToString() ?? n
			);
		}
		static void print_change_color(
			ref StringWriter text,
			PropertyChange<DiscordColor?> color,
			string label ) {
			print_change_string(
				ref text,
				label,
				color.Before?.color_code() ?? n,
				color.After ?.color_code() ?? n
			);
		}
		static void print_change_color(
			ref StringWriter text,
			PropertyChange<int?> color,
			string label ) {
			DiscordColor? color_before = (color.Before is null) ? null : new ((int)color.Before);
			DiscordColor? color_after  = (color.After  is null) ? null : new ((int)color.After );
			print_change_string(
				ref text,
				label,
				color_before?.color_code() ?? n,
				color_after ?.color_code() ?? n
			);
		}
		static void print_change_member(
			ref StringWriter text,
			PropertyChange<DiscordMember> member,
			string label ) {
			print_change_string(
				ref text,
				label,
				member_string(member.Before) ?? n,
				member_string(member.After ) ?? n
			);
		}
		static void print_change_channel(
			ref StringWriter text,
			PropertyChange<DiscordChannel> ch,
			string label ) {
			print_change_string(
				ref text,
				label,
				ch.Before?.Mention ?? n,
				ch.After ?.Mention ?? n
			);
		}
		static void print_change_perms(
			ref StringWriter text,
			PropertyChange<IReadOnlyList<DiscordOverwrite>> property,
			string label ) {
			// This should never happen. DiscordOverwrite changes go to
			// a different DiscordAuditLogEntry type.
			text.WriteLine($"{t}{label} changed.");
		}
		static void print_change_img(
			ref StringWriter text,
			PropertyChange<string> img,
			string label ) {
			text.WriteLine($"{t}{label} icon changed.");
		}

		// Fetches the most recent audit log entry of the given type.
		// Blocks the thread while it fetches the entry.
		// Returns null if entries cannot be searched.
		static DiscordAuditLogEntry? find_entry(AuditLogActionType type) {
			const int retry_interval = 500; // msec
			const int retry_count = 5;

			// Exit early if guilds aren't loaded.
			if (!is_guild_loaded) {
				log.warning("    Cannot fetch audit logs before guild is ready.");
				return null;
			}
			// Exit early if audit log baseline hasn't been initialized.
			if (!is_audit_log_loaded) {
				log.warning("    Must fetch baseline audit logs first.");
				return null;
			}

			// Repeatedly try to find the updated entry.
			DiscordGuild erythro = irene.GetGuildAsync(id_g_erythro).Result;
			for (int i=0; i<retry_count; i++) {
				DiscordAuditLogEntry? entry = erythro.last_audit_entry(type).Result;

				// Return the entry if one was found and is new.
				// Also update the "most recent" audit log entry of that type.
				if (entry is not null) {
					if (entry.Id != (audit_log_base[type]?.Id ?? null)) {
						audit_log_base[type] = entry;
						return entry;
					}
				}

				// Pause slightly before retrying.
				Thread.Sleep(retry_interval);
			}

			// Return null if nothing could be found.
			log.debug("    No new corresponding audit logs found.");
			return null;
		}

		// Append any changes to a display string.
		static void print_changes(ref StringWriter text, DiscordAuditLogMemberUpdateEntry? entry) {
			if (entry is null)
				{ return; }

			PropertyChange<string> nickname = entry.NicknameChange;
			if (was_changed(nickname))
				{ print_change_val(ref text, nickname, "Nickname"); }

			PropertyChange<bool?> deafened = entry.DeafenChange;
			if (was_changed(deafened))
				{ print_change_val(ref text, deafened, "Server deafened"); }
			
			PropertyChange<bool?> muted = entry.MuteChange;
			if (was_changed(muted))
				{ print_change_val(ref text, muted, "Server muted"); }

			if (entry.AddedRoles.Count > 0) {
				text.WriteLine($"{t}Role(s) added:");
				string roles = "";
				foreach (DiscordRole role in entry.AddedRoles) {
					roles += $"**{role.Name}**, ";
				}
				roles = roles[..^2];
				text.WriteLine($"{t}{t}{roles}");
			}

			if (entry.RemovedRoles.Count > 0) {
				text.WriteLine($"{t}Role(s) removed:");
				string roles = "";
				foreach (DiscordRole role in entry.AddedRoles) {
					roles += $"**{role.Name}**, ";
				}
				roles = roles[..^2];
				text.WriteLine($"{t}{t}{roles}");
			}
		}
		static void print_changes(ref StringWriter text, DiscordAuditLogGuildEntry? entry) {
			if (entry is null)
				{ return; }

			PropertyChange<string> name = entry.NameChange;
			if (was_changed(name))
				{ print_change_val(ref text, name, "Name"); }

			PropertyChange<string> icon = entry.IconChange;
			if (was_changed(icon))
				{ print_change_img(ref text, icon, "Icon"); }

			PropertyChange<string> splash = entry.SplashChange;
			if (was_changed(splash))
				{ print_change_img(ref text, splash, "Invite splash"); }
			
			PropertyChange<string> region = entry.RegionChange;
			if (was_changed(region))
				{ print_change_val(ref text, region, "Region"); }

			PropertyChange<DiscordMember> owner = entry.OwnerChange;
			if (was_changed(owner))
				{ print_change_member(ref text, owner, "Owner"); }

			PropertyChange<VerificationLevel> verification = entry.VerificationLevelChange;
			if (was_changed(verification))
				{ print_change_val(ref text, verification, "Member verification level"); }

			PropertyChange<MfaLevel> auth_level = entry.MfaLevelChange;
			if (was_changed(auth_level))
				{ print_change_val(ref text, auth_level, "Moderator 2FA requirement"); }

			PropertyChange<ExplicitContentFilter> filter = entry.ExplicitContentFilterChange;
			if (was_changed(filter))
				{ print_change_val(ref text, filter, "Explicit content filter"); }

			PropertyChange<DefaultMessageNotifications> notifications = entry.NotificationSettingsChange;
			if (was_changed(notifications))
				{ print_change_val(ref text, notifications, "Default notifications"); }

			PropertyChange<DiscordChannel> ch_sys = entry.SystemChannelChange;
			if (was_changed(ch_sys))
				{ print_change_channel(ref text, ch_sys, "System messages channel"); }

			PropertyChange<DiscordChannel> ch_afk = entry.AfkChannelChange;
			if (was_changed(ch_afk))
				{ print_change_channel(ref text, ch_afk, "AFK channel"); }

			PropertyChange<DiscordChannel> ch_widget = entry.EmbedChannelChange;
			if (was_changed(ch_widget))
				{ print_change_channel(ref text, ch_widget, "Server widget channel"); }
		}
		static void print_changes(ref StringWriter text, DiscordAuditLogRoleUpdateEntry? entry) {
			if (entry is null)
				{ return; }

			PropertyChange<string> name = entry.NameChange;
			if (was_changed(name))
				{ print_change_val(ref text, name, "Name"); }

			PropertyChange<int?> color = entry.ColorChange;
			if (was_changed(color))
				{ print_change_color(ref text, color, "Color"); }

			PropertyChange<bool?> mentionable = entry.MentionableChange;
			if (was_changed(mentionable))
				{ print_change_val(ref text, mentionable, "Mentionable by everyone"); }

			PropertyChange<Permissions?> permissions = entry.PermissionChange;
			if (was_changed(permissions)) {
				Permissions perms_before = permissions.Before ?? Permissions.None;
				Permissions perms_after  = permissions.After  ?? Permissions.None;

				Permissions perms_delta = perms_before ^ perms_after;

				Permissions perms_added   = perms_after  & perms_delta;
				Permissions perms_removed = perms_before & perms_delta;

				if (perms_added != Permissions.None) {
					text.WriteLine($"{t}Permissions granted:");
					print_perms(ref text, perms_added);
				}
				if (perms_removed != Permissions.None) {
					text.WriteLine($"{t}Permissions revoked:");
					print_perms(ref text, perms_removed);
				}
			}
		}
		static void print_changes(ref StringWriter text, DiscordAuditLogChannelEntry? entry) {
			if (entry is null)
				{ return; }

			PropertyChange<string> name = entry.NameChange;
			if (was_changed(name))
				{ print_change_val(ref text, name, "Channel name"); }

			PropertyChange<ChannelType?> type = entry.TypeChange;
			if (was_changed(type))
				{ print_change_val(ref text, type, "Channel type"); }

			// This should never happen. DiscordOverwrite changes go to
			// a different DiscordAuditLogEntry type.
			PropertyChange<IReadOnlyList<DiscordOverwrite>> permissions =
				entry.OverwriteChange;
			if (was_changed(permissions))
				{ print_change_perms(ref text, permissions, "Channel permissions"); }

			PropertyChange<bool?> nsfw = entry.NsfwChange;
			if (was_changed(nsfw))
				{ print_change_val(ref text, nsfw, "Channel NSFW status"); }

			PropertyChange<string> topic = entry.TopicChange;
			if (was_changed(topic))
				{ print_change_val(ref text, topic, "Channel topic"); }

			PropertyChange<int?> slowmode = entry.PerUserRateLimitChange;
			if (was_changed(slowmode))
				{ print_change_val(ref text, slowmode, "Channel slowmode (sec/post)"); }

			PropertyChange<int?> bitrate = entry.BitrateChange;
			if (was_changed(bitrate))
				{ print_change_val(ref text, bitrate, "Channel bitrate (kbps)"); }
		}
		static void print_changes(ref StringWriter text, DiscordAuditLogOverwriteEntry? entry) {
			if (entry is null)
				{ return; }
			if (!is_guild_loaded)
				{ return; }

			// N.B.: The "After" fields in the OverwriteEntry are always copied
			// of the "Before" fields; we must use the `entry.Target` fields in
			// place of the "After" fields.
			DiscordOverwrite overwrite = entry.Target;
			DiscordGuild erythro = irene.GetGuildAsync(id_g_erythro).Result;

			// Display which entity is having their permissions changed.
			string entity_str = overwrite.Type switch {
				OverwriteType.Member => "member: ",
				OverwriteType.Role   => "role: ",
				_ => "unknown entity"
			};
			ulong entity_id = overwrite.Id;
			entity_str += overwrite.Type switch {
				OverwriteType.Member => $"`{irene.GetUserAsync(overwrite.Id).Result.tag()}`",
				OverwriteType.Role   => $"`@{erythro.GetRole(entity_id).Name}`",
				_ => "",
			};
			text.WriteLine($"Permissions updated for {entity_str}.");

			// List "Allow" permissions.
			Permissions perms_allow_delta =
				entry.AllowChange.Before ^ overwrite.Allowed
				?? Permissions.None;
			Permissions perms_allow_added =
				perms_allow_delta & overwrite.Allowed;
			if (perms_allow_added != Permissions.None ) {
				text.WriteLine($"{t}Permissions now granted:");
				print_perms(ref text, perms_allow_added);
			}
			Permissions perms_allow_removed =
				perms_allow_delta & entry.AllowChange.Before
				?? Permissions.None;
			if (perms_allow_removed != Permissions.None) {
				text.WriteLine($"{t}Permissions no longer granted:");
				print_perms(ref text, perms_allow_removed);
			}

			// List "Deny" permissions.
			Permissions perms_deny_delta =
				entry.DenyChange.Before ^ overwrite.Denied
				?? Permissions.None;
			Permissions perms_deny_added =
				perms_deny_delta & overwrite.Denied;
			if (perms_deny_added != Permissions.None) {
				text.WriteLine($"{t}Permissions now denied:");
				print_perms(ref text, perms_deny_added);
			}
			Permissions perms_deny_removed =
				perms_deny_delta & entry.DenyChange.Before
				?? Permissions.None;
			if (perms_deny_removed != Permissions.None) {
				text.WriteLine($"{t}Permissions no longer denied:");
				print_perms(ref text, perms_deny_removed);
			}
		}
		static void print_changes(ref StringWriter text, DiscordAuditLogEmojiEntry? entry) {
			if (entry is null)
				{ return; }

			DiscordEmoji emoji = entry.Target;
			text.WriteLine($"{t}Emoji updated:");
			text.WriteLine($"{t}{t}{emoji_string(emoji)}");

			PropertyChange<string> name = entry.NameChange;
			if (was_changed(name))
				{ print_change_val(ref text, name, "Name"); }
		}

		// Initialize the audit log table with "base" values.
		// Compared to later to determine if a new entry was added.
		static async void init_audit_log_base() {
			DiscordGuild erythro = await irene.GetGuildAsync(id_g_erythro);

			List<AuditLogActionType> types = new () {
				AuditLogActionType.Ban,
				AuditLogActionType.Unban,
				AuditLogActionType.MemberUpdate,
				AuditLogActionType.MemberRoleUpdate,
				AuditLogActionType.GuildUpdate,
				AuditLogActionType.RoleCreate,
				AuditLogActionType.RoleDelete,
				AuditLogActionType.RoleUpdate,
				AuditLogActionType.ChannelCreate,
				AuditLogActionType.ChannelDelete,
				AuditLogActionType.ChannelUpdate,
				AuditLogActionType.OverwriteCreate,
				AuditLogActionType.OverwriteDelete,
				AuditLogActionType.OverwriteUpdate,
				AuditLogActionType.EmojiCreate,
				AuditLogActionType.EmojiDelete,
				AuditLogActionType.EmojiUpdate,
				AuditLogActionType.InviteCreate,
				AuditLogActionType.InviteDelete,
				AuditLogActionType.MessageDelete,
				AuditLogActionType.MessageBulkDelete,
			};

			foreach (AuditLogActionType type in types) {
				DiscordAuditLogEntry? entry =
					erythro.last_audit_entry(type).Result;
				audit_log_base.Add(type, entry);
			}

			is_audit_log_loaded = true;
		}
	}
}
