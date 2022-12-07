// This warning (obviously) needs to be disabled for global usings,
// since it is illegal to place them inside a namespace.
#pragma warning disable IDE0065 // Misplaced using directive

// System.
global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.IO;
global using System.Text;
global using System.Threading.Tasks;

// Libraries.
global using DSharpPlus;
global using DSharpPlus.Entities;
global using Microsoft.Extensions.Logging;
global using Serilog;

// Project namespaces.
global using Irene.Exceptions;
global using Irene.Utils;

// Project static variables.
global using static Irene.Const;
global using static Irene.Program;

// Type aliases - system types.
global using Promise = System.Threading.Tasks.TaskCompletionSource;
global using MessagePromise = System.Threading.Tasks.TaskCompletionSource<DSharpPlus.Entities.DiscordMessage>;

// Type aliases - D#+ types.
global using CommandType = DSharpPlus.ApplicationCommandType;
global using ArgType = DSharpPlus.ApplicationCommandOptionType;
global using DiscordCommand = DSharpPlus.Entities.DiscordApplicationCommand;
global using DiscordCommandOption = DSharpPlus.Entities.DiscordApplicationCommandOption;
global using DiscordCommandOptionEnum = DSharpPlus.Entities.DiscordApplicationCommandOptionChoice;
global using DiscordComponentRow = DSharpPlus.Entities.DiscordActionRowComponent;
global using DiscordButton = DSharpPlus.Entities.DiscordButtonComponent;
global using DiscordSelect = DSharpPlus.Entities.DiscordSelectComponent;
global using DiscordSelectOption = DSharpPlus.Entities.DiscordSelectComponentOption;
global using DiscordTextInput = DSharpPlus.Entities.TextInputComponent;

// Type aliases - object IDs.
global using id_ch = Irene.Const.Id.Channel;
global using id_e  = Irene.Const.Id.Emoji;
global using id_g  = Irene.Const.Id.Guild;
global using id_r  = Irene.Const.Id.Role;
global using id_u  = Irene.Const.Id.User;

// Type aliases - project types.
global using AccessLevel = Irene.Modules.Rank.AccessLevel;
global using ParsedArgs = System.Collections.Generic.IDictionary<string, object>;
global using Responder = System.Func<Irene.Interaction, System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;
global using Autocompleter = System.Func<Irene.Interaction, object, System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;
global using AutocompleterTable = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Irene.Interaction, object, System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>>;

#pragma warning restore IDE0065 // Misplaced using directive
