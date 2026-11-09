# 07 - Custom Commands

Scope: `/Commands` (Learn, Forget, History, Revert, Stats, Leaderboard), `/Command`, and `TextCustomCommandProcessor`
(the `!name` text trigger). Also the two autocomplete providers the commands share.

Source: `Grimoire/Features/CustomCommands/`, `Grimoire/DatabaseQueryHelpers/CustomCommandDatabaseQueryHelpers.cs`,
`Grimoire.Domain/CustomCommand.cs`.

Do the standard preflight from `00-environment.md` first. Also:

- `/GeneralSettings ModLogChannel` set to `#modlog`. Learn, Forget and Revert post there.
- Give `user1` a spare "Perm" role you can add and remove, for the restricted-command cases.
- Tables: `CustomCommands` (one row per version), `CustomCommandRoles` (allow or deny roles, keyed to one version) and
  `CustomCommandUsages` (one row per use). Names assume EF defaults; adjust to your schema.

## Behaviour summary (what the code does)

- A command is a list of versions. `Learn` on an existing name adds a new version; it never edits one. The newest
  version is the one that runs. Each version carries its own content, embed flag and colour, and roles.
- Content can hold `%Mention` and `%Message` (any case). `\n` in content becomes a newline. Nothing else is unescaped.
- Names are 1 to 24 characters with no whitespace, trimmed. They are matched exactly, so case matters.
- Restricted commands run only for members with one of the allow roles. Deny roles exist in the model, but nothing in
  the bot can create them (see Code observations).
- A use is recorded in `CustomCommandUsages` only after the reply is sent.
- Pings: neither path sets allowed mentions, and DSharpPlus sends `"allowed_mentions":{"parse":[]}`, so mentions in a
  command's output render but never ping (review finding C-3). `@everyone`, `@here` and `<@&guildId>` in `%Message` are
  also rewritten to `@ everyone` / `@ here`.

| Command | User gate | Reply | Mod log |
|---------|-----------|-------|---------|
| `/Commands Learn` | Manage Guild | public embed; errors ephemeral | yes |
| `/Commands Forget` | Manage Guild | public embed | yes, unless nothing was removed |
| `/Commands History` | Manage Guild | public embed, paged | no |
| `/Commands Revert` | Manage Guild | public embed; errors ephemeral | yes |
| `/Commands Stats` | none | public embed | no |
| `/Commands Leaderboard` | none | public embed | no |
| `/Command` | none (role list if restricted) | the command's content | no |
| `!name` text trigger | none (role list if restricted) | a message in the channel | no |

All slash commands are `RequireGuild` and `RequireModuleEnabled(Commands)` and defer first. A failed check shows as a
yellow embed. Owner and Administrator pass the Manage Guild gate. Error and warning replies are ephemeral (KI-21).

---

## A. `/Commands Learn name content [embed] [embed_color] [restricted_use] [permission_role_1..10]`

Gates: `RequireGuild`, `RequireModuleEnabled(Commands)`, Manage Guild, checked by the bot. `name` is a plain string with
a client-side length of 1 to 24. `content` has a client-side maximum of 2000. `embed_color` is 6 hex digits, with or
without `#`, and is stored upper case. It is refused without `embed:true`. Roles count only with `restricted_use:true`.
The reply is "Added {name} custom command." and the mod log gets "@user asked @Grimoire to learn a new command: {name}".

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-01 | none | `admin` runs `/Commands Learn name:hello content:"Hello there!"` | Green "Added hello custom command." **[auto]** `Learn_SavesATextCommand_AndLogsIt` | One `CustomCommands` row, text type, moderator = admin, no roles. `#modlog` purple "@admin asked @Grimoire to learn a new command: hello" |
| CC-02 | none | `name:rules content:"Be nice" embed:true embed_color:ff00aa` | Same reply. **[auto]** `Learn_WithEmbedAndColor_SavesAnEmbedCommand` | Embed row, colour `FF00AA` |
| CC-03 | none | `embed:true` with no colour | Same reply | Embed row, colour null. **[auto]** `LearnCommandTests.LearnEmbedCommand_WithoutColor_Saved_EmbedColorIsNull` |
| CC-04 | none | `embed:true embed_color:"#00FF00"` | Same reply | Colour `00FF00` (the `#` is dropped) |
| CC-05 | none | `embed_color:ff0000` with `embed` false or left out | Red "An embed color only applies to an embed. Set embed to true, or leave out the color." **[auto]** `Learn_WithABadNameOrColour_SaysWhatIsWrong` | No row |
| CC-06 | none | `embed:true embed_color:not-a-colour` (also `fff`, `gggggg`) | Red "A command embed color must be a 6 character hex color code." **[auto]** same test | No row |
| CC-07 | none | `name:secret content:shh restricted_use:true permission_role_1:Kicker permission_role_2:Banner` | Same reply. **[auto]** `Learn_Restricted_SavesItsRoles` | Two allow rows in `CustomCommandRoles` for that version |
| CC-08 | none | `restricted_use:true` with no roles | Red "A restricted command must have at least one permission role." **[auto]** `Learn_RestrictedWithoutRoles_IsRejected` | No row |
| CC-09 | none | `permission_role_1:Perm` with `restricted_use` false | **Expected:** the role becomes a deny role (decided in review C-2). **Actual:** "Added ... custom command." and the role is dropped, so everyone can use it | Row with no roles |
| CC-10 | none | `restricted_use:true` with the same role in slots 1 and 3 | Same reply | One role row (duplicates removed) |
| CC-11 | `hello` exists | `/Commands Learn name:hello content:"second\nline"` | "Added hello custom command." (it says Added, not Updated). **[auto]** `Learn_AnExistingName_AddsAVersion_ThatHistoryShows` | A second row; the first is kept. Content holds a real newline |
| CC-12 | `secret` from CC-07 | Learn `secret` again with no `restricted_use` | Same reply | New version has no roles: the command is now open to all. Each version carries its own roles |
| CC-13 | none | `name:"two words"` | Red "A command name cannot contain spaces." **[auto]** `Learn_WithABadNameOrColour_SaysWhatIsWrong` | No row |
| CC-14 | none | A 25-character name | Discord's client refuses it. **[auto]** `Learn_RegistersTheNameLength` | No row |
| CC-15 | none | `name:"   "` (spaces only) | Red "A value for command name must be provided." | No row |
| CC-16 | none | `content:"   "` and `content:"\n"` | Red "Command content cannot be empty." | No row |
| CC-17 | none | Content of 2001 characters | Discord's client refuses it (max 2000) | No row |
| CC-18 | none | `content:"C:\temp\files"` | Added | Stored as typed; only `\n` is unescaped (review C-5, fixed) |
| CC-19 | none | `name:Hello`, then `name:hello` | Both added | Two separate commands. Names are case-sensitive |
| CC-20 | none | `name:"!hello"` | Added | Stored as `!hello`. Its text trigger is `!!hello` |
| CC-21 | none | `admin` learns `content:"@everyone look"`, then runs it (CC-70, CC-90) | The text shows but pings nobody (parse: []) | none |
| CC-22 | none | `mod` (no Manage Guild) runs Learn | Yellow "You do not have the required permissions to use this command." **[auto]** `Learn_NeedsManageGuildAndTheModule` | No row |
| CC-23 | none | `owner`, and a member whose only grant is Administrator, run Learn | Both allowed (bypass) | Rows |
| CC-24 | Commands module disabled | `admin` runs Learn | Yellow "This module is disabled in this server." **[auto]** `Learn_NeedsManageGuildAndTheModule` | No row |
| CC-25 | Mod log not set | Learn | Reply as CC-01 | Row; nothing posted to any log |
| CC-26 | Optional: stop the database mid-session | Learn | Generic "Encountered exception while executing commands learn [ID ...]" and an alert. The friendly "Could not save that command right now due to a database error. Please try again." only shows for non-transient update errors (review C-6) | No row |

Automated in `Grimoire.Tests/Integration/CustomCommands/CustomCommandSettingsTests.cs`, with saving also covered by
`Grimoire.Tests/Features/CustomCommands/LearnCommandTests.cs`.

## B. `/Commands Forget name`

Gates: `RequireGuild`, `RequireModuleEnabled(Commands)`, Manage Guild, checked by the bot. `name` uses the name converter,
so a name that fails validation is a parse error, not a reply from the command. It deletes every version, their roles
(by cascade) and all usage rows. It replies and logs only when something was removed.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-30 | `hello` has two versions and one use | `admin` runs `/Commands Forget name:hello` | Green "Removed command hello". **[auto]** `Forget_DeletesEveryVersionAndItsUsage` | No `CustomCommands` or `CustomCommandUsages` rows for `hello`. `#modlog` "@admin asked @Grimoire to forget command `hello`." **[auto]** `Forget_IsLoggedWithTheBotsMention` (KI-47, fixed) |
| CC-31 | none | `/Commands Forget name:nope` | Green "Command `nope` was already forgotten." **[auto]** `Forget_AnUnknownCommand_SaysItWasAlreadyForgotten` | No mod-log post. **[auto]** `Forget_AnUnknownCommand_LogsNothing` |
| CC-32 | `secret` restricted (CC-07) | Forget `secret` | "Removed command secret" | No `CustomCommandRoles` rows left for `secret` |
| CC-33 | `hello` and `bye` exist | Forget `hello` | Removed | `bye` and its usage untouched. **[auto]** `ForgetCommandTests.ForgetCommand_DoesNotAffectOtherCommands` |
| CC-34 | CC-30 done | Type `!hello`, open `/Command` autocomplete | Nothing posted; `hello` is not offered | none |
| CC-35 | none | Type `name:"two words"` by hand | Yellow parse error from DSharpPlus (record the text) | none |
| CC-36 | none | `mod` runs Forget | "You do not have the required permissions to use this command." | Nothing deleted |
| CC-37 | Commands module disabled | Forget | "This module is disabled in this server." | Nothing deleted |

Automated in `CustomCommandSettingsTests.cs` and `Grimoire.Tests/Features/CustomCommands/ForgetCommandTests.cs`.

## C. `/Commands History name`

Gates: `RequireGuild`, `RequireModuleEnabled(Commands)`, Manage Guild. Newest first. Each entry is
`**vN (current)** — <t:...:f> by @mod` then `> preview`. Newlines and tabs in the preview show as `\n` and `\t`; previews
over 100 characters are cut with "…". The embed title is "Version history for {name}". Pages split at about 2048
characters; later pages are follow-ups.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-40 | `hello` has v1 "first" and v2 "second\nline" | `admin` runs `/Commands History name:hello` | "**v2 (current)** — <time> by @admin" / `> second\nline` / "**v1** — ..." / `> first`. **[auto]** `Learn_AnExistingName_AddsAVersion_ThatHistoryShows` | none |
| CC-41 | none | `name:nope` | Yellow "No command named `nope` exists." **[auto]** `History_OfAnUnknownCommand_SaysSo` | none |
| CC-42 | A version with 150 characters of content | History | Preview is the first 100 characters then "…" | none |
| CC-43 | About 30 versions with long content | History | Several pages, all visible (follow-ups, see KI-30). Version numbers run from 30 down to 1 | none |
| CC-44 | A legacy row with no moderator | History | That entry has no " by ..." part | none |
| CC-45 | Same name in Test-B | History in Test-A | Only Test-A versions. **[auto]** `HistoryCommandTests.History_SameNameDifferentGuild_GuildIsolated` | none |
| CC-46 | none | `mod` runs History; module disabled | Permission error; "This module is disabled in this server." | none |

Automated in `CustomCommandSettingsTests.cs` and `Grimoire.Tests/Features/CustomCommands/HistoryCommandTests.cs`.

## D. `/Commands Revert name version`

Gates: `RequireGuild`, `RequireModuleEnabled(Commands)`, Manage Guild. `version` is a string. Its autocomplete
(`GetCommandVersionOptions`) offers the newest 25 versions as "v{N} — {MMM d, yyyy h:mm tt} UTC by {display name}",
numbered from the full count, with the version's `CreatedAt` ticks as the value. Revert copies the chosen version
(content, embed and colour, allow and deny roles) into a new current version.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-50 | `hello` v1 "first" (restricted to Kicker), v2 "second" | `admin` runs `/Commands Revert name:hello`, picks v1 | Green "Reverted `hello` to version from <t:...:f>." **[auto]** `Revert_SavesTheOldVersionAsTheNewest` | Third row: content "first", moderator admin, allow role Kicker. `#modlog` "@admin reverted command `hello` to version from <t:...:f>." |
| CC-51 | `hello` has 3 versions | Open the `version` autocomplete | Three choices, newest first, labelled v3, v2, v1 with date and moderator name (or id if not cached) | none |
| CC-52 | 30 versions | Open the autocomplete | 25 choices, v30 down to v6, same numbers as History (review C-8, fixed; the harness can't drive autocomplete) | none |
| CC-53 | none | Type `version:abc` | Yellow "Invalid version selected. Use the autocomplete to pick a version." **[auto]** `Revert_ToABadVersion_IsRejected` | No new row |
| CC-54 | none | Type `version:12345` | Yellow "Version not found for command `hello`." **[auto]** same test | No new row |
| CC-55 | none | Type `version:9000000000000000000` or `version:-1` | **Expected:** "Version not found for command `hello`." **Actual:** the ticks are outside `DateTimeOffset`'s range, the constructor throws, and the generic "Encountered exception while executing commands revert" shows | No new row |
| CC-56 | `bye` exists | Revert `bye` with a version value copied from `hello` | "Version not found for command `bye`." | none |
| CC-57 | `rules` v1 embed `FF0000`, v2 plain text | Revert to v1, run `/Command rules` | Embed with the red colour again | New embed row |
| CC-58 | none | Revert to the current version | Reverted; History shows a new version with the same content | Extra row |
| CC-59 | `nope` doesn't exist | `name:nope version:<any number>` | "Version not found for command `nope`." | none |
| CC-60 | none | `mod` runs Revert; module disabled | Permission error; "This module is disabled in this server." | none |

Automated in `CustomCommandSettingsTests.cs`. The autocomplete cases (CC-51, CC-52) are manual only.

## E. `/Commands Stats name`

Gates: `RequireGuild`, `RequireModuleEnabled(Commands)`. No permission gate: any member can run it. The embed's author
is "Stats for !{name}" with inline fields Total Uses, Last 30 Days, Last Year, Last Used (`<t:...:R>` or "Never") and a
Top Users field with the top 3: "**1.** @user — N uses", or "No usage data yet."

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-65 | `hello` used by `user1` today, yesterday and 40 days ago, by `mod` 400 days ago | `user1` runs `/Commands Stats name:hello` | Total 4, Last 30 Days 2, Last Year 3, Last Used relative time, Top Users user1 (3) then mod (1). **[auto]** `Stats_CountsUsesByPeriod_AndListsTopUsers` | none |
| CC-66 | `hello` never used | Stats | Zeros, "Never", "No usage data yet." **[auto]** `Stats_ForAnUnusedCommand_ShowsZeros` | none |
| CC-67 | none | `name:nope` | Yellow "No command named `nope` exists." **[auto]** `Stats_ForAnUnknownCommand_SaysSo` | none |
| CC-68 | `secret` restricted to a role `user1` lacks | `user1` runs Stats `secret` | Stats are shown. Restricted commands are not hidden here | none |
| CC-69 | Commands module disabled | Stats | "This module is disabled in this server." | none |

Automated in `CustomCommandSettingsTests.cs` and `Grimoire.Tests/Features/CustomCommands/StatsCommandTests.cs`.

## F. `/Commands Leaderboard [name]`

Gates: `RequireGuild`, `RequireModuleEnabled(Commands)`. No permission gate. Without a name it ranks commands
("Command Leaderboard", rows "**1.** `!a` — 3 uses", footer "{commands} commands, {uses} uses"). With a name it ranks
users ("Leaderboard for !{name}", footer "{uses} total uses"). Both show 15 rows; the footers count everything.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-75 | `a` used 3 times, `b` once | `/Commands Leaderboard` | "**1.** `!a` — 3 uses" / "**2.** `!b` — 1 uses", footer "2 commands, 4 uses". **[auto]** `Leaderboard_RanksCommands_OrUsersOfOneCommand` | none |
| CC-76 | As CC-75 | `name:a` | User rows, footer "3 total uses". **[auto]** same test | none |
| CC-77 | No usage at all | `/Commands Leaderboard` | Yellow "No commands have been used yet." **[auto]** `Leaderboard_WithNoUsage_SaysSo` | none |
| CC-78 | none | `name:nope` | Yellow "No command named `nope` exists." **[auto]** `Leaderboard_ForAnUnknownCommand_SaysItDoesNotExist` | none |
| CC-79 | `hello` exists, never used | `name:hello` | Yellow "Command `!hello` has not been used yet." | none |
| CC-80 | 21 used commands; one with 20 users | Overall, then that name | 15 rows each; footers count all 21 commands and all 20 users' uses. **[auto]** `Leaderboard_Footers_CountPastTheFifteenShown` | none |
| CC-81 | Usage in Test-B | Leaderboard in Test-A | Only Test-A usage. **[auto]** `LeaderboardCommandTests.Overall_OtherGuildUsages_NotIncluded` | none |
| CC-82 | Commands module disabled | Leaderboard | "This module is disabled in this server." | none |

Automated in `CustomCommandSettingsTests.cs` and `Grimoire.Tests/Features/CustomCommands/LeaderboardCommandTests.cs`.

## G. `/Command command_name [mention] [message]`

Gates: `RequireGuild`, `RequireModuleEnabled(Commands)`. Anyone can run it. `command_name` uses the name converter and the
autocomplete below. `mention` takes a user or role. The bot defers publicly, then edits the response with the content.
For an unknown name, a member outside the role list, or content that renders empty, it deletes the response and says
nothing. Text content is cut to 2000 characters and embed content to 4096, ending in "…".

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-85 | `hello` = "Hello there!" | `user1` runs `/Command command_name:hello` | Public "Hello there!" **[auto]** `CustomCommandUseTests.SlashCommand_RepliesWithTheContent_AndRecordsTheUse` | One usage row (user1, hello) |
| CC-86 | `rules` embed with `FF0000` | `/Command command_name:rules` | Embed, description "Be nice", red. **[auto]** `SlashCommand_AnEmbedCommand_RepliesWithAnEmbed` | Usage row |
| CC-87 | Embed command with no colour | Run it | Embed with Discord's default colour | Usage row |
| CC-88 | `hug` = "%Mention gets a hug: %Message" | `mention:@mod message:"hi @everyone"` | "@mod gets a hug: hi @ everyone"; nobody is pinged. **[auto]** `SlashCommand_FillsMentionAndMessage_AndDefusesEveryone` | Usage row |
| CC-89 | As CC-88 | `mention:` a role, then the @everyone role | The role's mention (no ping); for @everyone the text "@ everyone" | Usage rows |
| CC-90 | As CC-88 | No mention or message | " gets a hug: " (placeholders removed) | Usage row |
| CC-91 | As CC-88 | `message:"<@&modRoleId> and @here"` | The role mention is kept, `@here` becomes "@ here"; no ping (review C-3) | Usage row |
| CC-92 | `secret` restricted to Perm | `user1` with Perm, then without | With: "shh". Without: the "thinking" response appears, then is deleted. **[auto]** `SlashCommand_Restricted_OnlyRunsForItsRoles` | One usage row, only for the allowed run |
| CC-93 | none | `command_name:nope` | Response deleted, no message. **[auto]** `SlashCommand_ThatDoesNotExist_DeletesTheResponse` | No usage row |
| CC-94 | `say` = "%Message" | `/Command command_name:say` with no message | Response deleted. **[auto]** `SlashCommand_ThatRendersEmpty_DeletesTheResponse_AndRecordsNoUse` | No usage row |
| CC-95 | Text command of about 1990 characters with `%Message` | Run with a 100-character message | Cut to 2000 characters ending "…" (embed: 4096). Unit tested in `GetCustomCommandTests.TruncateForDiscord_*` | Usage row |
| CC-96 | none | Type `command_name:"two words"` | Yellow parse error from DSharpPlus (record the text) | none |
| CC-97 | Commands module disabled | `/Command command_name:hello` | Yellow "This module is disabled in this server." | No usage row |
| CC-98 | In `#private`, which the bot can't view or send in | `admin` runs `/Command command_name:hello` | Interaction replies still work there: "Hello there!" (record if Discord refuses) | Usage row |
| CC-99 | `hello` only in Test-B | `/Command command_name:hello` in Test-A | Response deleted (A-35, `MultiGuildTests`) | none |

Automated in `Grimoire.Tests/Integration/CustomCommands/CustomCommandUseTests.cs`. The substitution helpers are unit
tested in `Grimoire.Tests/Features/CustomCommands/GetCustomCommandTests.cs`.

## H. Autocomplete for command names

`GetCustomCommandOptions`, used by `/Command`, Forget, History, Revert, Stats and Leaderboard. It offers up to 5 commands
(newest versions only), sorted by name when nothing is typed, or by Levenshtein distance (lower-cased) to the first typed
word, cut to 24 characters. A label gets " <Mention>" and/or " <Message>" when the content has those placeholders. The
harness can't drive autocomplete, so these are manual; the query itself is covered by
`Grimoire.Tests/Features/CustomCommands/AutocompleteQueryTests.cs`.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-100 | 7 commands, `hug` uses both placeholders | Open `/Command` autocomplete with nothing typed | First 5 by name; `hug` shows as "hug <Mention> <Message>" | none |
| CC-101 | As CC-100 | Type `hgu` | `hug` near the top | none |
| CC-102 | none | Type a 300-character word | Choices still appear, no error (review C-8, fixed) | No `PostgresException` in the console |
| CC-103 | `secret` restricted to a role `user1` lacks | `user1` opens the autocomplete | `secret` is offered, though running it does nothing | none |
| CC-104 | Commands in Test-B only | Autocomplete in Test-A | None of Test-B's commands | none |

## I. Text trigger (`TextCustomCommandProcessor`)

Runs on every message create. It handles only Default and Reply messages from a non-bot member, starting with `!`.
The text after `!` is split on spaces (repeated spaces are dropped); the first word is the name. If the command holds
`%Mention` and the second word is a user or role mention that resolves, it fills `%Mention` and the rest fills
`%Message`; otherwise every word after the name is the message. Checks the Commands module (cached settings), then the
role list. A miss posts nothing and logs at Information "Custom command {name} not found or user not authorized". If
the bot can't post (403), it reports `CannotSendInChannel` to the mod log and records no use.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| CC-110 | `hello` exists | `user1` sends `!hello` in `#general` | Bot posts "Hello there!" **[auto]** `TextTrigger_PostsTheContent_AndRecordsTheUse` | Usage row |
| CC-111 | `hug` as CC-88 | `!hug @mod you are great` | "@mod gets a hug: you are great"; no ping. **[auto]** `TextTrigger_FillsMentionFromTheFirstWord_AndMessageFromTheRest` | Usage row |
| CC-112 | As CC-111 | `!hug hello world` | " gets a hug: hello world" (no word lost, review C-8). **[auto]** `TextTrigger_WithoutAMention_KeepsTheWholeMessage` | Usage row |
| CC-113 | As CC-111 | `!hug <@&modRoleId> hi`, then `!hug @everyone hi` | Role mention text (no ping); "@ everyone gets a hug: hi" | Usage rows |
| CC-114 | As CC-111 | `!hug <@1> hi` (a user id that doesn't exist) | " gets a hug: <@1> hi" (the mention didn't resolve, so it is part of the message) | Usage row |
| CC-115 | `rules` embed | `!rules` | Embed post. A legacy row with a blank colour still runs. **[auto]** `ALegacyCommandWithABlankColour_StillRuns` | Usage row |
| CC-116 | none | `!nope`; a bot posts `!hello`; `user1` without Perm sends `!secret`; module disabled then `!hello` | Nothing posted in each. **[auto]** `TextTriggers_ThatShouldNotRun_PostNothing` | No usage rows. Console: Information "not found or user not authorized" for the unknown and restricted cases; Debug "Ignored text command because the Commands module is disabled" |
| CC-117 | Bot can view but not send in `#private` | `user1` sends `!hello` there | Nothing posted. `#modlog` setup problem "I could not post in #private." with "Give my role **View Channel** and **Send Messages** in that channel, and check its permission overwrites." **[auto]** `TextTrigger_WhereTheBotCannotPost_IsReported` | No usage row. **[auto]** `TextTrigger_WhereTheBotCannotPost_RecordsNoUse`. Warning "Could not send custom command response ... missing permissions" |
| CC-118 | `say` = "%Message" | `!say` | Nothing posted, no error. **[auto]** `TextTrigger_ThatRendersEmpty_PostsNothing_AndRecordsNoUse` | No usage row; no alert |
| CC-119 | `rules` embed; remove Embed Links from the bot in `#general` | `!rules` | **Expected:** a clear report, as for CC-117. **Actual:** unconfirmed. Discord likely drops the embed and refuses the empty message with 400, which is not caught, so it raises an event-handler alert on every use. Record what happens | Record whether a usage row is written |
| CC-120 | none | `! hello` (space after `!`) | Posts "Hello there!" (leading spaces are dropped) | Usage row |
| CC-121 | none | `!Hello` | Nothing (names are case-sensitive) | none |
| CC-122 | none | `!hello` followed by a newline and more text | Nothing: the newline makes the first word `hello\n...`, which isn't a valid name | none |
| CC-123 | `hug` | `!hug @mod a    b` | Message "a b": repeated spaces collapse | Usage row |
| CC-124 | `hello` | Reply to any message with `!hello` | Posts (Reply messages count) | Usage row |
| CC-125 | `pk` has a PluralKit proxy | `pk` sends `!hello` with the proxy tag | One post, from the original message; the proxied webhook copy is ignored. Record if it posts twice | One usage row |
| CC-126 | Learn a custom command named `leaderboard` (or `commands`) | Send `!leaderboard`, then `!commands stats hello` | **Expected:** only the custom command answers, and `!commands ...` does nothing. **Actual:** unconfirmed. DSharpPlus's own `!` text processor is registered too, and commands taking `CommandContext` (`/Commands Stats`, `/Commands Leaderboard`, the leveling `/Leaderboard`) look reachable as text. Record every reply | Record usage rows |
| CC-127 | `hello` in Test-B only | `!hello` in Test-A | Nothing (A-35, `MultiGuildTests`) | none |

Automated in `Grimoire.Tests/Integration/CustomCommands/CustomCommandUseTests.cs`. The "newest version only" lookup is
covered by `AutocompleteQueryTests.GetCustomCommandQuery_*`.

## Code observations

1. **Revert crashes on an out-of-range version** (new). `CustomCommandSettings.Revert.cs:45` (and `:116`) builds
   `new DateTimeOffset(ticks, TimeSpan.Zero)` from any parsed `long`. A negative or very large value throws
   `ArgumentOutOfRangeException`, so a hand-typed version shows the generic error and raises an alert instead of
   "Version not found". Only reachable by typing a value, not from the autocomplete. CC-55.
2. **Roles without `restricted_use` are dropped silently** (new to known-issues; review C-2, decided but not yet done).
   `CustomCommandSettings.Learn.cs:93-106` only builds allow roles, and only when restricted. The reply still says
   "Added". The owner decided these should become deny roles. CC-09.
3. **Output never pings, so the sanitiser does nothing useful** (new to known-issues; review C-3, decided but not yet
   done). No `WithAllowedMentions` in `GetCustomCommand.cs:133-141`, so DSharpPlus sends `parse: []`. The decision is to
   ping only `%Mention`'s resolved user or role and delete `SanitizeUserMessageMentions` (`GetCustomCommand.cs:83-101`).
   CC-21, CC-88, CC-91, CC-113.
4. **Embed commands where the bot lacks Embed Links** (new, unconfirmed). `TextCustomCommandProcessor.cs:135-147` only
   catches `UnauthorizedException`. If Discord drops the embed and refuses the empty message with a 400, the
   `BadRequestException` reaches the event-handler error path on every use, like review C-1 did. CC-119.
5. **The DSharpPlus `!` text processor overlaps the text trigger** (new, unconfirmed). `GrimoireServiceRegistration.cs:207-210`
   registers a `TextCommandProcessor` with prefix `!`. `/Commands Stats` (`CustomCommandSettings.Stats.cs:24`) and
   `/Commands Leaderboard` (`CustomCommandSettings.Leaderboard.cs:23`) take `CommandContext`, so they look runnable as
   `!commands stats ...`, and a custom command named like a text-runnable bot command would get two replies. Belongs
   with area 09's cross-cutting checks. CC-126.
6. **Restricted commands are visible to everyone** (new, low). The name autocomplete (`GetCustomCommandOptions.cs:46-50`)
   and `/Commands Stats` and Leaderboard don't check the role list, so a member learns the names and usage of commands
   they can't run. CC-68, CC-103.
7. **Names are case-sensitive, but autocomplete matches case-insensitively** (new, low, maybe intended). `Hello` and
   `hello` are separate commands, and `!Hello` doesn't run `hello` (`CustomCommandDatabaseQueryHelpers.cs:24`). CC-19,
   CC-121.
8. **Learn says "Added" when it updates** (new, trivial). `CustomCommandSettings.Learn.cs:127`. CC-11.
9. **The database catches miss outages** (review C-6, planned). `Learn` catches `DbUpdateException`, `Forget` and `Stats`
   catch `DbException`; with retry-on-failure an outage arrives as `RetryLimitExceededException` and shows the generic
   error. History, Revert and Leaderboard have no catch at all. CC-26.
10. **Silent `/Command` failures** (behaviour note, not a bug). An unknown or unauthorized name shows the public
    "thinking" state and then vanishes with no message (`GetCustomCommand.cs:46-50`). This keeps restricted commands
    quiet, but a user who mistypes gets no feedback. CC-92, CC-93.

## Findings

Record failures from manual runs here.
