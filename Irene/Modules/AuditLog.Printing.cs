namespace Irene.Modules;

static partial class AuditLog {

	// Append any changes to a display string.
	private static List<string> AddChanges(List<string> data_in, DiscordAuditLogMemberUpdateEntry? entry) {
		List<string> data = data_in;
		if (entry is null)
			return data;

		PropertyChange<string> nickname = entry.NicknameChange;
		if (DidChange(nickname))
			data = AddValueIfChanged(data, "Nickname", nickname);

		PropertyChange<bool?> deafened = entry.DeafenChange;
		if (DidChange(deafened))
			data = AddValueIfChanged(data, "Server deafened", deafened);
		
		PropertyChange<bool?> muted = entry.MuteChange;
		if (DidChange(muted))
			data = AddValueIfChanged(data, "Server muted", muted);

		if (entry.AddedRoles.Count > 0) {
			data.Add($"{_t}Role(s) added:");
			string roles = "";
			foreach (DiscordRole role in entry.AddedRoles)
				roles += $"**{role.Name}**, ";
			roles = roles[..^2];
			data.Add($"{_t2}{roles}");
		}

		if (entry.RemovedRoles.Count > 0) {
			data.Add($"{_t}Role(s) removed:");
			string roles = "";
			foreach (DiscordRole role in entry.AddedRoles)
				roles += $"**{role.Name}**, ";
			roles = roles[..^2];
			data.Add($"{_t2}{roles}");
		}

		return data;
	}
	private static List<string> AddChanges(List<string> data_in, DiscordAuditLogGuildEntry? entry) {
		List<string> data = data_in;
		if (entry is null)
			return data;

		PropertyChange<string> name = entry.NameChange;
		if (DidChange(name))
			data = AddValueIfChanged(data, "Name", name);

		PropertyChange<string> icon = entry.IconChange;
		if (DidChange(icon))
			data = AddIfChanged(data, "Icon", icon);

		PropertyChange<string> splash = entry.SplashChange;
		if (DidChange(splash))
			data = AddIfChanged(data, "Invite splash", splash);
		
		PropertyChange<string> region = entry.RegionChange;
		if (DidChange(region))
			data = AddValueIfChanged(data, "Region", region);

		PropertyChange<DiscordMember> owner = entry.OwnerChange;
		if (DidChange(owner))
			data = AddIfChanged(data, "Owner", owner);

		PropertyChange<VerificationLevel> verification = entry.VerificationLevelChange;
		if (DidChange(verification))
			data = AddValueIfChanged(data, "Member verification level", verification);

		PropertyChange<MfaLevel> auth_level = entry.MfaLevelChange;
		if (DidChange(auth_level))
			data = AddValueIfChanged(data, "Moderator 2FA requirement", auth_level);

		PropertyChange<ExplicitContentFilter> filter = entry.ExplicitContentFilterChange;
		if (DidChange(filter))
			data = AddValueIfChanged(data, "Explicit content filter", filter);

		PropertyChange<DefaultMessageNotifications> notifications = entry.NotificationSettingsChange;
		if (DidChange(notifications))
			data = AddValueIfChanged(data, "Default notifications", notifications);

		PropertyChange<DiscordChannel> ch_sys = entry.SystemChannelChange;
		if (DidChange(ch_sys))
			data = AddIfChanged(data, "System messages channel", ch_sys);

		PropertyChange<DiscordChannel> ch_afk = entry.AfkChannelChange;
		if (DidChange(ch_afk))
			data = AddIfChanged(data, "AFK channel", ch_afk);

		PropertyChange<DiscordChannel> ch_widget = entry.EmbedChannelChange;
		if (DidChange(ch_widget))
			data = AddIfChanged(data, "Server widget channel", ch_widget);

		return data;
	}
	private static List<string> AddChanges(List<string> data_in, DiscordAuditLogRoleUpdateEntry? entry) {
		List<string> data = data_in;
		if (entry is null)
			return data;

		PropertyChange<string> name = entry.NameChange;
		if (DidChange(name))
			data = AddValueIfChanged(data, "Name", name);

		PropertyChange<int?> color = entry.ColorChange;
		if (DidChange(color))
			data = AddIfChanged(data, "Color", color);

		PropertyChange<bool?> mentionable = entry.MentionableChange;
		if (DidChange(mentionable))
			data = AddValueIfChanged(data, "Mentionable by everyone", mentionable);

		PropertyChange<Permissions?> permissions = entry.PermissionChange;
		if (DidChange(permissions)) {
			Permissions perms_before = permissions.Before ?? Permissions.None;
			Permissions perms_after  = permissions.After  ?? Permissions.None;

			Permissions perms_delta = perms_before ^ perms_after;

			Permissions perms_added   = perms_after  & perms_delta;
			Permissions perms_removed = perms_before & perms_delta;

			if (perms_added != Permissions.None) {
				data.Add($"{_t}Permissions granted:");
				data.AddRange(PrintPermissions(perms_added));
			}
			if (perms_removed != Permissions.None) {
				data.Add($"{_t}Permissions revoked:");
				data.AddRange(PrintPermissions(perms_removed));
			}
		}

		return data;
	}
	private static List<string> AddChanges(List<string> data_in, DiscordAuditLogChannelEntry? entry) {
		List<string> data = data_in;
		if (entry is null)
			return data;

		PropertyChange<string> name = entry.NameChange;
		if (DidChange(name))
			data = AddValueIfChanged(data, "Channel name", name);

		PropertyChange<ChannelType?> type = entry.TypeChange;
		if (DidChange(type))
			data = AddValueIfChanged(data, "Channel type", type);

		// This should never happen. DiscordOverwrite changes go to
		// a different DiscordAuditLogEntry type.
		//PropertyChange<IReadOnlyList<DiscordOverwrite>> permissions =
		//	entry.OverwriteChange;
		//if (DidChange(permissions))
		//	data = print_change_perms(data, "Channel permissions", permissions);

		PropertyChange<bool?> nsfw = entry.NsfwChange;
		if (DidChange(nsfw))
			data = AddValueIfChanged(data, "Channel NSFW status", nsfw);

		PropertyChange<string> topic = entry.TopicChange;
		if (DidChange(topic))
			data = AddValueIfChanged(data, "Channel topic", topic);

		PropertyChange<int?> slowmode = entry.PerUserRateLimitChange;
		if (DidChange(slowmode))
			data = AddValueIfChanged(data, "Channel slowmode (sec/post)", slowmode);

		PropertyChange<int?> bitrate = entry.BitrateChange;
		if (DidChange(bitrate))
			data = AddValueIfChanged(data, "Channel bitrate (kbps)", bitrate);

		return data;
	}
	private static List<string> AddChanges(List<string> data_in, DiscordAuditLogOverwriteEntry? entry) {
		List<string> data = data_in;
		if (entry is null)
			return data;

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
			OverwriteType.Role   => $"`@{Guild!.GetRole(entity_id).Name}`",
			_ => "",
		};
		data.Add($"Permissions updated for {entity_str}.");

		// List "Allow" permissions.
		Permissions perms_allow_delta =
			entry.AllowChange.Before ^ overwrite.Allowed
			?? Permissions.None;
		Permissions perms_allow_added =
			perms_allow_delta & overwrite.Allowed;
		if (perms_allow_added != Permissions.None ) {
			data.Add($"{_t}Permissions now granted:");
			data.AddRange(PrintPermissions(perms_allow_added));
		}
		Permissions perms_allow_removed =
			perms_allow_delta & entry.AllowChange.Before
			?? Permissions.None;
		if (perms_allow_removed != Permissions.None) {
			data.Add($"{_t}Permissions no longer granted:");
			data.AddRange(PrintPermissions(perms_allow_removed));
		}

		// List "Deny" permissions.
		Permissions perms_deny_delta =
			entry.DenyChange.Before ^ overwrite.Denied
			?? Permissions.None;
		Permissions perms_deny_added =
			perms_deny_delta & overwrite.Denied;
		if (perms_deny_added != Permissions.None) {
			data.Add($"{_t}Permissions now denied:");
			data.AddRange(PrintPermissions(perms_deny_added));
		}
		Permissions perms_deny_removed =
			perms_deny_delta & entry.DenyChange.Before
			?? Permissions.None;
		if (perms_deny_removed != Permissions.None) {
			data.Add($"{_t}Permissions no longer denied:");
			data.AddRange(PrintPermissions(perms_deny_removed));
		}

		return data;
	}
	private static List<string> AddChanges(List<string> data_in, DiscordAuditLogEmojiEntry? entry) {
		List<string> data = data_in;
		if (entry is null)
			return data;

		DiscordEmoji emoji = entry.Target;
		data.Add($"{_t}Emoji updated:");
		data.Add($"{_t2}{AsData(emoji)}");

		PropertyChange<string> name = entry.NameChange;
		if (DidChange(name))
			data = AddValueIfChanged(data, "Name", name);

		return data;
	}

	// Syntax sugar for checking if a property change needs to be printed.
	private static bool DidChange<T>(PropertyChange<T> property) {
		if (property is null)
			return false;
		
		if (property.After is not null)
			return !property.After.Equals(property.Before);
		if (property.Before is not null)
			return !property.Before.Equals(property.After);

		// If the logic reaches this point, then the property must
		// always be null, and not have changed.
		return false;
	}
	
	// Prints the list of permissions (indented twice).
	private static List<string> PrintPermissions(Permissions permissions) {
		List<string> text = new ();

		IReadOnlyList<Permissions> permissions_all =
			Util.PermissionsFlags();
		foreach (Permissions permission_i in permissions_all) {
			// Do not print "None" as a changed field.
			if (permission_i == Permissions.None)
				continue;

			if (permissions.HasPermission(permission_i))
				text.Add($"{_t2}{_b} {permission_i.Description()}");
		}

		return text;
	}

	// Print the changes from a single property change item.
	private static List<string> AddValueIfChanged<T>(
		List<string> data,
		string label,
		PropertyChange<T> property
	) =>
		AddChange(
			data, label,
			property.Before?.ToString() ?? _n,
			property.After ?.ToString() ?? _n
		);
	private static List<string> AddIfChanged(
		List<string> data,
		string label,
		PropertyChange<DiscordColor?> color
	) =>
		AddChange(
			data, label,
			color.Before?.HexCode() ?? _n,
			color.After ?.HexCode() ?? _n
		);
	private static List<string> AddIfChanged(
		List<string> data,
		string label,
		PropertyChange<int?> color
	) {
		DiscordColor? color_before = (color.Before is null)
			? null
			: new ((int)color.Before);
		DiscordColor? color_after = (color.After  is null)
			? null
			: new ((int)color.After);
		return AddChange(
			data, label,
			color_before?.HexCode() ?? _n,
			color_after ?.HexCode() ?? _n
		);
	}
	private static List<string> AddIfChanged(
		List<string> data,
		string label,
		PropertyChange<DiscordMember> member
	) =>
		AddChange(
			data, label,
			AsData(member.Before) ?? _n,
			AsData(member.After ) ?? _n
		);
	private static List<string> AddIfChanged(
		List<string> data,
		string label,
		PropertyChange<DiscordChannel> channel
	) =>
		AddChange(
			data, label,
			channel.Before?.Mention ?? _n,
			channel.After?.Mention ?? _n
		);
	// This should never happen. DiscordOverwrite changes go to
	// a different DiscordAuditLogEntry type.
	//private static List<string> PrintChangePermissions(
	//	List<string> data,
	//	string label,
	//	PropertyChange<IReadOnlyList<DiscordOverwrite>> property
	//)
	private static List<string> AddIfChanged(
		List<string> data,
		string label,
		PropertyChange<string> image
	) {
		List<string> data_new = data;
		if (image.Before != image.After)
			data_new.Add($"{_t}{label} icon changed.");
		return data_new;
	}

	// Internal function used to print a stringified PropertyChange<T>.
	private static List<string> AddChange(
		List<string> data,
		string label,
		string before,
		string after
	) {
		List<string> data_new = data;
		if (before != after)
			data_new.Add($"{_t}{label}: {before} {_r} {after}");
		return data_new;
	}
}
