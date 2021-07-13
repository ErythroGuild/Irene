using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;

using static Irene.Program;

namespace Irene.Modules {
	using id_ch = ChannelIDs;

	static class Starboard {
		// Cache variables.
		const int cache_size = 20;
		static readonly Queue<string> cache = new ();
		static readonly Dictionary<string, DiscordMessage> cache_dict = new ();

		// Constants.
		const int emoji_cap = 4;
		const int preview_chars = 400;
		const string str_footer_prefix = "id: ";
		static readonly TimeSpan time_tracked = TimeSpan.FromDays(30);
		static readonly Dictionary<ulong, int> dict_thresholds = new () {
			{ id_ch.general , 4 },
			{ id_ch.sharing , 4 },
			{ id_ch.spoilers, 4 },
			{ id_ch.memes   , 6 },
			{ id_ch.tts     , 4 },
			{ id_ch.bots    , 4 },
			{ id_ch.news    , 4 },
		};
		static readonly List<ulong> ch_spoiler = new () {
			id_ch.spoilers,
		};
		static readonly Dictionary<ulong, DiscordColor> dict_colors = new () {
			{ id_ch.general , new ("#FFCEC9") },
			{ id_ch.sharing , new ("#DA4331") },
			{ id_ch.spoilers, new ("#FFCEC9") },
			{ id_ch.memes   , new ("#3E0600") },
			{ id_ch.tts     , new ("#FFCEC9") },
			{ id_ch.bots    , new ("#3E0600") },
			{ id_ch.news    , new ("#FFCEC9") },
		};

		// Force static initializer to run.
		public static void init() { return; }

		static Starboard() {
			irene.MessageReactionAdded += (irene, e) => {
				_ = Task.Run(async () => {
					// Exit if guild data isn't loaded yet.
					if (!is_guild_loaded) {
						return;
					}

					// Fetch uncached data.
					DiscordChannel channel = e.Message.Channel ??
						await irene.GetChannelAsync(e.Message.ChannelId);
					DiscordMessage msg =
						await channel.GetMessageAsync(e.Message.Id);
					DiscordUser author = msg.Author;

					// Exit if reaction doesn't need to be tracked.
					if (e.User.IsBot || e.User == author) {
						return;
					}
					if (!dict_thresholds.ContainsKey(channel.Id)) {
						return;
					}
					TimeSpan time_posted =
						DateTimeOffset.UtcNow -
						msg.Timestamp.UtcDateTime;
					if (time_posted > time_tracked) {
						return;
					}

					// Populate set of reacting users.
					// Not including bots or the author themselves.
					HashSet<DiscordUser> users = new ();
					Dictionary<DiscordEmoji, int> counts = new ();
					Dictionary<DiscordEmoji, List<DiscordUser>> emojis = new ();
					List<DiscordReaction> reactions = new (msg.Reactions);
					foreach (DiscordReaction reaction in reactions) {
						DiscordEmoji emoji = reaction.Emoji;
						List<DiscordUser> users_i =
						new (await msg.GetReactionsAsync(emoji));
						counts.Add(emoji, users_i.Count);
						foreach (DiscordUser user in users_i) {
							if (!user.IsBot && user != author) {
								users.Add(user);
							}
						}
					}

					// Exit early if not enough people reacted.
					int count = users.Count;
					if (count < dict_thresholds[channel.Id]) {
						return;
					}
					log.info("Popular post detected; checking starboard.");
					log.debug($"  #{channel.Name} - {author.tag()}");

					// Check if message is already pinned.
					bool is_update = false;
					DiscordMessage? pin = null;
					string msg_id = msg.Id.ToString();
					if (cache_dict.ContainsKey(msg_id)) {
						is_update = true;
						pin = cache_dict[msg_id];
					} else {
						pin = fetch_pin(msg_id);
						if (pin is not null) {
							is_update = true;
						}
					}

					// Update the existing pin, or add a new message.
					DiscordEmbed embed = get_embed(msg, counts);
					if (is_update) {
						log.debug("  Updating existing pinned embed.");
						_ = pin!.ModifyAsync(embed);
					} else {
						log.info("  Adding new pinned embed.");
						pin = channels[id_ch.starboard].SendMessageAsync(embed).Result;

						// Update cache.
						cache.Enqueue(msg_id);
						cache_dict.Add(msg_id, pin);
						if (cache.Count > cache_size) {
							string discard = cache.Dequeue();
							cache_dict.Remove(discard);
						}

						// Send message to original author.
						DiscordMember? author_member = author as DiscordMember;
						if (author_member is not null) {
							log.info("  Notifying original message author.");
							StringWriter text = new ();
							text.WriteLine("Congrats! :tada:");
							string m = channels[id_ch.starboard].Mention;
							text.WriteLine($"Your post was exceptionally popular, and has been added to **<Erythro>**'s {m} channel.");
							text.WriteLine(":champagne_glass: :champagne:");
							_ = author_member.SendMessageAsync(text.output());
						}
					}
					log.endl();
				});
				return Task.CompletedTask;
			};
		}

		// Searches through the starboard channel for existing
		// pins with the message already.
		// Returns that message if it exists, or null if it doesn't.
		static DiscordMessage? fetch_pin(string id) {
			DiscordChannel ch = channels[id_ch.starboard];

			// Fetch the most recent message.
			// Needed to have a simple loop to search through.
			List<DiscordMessage> msg_list =
				new (ch.GetMessagesAsync(1).Result);
			if (msg_list.Count == 0) {
				return null;
			}

			// Set the time at which to give up searching.
			DateTimeOffset time_untracked =
				DateTimeOffset.UtcNow - time_tracked;

			// Search through existing messages.
			while (msg_list.Count > 0) {
				foreach (DiscordMessage msg in msg_list) {
					List<DiscordEmbed> embeds = new (msg.Embeds);
					if (embeds.Count == 0) {
						continue;
					}
					DiscordEmbed embed = embeds[0];
					if (embed.Footer is null) {
						continue;
					}
					string? footer_text = embed.Footer.Text;
					if (footer_text?.StartsWith(str_footer_prefix) ?? false) {
						string id_pin = footer_text.Replace(str_footer_prefix, "");
						if (id_pin == id) {
							return msg;
						}
					}

					if (msg.Timestamp < time_untracked) {
						return null;
					}
				}

				DiscordMessage msg_last = msg_list[^1];
				ulong id_last = msg_last.Id;
				msg_list =
					new (ch.GetMessagesBeforeAsync(id_last).Result);
			}

			// Could not find matching message.
			return null;
		}

		// Returns the message... embedded... into the embed.
		static DiscordEmbed get_embed(DiscordMessage msg, Dictionary<DiscordEmoji, int> reacts) {
			// Fetch author name.
			string author_name;
			if (msg.Channel is not null && !msg.Channel.IsPrivate) {
				// Check for webhook.
				if (msg.Author.IsBot && msg.Author.Discriminator == "0000") {
					author_name = msg.Author.Username;
				} else {
					DiscordMember author = (DiscordMember)msg.Author;
					author_name = author.DisplayName;
				}
			} else {
				DiscordUser author = msg.Author;
				author_name = $"{author.tag()}";
			}

			// Get content strings.
			string text;
			string? content = get_content(msg);
			string emojis = get_emoji_list(reacts);
			if (content is not null) {
				if (ch_spoiler.Contains(msg.ChannelId)) {
					text = $"||{content}||";
				} else {
					text = content;
				}
				text = $"{text}\n{emojis}";
			} else {
				text = emojis;
			}

			// Create the embed object.
			DiscordEmbedBuilder embed =
				new DiscordEmbedBuilder()
				.WithAuthor(author_name, null, msg.Author.AvatarUrl)
				.WithTitle($"\u21D2 #{msg.Channel?.Name}")
				.WithUrl(msg.JumpLink)
				.WithColor(dict_colors[msg.ChannelId])
				.WithDescription(text)
				.WithFooter(str_footer_prefix + msg.Id.ToString());

			// Add thumbnail if applicable.
			string? thumbnail = get_thumbnail(msg);
			if (ch_spoiler.Contains(msg.ChannelId)) {
				thumbnail = null;
			}
			if (thumbnail is not null) {
				embed = embed.WithThumbnail(thumbnail);
			}

			return embed.Build();
		}

		// Returns a thumbnail of the crossposted message.
		// Returns null if there is nothing to preview.
		static string? get_thumbnail(DiscordMessage msg) {
			// Only fetch thumbnails for regular messages.
			switch (msg.MessageType) {
			case MessageType.Default:
			case MessageType.Reply:
				break;
			default:
				return null;
			}

			// Return early if no thumbnail content exists.
			List<DiscordAttachment> files = new (msg.Attachments);
			List<DiscordEmbed> embeds = new (msg.Embeds);
			if (files.Count == 0 && embeds.Count == 0) {
				return null;
			}

			// Return image thumbnail if exists.
			foreach (DiscordAttachment file in files) {
				if (file.MediaType.StartsWith("image")) {
					return file.Url;
				}
			}

			// Return embed thumbnail if exists.
			if (embeds.Count > 0) {
				if (embeds[0].Image is not null) {
					return embeds[0].Image.Url.ToString();
				}
				if (embeds[0].Thumbnail is not null) {
					return embeds[0].Thumbnail.Url.ToString();
				}
			}

			return null;
		}

		// Returns a string representation of the crossposted message.
		// Returns null if the content is blank.
		static string? get_content(DiscordMessage msg) {
			// Return generic messages for specific message types.
			// Filter unsupported message types.
			switch (msg.MessageType) {
			case MessageType.Default:
			case MessageType.Reply:
				break;
			case MessageType.ChannelPinnedMessage:
				return "pinned message to the channel";
			case MessageType.GuildMemberJoin:
				return "joined the server";
			case MessageType.UserPremiumGuildSubscription:
				return "boosted the server";
			case MessageType.TierOneUserPremiumGuildSubscription:
				return "server boost level 1";
			case MessageType.TierTwoUserPremiumGuildSubscription:
				return "server boost level 2";
			case MessageType.TierThreeUserPremiumGuildSubscription:
				return "server boost level 3";
			default:
				return null;
			}

			// Return trimmed message content if available.
			if (msg.Content.Trim() != "") {
				string preview = msg.Content.Trim();
				if (preview.Length < preview_chars) {
					return preview;
				} else {
					return preview[..preview_chars] + " [...]";
				}
			}

			// Return embed summary if available.
			List<DiscordEmbed> embeds = new (msg.Embeds);
			if (embeds.Count == 0) {
				return null;
			}
			string title = embeds[0].Title.Trim();
			if (title != "") {
				return title;
			}
			string description = embeds[0].Description.Trim();
			if (description != "") {
				if (description.Length < preview_chars) {
					return description;
				} else {
					return description[..preview_chars] + " [...]";
				}
			}

			// Return null if no summary could be created.
			return null;
		}

		// Returns a formatting string describing the emojis.
		static string get_emoji_list(Dictionary<DiscordEmoji, int> list) {
			const string
				nbsp      = "\u00A0",
				separator = "\u2003",
				ellipsis  = "\u2026";

			// Sort list of emojis.
			List<DiscordEmoji> emojis = new (list.Keys);
			emojis.Sort(delegate (DiscordEmoji x, DiscordEmoji y) {
				return list[y] - list[x];
			});
			bool is_elided = (emojis.Count > emoji_cap);

			// Format as string.
			StringWriter text = new ();
			foreach (DiscordEmoji emoji in emojis) {
				text.Write($"{emoji}{nbsp}**{list[emoji]}**{separator}");
			}
			if (is_elided) {
				text.Write($"{ellipsis}");
			}

			// Cleanup result and return.
			string output = text.output();
			if (output.EndsWith(separator)) {
				output = output[..^separator.Length];
			}
			return output;
		}
	}
}
