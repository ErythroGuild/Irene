using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Emzi0767.Utilities;

using static Irene.Program;

namespace Irene.Commands {
	using DropdownLambda = AsyncEventHandler<DiscordClient, ComponentInteractionCreateEventArgs>;
	using id_r = RoleIDs;
	using id_e = EmojiIDs;

	class Roles : ICommands {
		public enum PingRole {
			Raid,
			Mythics, KSM, Gearing,
			Events, Herald,
		}

		class RoleDropdown {
			static readonly TimeSpan timeout = TimeSpan.FromMinutes(5);
			static readonly List<RoleDropdown> handlers = new ();

			static readonly Dictionary<PingRole, string> dict_ids = new () {
				{ PingRole.Raid   , "option_raid"    },
				{ PingRole.Mythics, "option_mythics" },
				{ PingRole.KSM    , "option_ksm"     },
				{ PingRole.Gearing, "option_gearing" },
				{ PingRole.Events , "option_events"  },
				{ PingRole.Herald , "option_herald"  },
			};
			static readonly Dictionary<PingRole, DiscordComponentEmoji> dict_emojis = new () {
				{ PingRole.Raid   , new ("\U0001F409") },   // :dragon:
				{ PingRole.Mythics, new ("\U0001F5FA") },   // :map:
				{ PingRole.KSM    , new ("\U0001F94B") },   // :martial_arts_uniform:
				{ PingRole.Gearing, new ("\U0001F392") },   // :school_satchel:
				{ PingRole.Events , new ("\U0001F938\u200D\u2640\uFE0F") }, // :woman_cartwheeling:
				// { PingRole.Events , new ("\U0001FA97") },   // :accordion:
				{ PingRole.Herald , new ("\u2604"    ) },   // :comet:
			};

			public readonly List<PingRole> roles;
			public readonly DiscordMember author;
			public DiscordMessage? msg;

			readonly Timer timer;
			readonly DropdownLambda handler;

			public RoleDropdown(List<PingRole> roles, DiscordMember author) {
				// Initialize members.
				this.roles = roles;
				this.author = author;

				timer = new Timer(timeout.TotalMilliseconds);
				timer.AutoReset = false;

				handler = async (irene, e) => {
					// Ignore triggers from the wrong message.
					if (e.Message != msg) {
						return;
					}

					// Ignore people who aren't the original user.
					if (e.User != author) {
						await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
						return;
					}

					// Set roles.
					roles = str_to_roles(new List<string>(e.Values));
					log.info($"Updating roles for {author.DisplayName}.");
					assign(author, roles);

					// Update page content.
					await e.Interaction.CreateResponseAsync(
						InteractionResponseType.UpdateMessage,
						new DiscordInteractionResponseBuilder()
						.WithContent(display_roles(roles))
						.AddComponents(get_dropdown(roles))
					);

					// Refresh deactivation timer.
					timer.Stop();
					timer.Start();
				};

				// Configure timeout event listener.
				timer.Elapsed += async (s, e) => {
					irene.ComponentInteractionCreated -= handler;
					if (msg is not null) {
						await msg.ModifyAsync(
							new DiscordMessageBuilder()
							.WithContent(msg.Content)
							.AddComponents(get_dropdown(roles, false))
						);
					}
					handlers.Remove(this);
				};

				// Attach handler to client and start deactivation timer.
				handlers.Add(this);
				irene.ComponentInteractionCreated += handler;
				timer.Start();
			}

			// Returns a standalone DiscordMessage to init with.
			public DiscordMessageBuilder init_msg() {
				return new DiscordMessageBuilder()
					.WithContent(display_roles(roles))
					.AddComponents(get_dropdown(roles));
			}

			// Formats the given list of roles into a string.
			static string display_roles(List<PingRole> roles) {
				// Special cases for none/singular.
				if (roles.Count == 0) {
					return "No roles currently set.";
				}
				if (roles.Count == 1) {
					return $"Role currently set: **{dict_names[roles[0]]}**";
				}

				// Construct list of role names.
				StringWriter text = new ();
				text.Write("Roles currently set:  ");
				foreach (PingRole role in roles) {
					text.Write($"**{dict_names[role]}**  ");
				}
				return text.output()[..^2];
			}

			// Returns a dropdown of all roles; some of them pre-selected.
			static DiscordSelectComponent get_dropdown(List<PingRole> roles, bool is_enabled = true) {
				return new DiscordSelectComponent(
					id_dropdown,
					"No roles selected",
					get_dropdown_options(roles),
					!is_enabled,
					0, dict_ids.Count
				);
			}

			// Returns an option list of all available roles to select.
			static DiscordSelectComponentOption[] get_dropdown_options(List<PingRole> roles) {
				DiscordSelectComponentOption option(PingRole role) {
					return new DiscordSelectComponentOption(
						dict_names[role],
						dict_ids[role],
						dict_summaries[role],
						roles.Contains(role),
						dict_emojis[role]
					);
				}

				return new DiscordSelectComponentOption[] {
				option(PingRole.Raid   ),
				option(PingRole.Mythics),
				option(PingRole.KSM    ),
				option(PingRole.Gearing),
				option(PingRole.Events ),
				option(PingRole.Herald ),
			};
			}

			// Convert the returned event arg IDs to `PingRole`s.
			static List<PingRole> str_to_roles(List<string> ids) {
				List<PingRole> list = new ();
				foreach (PingRole role in dict_ids.Keys) {
					if (ids.Contains(dict_ids[role])) {
						list.Add(role);
					}
				}
				return list;
			}
		}

		static readonly Dictionary<PingRole, DiscordRole> pingRole_to_discordRole = new () {
			{ PingRole.Raid   , roles[id_r.raid   ] },
			{ PingRole.Mythics, roles[id_r.mythics] },
			{ PingRole.KSM    , roles[id_r.ksm    ] },
			{ PingRole.Gearing, roles[id_r.gearing] },
			{ PingRole.Events , roles[id_r.events ] },
			{ PingRole.Herald , roles[id_r.herald ] },
		};
		static readonly Dictionary<DiscordRole, PingRole> discordRole_to_pingRole;
		static readonly Dictionary<string, PingRole> dict_pingRoles = new () {
			{ "raid"   , PingRole.Raid },
			{ "raids"  , PingRole.Raid },
			{ "raiding", PingRole.Raid },

			{ "m+"     , PingRole.Mythics },
			{ "mythic+", PingRole.Mythics },
			{ "mythics", PingRole.Mythics },
			{ "keys"   , PingRole.Mythics },
			{ "ksm"    , PingRole.KSM     },
			{ "gearing", PingRole.Gearing },
			{ "gear"   , PingRole.Gearing },

			{ "events", PingRole.Events },
			{ "event" , PingRole.Events },
			{ "herald", PingRole.Herald },
		};
		static readonly Dictionary<PingRole, string> dict_names = new () {
			{ PingRole.Raid   , "Raid"    },
			{ PingRole.Mythics, "M+"      },
			{ PingRole.KSM    , "KSM"     },
			{ PingRole.Gearing, "Gearing" },
			{ PingRole.Events , "Events"  },
			{ PingRole.Herald , "Herald"  },
		};
		static readonly Dictionary<PingRole, string> dict_summaries = new () {
			{ PingRole.Raid   , "Raid announcements." },
			{ PingRole.Mythics, "M+ keys in general." },
			{ PingRole.KSM    , "Higher keys requiring more focus." },
			{ PingRole.Gearing, "Lower keys / M0s to help gear people." },
			{ PingRole.Events , "Social event announcements." },
			{ PingRole.Herald , "Herald of the Titans announcements." },
		};

		const string id_dropdown = "dropdown_roles";
		const string path_intros = @"data/roles_intros.txt";
		const string delim = "=";

		// Force static initializer to run.
		public static void init() { return; }
		static Roles() {
			discordRole_to_pingRole = new Dictionary<DiscordRole, PingRole>();
			foreach (PingRole role in pingRole_to_discordRole.Keys) {
				discordRole_to_pingRole.Add(pingRole_to_discordRole[role], role);
			}
		}

		public static string help() {
			StringWriter text = new ();

			text.WriteLine("`@Irene -roles` Shows you your current roles, and lets you modify them.");
			text.WriteLine("`@Irene -roles-info` Lists available roles and also shows a brief description.");
			text.WriteLine("Any member can view available roles, but you must be at least a Guest to update them.");

			return text.output();
		}

		public static void set(Command cmd) {
			// Make sure user is in the guild (can have roles).
			if (cmd.user is null) {
				log.info("  Cannot set roles for non-guild member.");
				_ = cmd.msg.RespondAsync("Cannot set roles for people who aren't members of the **<Erythro>** server.");
				return;
			}

			// Fetch current roles of the member.
			DiscordMember member = cmd.user;
			List<PingRole> roles_current = new ();
			foreach(DiscordRole role in member.Roles) {
				if (discordRole_to_pingRole.ContainsKey(role)) {
					roles_current.Add(discordRole_to_pingRole[role]);
				}
			}

			// Send message with selection menu.
			log.info("  Sending role selection menu.");
			RoleDropdown dropdown = new (roles_current, member);
			DiscordMessage msg =
				cmd.msg.RespondAsync(dropdown.init_msg()).Result;
			dropdown.msg = msg;
		}

		public static void list(Command cmd) {
			log.info("  Listing available roles.");
			StringWriter text = new ();

			text.WriteLine("*Available roles:*");
			foreach (PingRole role in pingRole_to_discordRole.Keys) {
				string name = pingRole_to_discordRole[role].Name;
				string summary = dict_summaries[role];
				text.WriteLine($"**{name}:** {summary}");
			}
			text.WriteLine("*Use `@Irene -roles` to assign yourself roles.*");

			_ = cmd.msg.RespondAsync(text.output());
		}

		public static void royce(Command cmd) {
			const string rolls_royce = @"https://i.imgur.com/mTEdYN6.jpeg";
			log.info("  Sending Rolls Royce.");
			_ = cmd.msg.RespondAsync(rolls_royce);
		}

		// Assigns the list of roles to the member, and removes any
		// that aren't on the list.
		// Also sends welcome messages for relevant roles.
		static async void assign(DiscordMember member, List<PingRole> roles) {
			// Update member so its associated roles are current.
			member = await member.Guild.GetMemberAsync(member.Id);

			// Initialize comparison sets.
			HashSet<PingRole> roles_prev = new ();
			foreach (DiscordRole role in member.Roles) {
				if (discordRole_to_pingRole.ContainsKey(role)) {
					roles_prev.Add(discordRole_to_pingRole[role]);
				}
			}
			HashSet<PingRole> roles_new = new (roles);

			// Find removed/added roles.
			HashSet<PingRole> roles_removed = new (roles_prev);
			roles_removed.ExceptWith(roles_new);
			HashSet<PingRole> roles_added = new (roles_new);
			roles_added.ExceptWith(roles_prev);

			// Remove/add roles.
			log.info($"  Removing {roles_removed.Count} role(s).");
			foreach (PingRole role in roles_removed) {
				DiscordRole role_discord = pingRole_to_discordRole[role];
				log.debug($"    Removing {role}.");
				_ = member.RevokeRoleAsync(role_discord);
			}
			log.info($"  Adding {roles_added.Count} role(s).");
			foreach (PingRole role in roles_added) {
				DiscordRole role_discord = pingRole_to_discordRole[role];
				log.debug($"    Adding {role}.");
				_ = member.GrantRoleAsync(role_discord);
				string welcome = get_welcome(role);
				_ = member.SendMessageAsync(welcome);
			}
			log.endl();
		}

		// Read through data file to find matching welcome message.
		static string get_welcome(DiscordRole role) {
			return get_welcome(discordRole_to_pingRole[role]);
		}
		static string get_welcome(PingRole role) {
			string content = "";
			StreamReader data = File.OpenText(path_intros);

			while(!data.EndOfStream) {
				string line = data.ReadLine() ?? "";
				if (line.Contains(delim)) {
					string[] split = line.Split(delim, 2);
					if (dict_pingRoles[split[0]] == role) {
						content = split[1];
						break;
					}
				}
			}
			data.Close();

			content = content.unescape();
			content = $"{emojis[id_e.erythro]} {content}";
			return content;
		}
	}
}
