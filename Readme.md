# Irene

![Discord][1] ![Latest release][2] ![Commit activity][3] ![License][4]

**Irene** is the server admin bot for **\<Erythro\>**. It has a wide variety
of features, and is in constant development.

*Related Discord bots:*
**[Polybius][8]**,
**[Puck][9]**,
**[Wowhead Digest][10]**,
**[Blizz CS][11]**

## Bot Commands

A frequently-updated list of commands can always be accessed via the `/help`
command of the bot. The following list may not always include the most
recently added commands.

* **Setup**
	* `/help` displays help for a command, or lists all commands.
	* `/about` displays stats and info about the current **Irene** build.
	* `/invite` gets the invite link for the guild server.
	* `/class-discord` gets invite links for class discords.
	* `/rank` manages a user's rank and guilds.
	* `/roles` manages a user's own roles (pings and guilds).
* **Roster**
	* `/raid` displays and manages raid info.
* **Data**
	* `/tag` displays and manages copy/pasted texts.
	* `/farm` displays farming routes for mats.
	* `/cap` displays the current caps for timegated resources.
	* `/wow-token` displays the current token price.
* **Tools**
	* `/solve` solves various in-game puzzles.
	* `/translate` translates text.
* **Moderation**
	* `/best-of` manages messages on the starboard.
	* `/slowmode` manages slowmode for channels.
* **Fun**
	* `/roll` chooses random numbers.
	* `/random` contains various randomization utilities.
	* `/mimic` randomizes text as in-game languages.
	* `/irene-status` modifies **Irene**'s Discord status.

## Repo Structure

#### **`Commands/`**
This directory contains definitions for all Discord application commands.
(If a command has related context menu commands, the classes are included
in the same file.) This is as thin a  wrapper around the implementation
as possible, as **D#+** cannot be easily mocked for testing. Implementation
is always in a corresponding class inside `Irene.Modules`.

#### **`Modules/`**
This directory contains different "modules" of functionality which **Irene**
provides. This includes implementations for the commands found in `Commands/`,
but also other functionality (e.g. `RecurringEvents`) which don't directly
interface with commands.

#### **`Interactables/`**
This directory contains classes that use Discord message components to
implement common functionality. Instantiating these classes is often a
two-stage process, as the `Interactable` itself often holds a reference
to its containing message. This is passed in as a future, and then later
set when the containing message has been created.

#### **`Libs/`**
This directory contains library modules which are completely independent
of **Irene**. These libraries are designed with interfaces allowing them
to simply be dropped in to other projects which require their functionality.

#### **`Utils/`**
This directory contains (loosely categorized) standalone utility methods,
many of which are extension methods, and therefore require their own static
classes.

#### **`config/`**
This directory contains settings files, including many API tokens/secrets
which are not synced with the remote repo (excluded through `.gitignore`).
These files must be created and populated with the corresponding secure
data, which must be obtained from their original sources.

#### **`data/`**
This directory contains (text-based) data for other modules to use. Some
of the files in here contain save data for various modules, and is periodically
copied back from the running bot and updated as part of the repo. Some
of the files are read and cached on program start, and require a restart
of the bot before any changes are reflected.

[1]: https://img.shields.io/discord/317723973968461824?label=%3CErythro%3E&logo=discord&logoColor=fff&style=flat-square
[2]: https://img.shields.io/github/v/release/ErythroGuild/irene?style=flat-square
[3]: https://img.shields.io/github/commit-activity/m/ErythroGuild/irene?style=flat-square
[4]: https://img.shields.io/github/license/ErythroGuild/irene?style=flat-square
[8]: https://github.com/ErythroGuild/polybius
[9]: https://github.com/ErythroGuild/puck
[10]: https://github.com/ErythroGuild/wowhead-digest
[11]: https://github.com/ErythroGuild/BlizzCS
