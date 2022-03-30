namespace Irene.Modules;

static partial class AuditLog {

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
		if (Guild is null)
			{ return; }

		// N.B.: The "After" fields in the OverwriteEntry are always copied
		// of the "Before" fields; we must use the `entry.Target` fields in
		// place of the "After" fields.
		DiscordOverwrite overwrite = entry.Target;
		UpdateGuild().RunSynchronously();

		// Display which entity is having their permissions changed.
		string entity_str = overwrite.Type switch {
			OverwriteType.Member => "member: ",
			OverwriteType.Role   => "role: ",
			_ => "unknown entity"
		};
		ulong entity_id = overwrite.Id;
		entity_str += overwrite.Type switch {
			OverwriteType.Member => $"`{Client.GetUserAsync(overwrite.Id).Result.Tag()}`",
			OverwriteType.Role   => $"`@{Guild.GetRole(entity_id).Name}`",
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

	// Syntax sugar for checking if a property change needs to be printed.
	static bool was_changed<T>(PropertyChange<T> property) {
		if (property is null)
			{ return false; }
		
		if (property.After is not null) {
			return !property.After.Equals(property.Before);
		}
		if (property.Before is not null) {
			return !property.Before.Equals(property.After);
		}

		// If the logic reaches this point, then the property must
		// always be null, and not have changed.
		return false;
	}
	
	// Prints the list of permissions (indented twice).
	static void print_perms(
		ref StringWriter text,
		Permissions perms ) {
		List<Permissions> perms_list = Util.PermissionsFlags();
		foreach (Permissions perms_i in perms_list) {
			// Do not print "None" ever, as a changed field.
			if (perms_i == Permissions.None)
				{ continue; }

			if (perms.HasPermission(perms_i)) {
				text.WriteLine($"{t}{t}{b} {perms_i.Description()}");
			}
		}
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
			color.Before?.HexCode() ?? n,
			color.After ?.HexCode() ?? n
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
			color_before?.HexCode() ?? n,
			color_after ?.HexCode() ?? n
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
	
	// Internal function used to print a stringified PropertyChange<T>.
	static void print_change_string(
		ref StringWriter text,
		string label,
		string before,
		string after ) {
		text.WriteLine($"{t}{label}: {before} {r} {after}");
	}
}
