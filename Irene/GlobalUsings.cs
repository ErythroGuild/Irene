// System.
global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.IO;
global using System.Threading.Tasks;

// D#+.
global using DSharpPlus;
global using DSharpPlus.Entities;
global using DSharpPlus.EventArgs;
global using DSharpPlus.Exceptions;

// Serilog.
global using Serilog;

// Project namespaces.
global using Irene.Utils;

// Project static variables.
global using static Irene.Const;
global using static Irene.Program;

// Type aliases.
global using id_ch = Irene.Const.ChannelIDs;
global using id_e  = Irene.Const.EmojiIDs;
global using id_r  = Irene.Const.RoleIDs;
global using id_vc = Irene.Const.VoiceChatIDs;

global using AccessLevel = Irene.Commands.Rank.Level;
global using MessagePromise = System.Threading.Tasks.TaskCompletionSource<DSharpPlus.Entities.DiscordMessage>;
global using CommandOption = DSharpPlus.Entities.DiscordApplicationCommandOption;
global using CommandOptionEnum = DSharpPlus.Entities.DiscordApplicationCommandOptionChoice;
global using InteractionHandler = System.Func<DSharpPlus.Entities.DiscordInteraction, System.Diagnostics.Stopwatch, System.Threading.Tasks.Task>;
