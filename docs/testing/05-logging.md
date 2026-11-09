# 05 - Logging

Scope: `/Log Message` (Set, View, Override, ViewOverrides), `/Log User` (Set, View); the message add, edit, delete and
bulk-delete events; reaction tracking on logged messages; the avatar, nickname, username, join and leave events; and
`CleanupLogsBackgroundTask`.

Source: `Grimoire/Features/Logging/` (`MessageLogging/`, `Settings/`, `UserLogging/`), `Grimoire/Features/LogCleanup/`,
`Grimoire/Utilities/DiscordAuditLogParserService.cs`, `Grimoire/Features/Shared/Commands/ChannelSettingCommand.cs`,
`Grimoire/Features/Shared/Channels/GuildLog/GuildLogPublisher.cs`,
`Grimoire.Settings/Services/SettingsModule.{LogChannels,MessageLog.ChannelOverrides}.cs`.
PluralKit handling in the delete and edit logs is listed here only briefly; area 09 covers it.

Do the standard preflight from `00-environment.md` first. Also:

- `/GeneralSettings ModLogChannel` set to `#modlog`. Settings changes and setup problems are posted there.
- MessageLog and UserLog modules enabled in Test-A.
- Message logs (delete, bulk delete, edit) pointed at `#messagelog`; user logs (join, leave, username, nickname,
  avatar) pointed at `#userlog`, unless a case says otherwise.
- The bot needs View Channel, Send Messages, Embed Links and Attach Files in `#messagelog` and `#userlog`,
  Manage Server (to list invites) and View Audit Log (to attribute deletes). Remove one at a time for the
  bot-lacks-permission cases and restore it afterwards.
- `#private` is a channel the bot can't send in. Make a second channel `#noembed` where the bot can send but has no
  Embed Links or Attach Files.
- Tables (EF defaults; adjust to your schema). `GrimoireDbContext`: `Messages`, `MessageHistory` (created, edited,
  deleted and deleted-by-moderator entries), `Attachments`, `Reactions`, `OldLogMessages` (log posts waiting to be
  purged), `Avatars`, `NicknameHistory`, `UsernameHistory`. `GrimoireSettingsDbContext`: the log channel settings and
  `MessageLogChannelOverrides` (append-only: the newest row per channel wins).

## Behaviour summary (what the code does)

- Every `/Log` command is `RequireGuild` and needs Manage Server, checked by the bot. `/Log Message ...` needs the
  MessageLog module and `/Log User ...` needs the UserLog module, View included.
- Set, Override and ViewOverrides reply publicly. Both Views reply ephemerally. Errors are ephemeral.
- With MessageLog on, every Default or Reply message in the guild is saved, whether or not a log channel is set,
  unless an override says Never. The bot's own posts are never saved. Edits, deletes and reactions only act on saved
  messages.
- A log with no channel set is dropped quietly. History rows are still written.
- Deleted-message, bulk-delete and edit log posts are queued for purge and deleted from Discord after 30 days.
  Join, leave and the other user logs are kept.

---

## A. `/Log Message Set setting option [value]`

Gates: `RequireGuild`, Manage Server, `RequireModuleEnabled(MessageLog)`. `setting` is "Delete Message Log", "Bulk
Delete Message Log" or "Edit Message Log". `option` is Off, Current Channel or Select Channel. The bot must be able to
send messages and embed links in the chosen channel. The delete and bulk delete logs also need Attach Files. A
category, forum or media channel is refused. The reply and mod log name the setting by its code name (`DeleteLog`,
`BulkDeleteLog`, `EditLog`), not its display name.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-01 | none | `admin` runs `/Log Message Set setting:Delete Message Log option:Select Channel value:#messagelog` | "Updated DeleteLog to #messagelog" **[auto]** | MessageDeleted channel saved. `#modlog`: "@admin updated DeleteLog to #messagelog." |
| LOG-02 | none | Same for Bulk Delete Message Log and Edit Message Log | "Updated BulkDeleteLog to ..." and "Updated EditLog to ..." **[auto]** | BulkMessageDeleted and MessageEdited channels saved; one `#modlog` line each |
| LOG-03 | none | Run in `#messagelog`: `option:Current Channel` | "Updated DeleteLog to #messagelog" **[auto]** | Channel saved |
| LOG-04 | Delete log set | `option:Off` | "Disabled DeleteLog." **[auto]** | Setting cleared. `#modlog`: "@admin disabled DeleteLog." |
| LOG-05 | Delete log set to `#messagelog` | Set it to `#messagelog` again | Same "Updated ..." reply (a repeat counts as saved) | No change; `#modlog` gets the line again |
| LOG-06 | none | `option:Select Channel` with no `value` | Ephemeral red "Select a channel when using the Select Channel option." **[auto]** | Setting unchanged |
| LOG-07 | none | `option:Off value:#general` | "Disabled DeleteLog." The channel is ignored | Setting cleared |
| LOG-08 | none | Select a category, then the forum channel | "#<channel> can't hold messages. Pick a text channel." **[auto]** (forum) | Setting unchanged |
| LOG-09 | none | Select `#private` | "@Grimoire doesn't have permission to send messages in that channel." **[auto]** | Setting unchanged |
| LOG-10 | none | Select `#noembed` for each setting | Delete and bulk delete: "@Grimoire also needs **Embed Links** and **Attach Files** in that channel to post these logs." Edit: "... also needs **Embed Links** ..." only. Give Embed Links back and retry: edit log accepted, delete log asks for **Attach Files** **[auto]** | Setting unchanged when refused |
| LOG-11 | none | `mod` (no Manage Server) runs Set | "You do not have the required permissions to use this command." **[auto]** (View variant) | none |
| LOG-12 | MessageLog disabled | `admin` runs Set | "This module is disabled in this server." **[auto]** (View variant) | none |
| LOG-13 | Delete log set in Test-A | `/Log Message View` in Test-B | Test-B shows None | Settings are per guild |

## B. `/Log Message View`

Gates: as section A. Replies ephemerally with the title "Current Logging System Settings" and three lines. A channel
that no longer exists shows "Deleted Channel".

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-15 | Fresh guild | `admin` runs `/Log Message View` | Ephemeral: "**Delete Log:** None", "**Bulk Delete Log:** None", "**Edit Log:** None" **[auto]** | none |
| LOG-16 | All three set | View | Each line shows its channel mention **[auto]** | none |
| LOG-17 | Delete log set, then that channel deleted | View | "**Delete Log:** Deleted Channel" **[auto]** | Setting still holds the old id |
| LOG-18 | none | `mod` runs View | Permission error **[auto]** | none |
| LOG-19 | MessageLog disabled | View | "This module is disabled in this server." **[auto]** (see Code observations: `/ModSettings View` and `/LevelSettings View` now run with their module off) | none |

Automated in `Grimoire.Tests/Integration/Logging/MessageLogSettingsTests.cs` (`View_*`, `Set_*`,
`LogMessage_NeedsManageGuildAndTheModule`).

## C. `/Log Message Override option [channel]` and `/Log Message ViewOverrides`

Gates: as section A. `option` is Always, Inherit or Never; `channel` defaults to the current channel. An override
applies to the channel and everything under it (a category's channels, a channel's threads, a forum's posts). The
nearest override up the tree wins; with none, messages are logged. Override has no bot permission check. Its mod log
entry has the title "<invoker username> updated the channel overrides" and the reply as its description.

ViewOverrides replies publicly with an embed headed "Channel Override Settings", one line per channel:
"#channel - Always Log" or "#channel - Never Log". Inherit rows and deleted channels are left out. A long list is paged
with buttons.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-20 | none | `admin` runs `/Log Message Override option:Never channel:#spam` | "Will now never log messages from #spam and its sub channels/threads." | `MessageLogChannelOverrides` row NeverLog. `#modlog` entry as above **[auto]** |
| LOG-21 | LOG-20 done | `user1` posts in `#spam`, then deletes it | Nothing in `#messagelog` **[auto]** | No `Messages` row |
| LOG-22 | LOG-20 done | `option:Inherit channel:#spam`; post and delete again | "Override was successfully removed from the channel."; the delete is logged **[auto]** | New Inherit row; `Messages` row |
| LOG-23 | `#general`'s category set Never | `option:Always channel:<thread in #general>`; post in the thread and in `#general` | "Will now always log messages from <thread> and its sub channels/threads." The thread message is saved; the `#general` one is not **[auto]** (thread over parent) | Row AlwaysLog |
| LOG-24 | Category set Never, nothing on `#general` | Post in `#general` and in a thread of it | Neither saved (inherited through two levels) **[auto]** (Settings tests) | none |
| LOG-25 | none | Run Override in `#spam` with no `channel` | The override applies to `#spam` | Row for `#spam` |
| LOG-26 | `#spam` already Never | Set Never again | Same reply; `#modlog` gets the entry again | No new row (not modified) **[auto]** (Settings tests) |
| LOG-27 | No override on `#general` | `option:Inherit channel:#general` | "Override was successfully removed from the channel." even though there was none | No new row |
| LOG-28 | A message saved in `#spam`, then `#spam` set Never | Edit and delete that message | Both still logged: an override only stops new messages being saved | History rows added |
| LOG-29 | MessageLog disabled; `mod` without Manage Server | Run Override as `admin`, then as `mod` | "This module is disabled in this server."; permission error | none |
| LOG-30 | `#spam` Never, a thread Always, `#general` Inherit | `/Log Message ViewOverrides` | Public embed "Channel Override Settings": "#spam - Never Log", "<thread> - Always Log". `#general` is not listed | none |
| LOG-31 | No overrides | ViewOverrides | An embed with the heading and an empty body. **Expected:** a line such as "No overrides." **Actual:** blank (see Code observations) | none |
| LOG-32 | An override on a channel that was then deleted | ViewOverrides | The deleted channel is not listed | The row stays |
| LOG-33 | Overrides in Test-A and Test-B | ViewOverrides in Test-A | Only Test-A channels **[auto]** (Settings tests) | none |

Automated in `Grimoire.Tests/Integration/Logging/MessageLogSettingsTests.cs` (`NeverOverride_StopsLogging_AndInheritRestoresIt`,
`AlwaysOverrideOnAThread_WinsOverItsParent`, `Override_IsLoggedToTheModLog`) and
`Grimoire.Settings.Tests/MessageLog/ChannelOverrideTests.cs` (tree walk, not-modified, latest row wins, guild isolation).

## D. `/Log User Set setting option [value]` and `/Log User View`

Gates: `RequireGuild`, Manage Server, `RequireModuleEnabled(UserLog)`. `setting` is "Joined Server Log", "Left Server
Log", "Username Change Log", "Nickname Change Log" or "Avatar Change Log". The avatar log needs Attach Files; the others
need Send Messages and Embed Links. The reply names the setting by its code name (`JoinLog`, `LeaveLog`, `UsernameLog`,
`NicknameLog`, `AvatarLog`). View is ephemeral, titled "Current Logging System Settings" (the same title as the
message log View), with "**Join Log:**", "**Leave Log:**", "**Username Log:**", "**Nickname Log:**" and
"**Avatar Log:**" lines.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-40 | none | `admin` runs `/Log User Set setting:Joined Server Log option:Select Channel value:#userlog`; repeat for the other four | "Updated JoinLog to #userlog" (and LeaveLog, UsernameLog, NicknameLog, AvatarLog) **[auto]** | Each channel saved; `#modlog` "@admin updated JoinLog to #userlog." |
| LOG-41 | All set | `option:Off` for each | "Disabled JoinLog." etc. **[auto]** | Settings cleared; `#modlog` lines |
| LOG-42 | none | `option:Current Channel` in `#userlog` | "Updated ... to #userlog" | Channel saved |
| LOG-43 | none | `option:Select Channel`, no value | "Select a channel when using the Select Channel option." | Unchanged |
| LOG-44 | none | Select `#private` | "@Grimoire doesn't have permission to send messages in that channel." | Unchanged |
| LOG-45 | `#noembed` has Embed Links but not Attach Files | Select it for the avatar log, then the nickname log | Avatar: "@Grimoire also needs **Attach Files** in that channel to post these logs." Nickname: accepted | Only the nickname setting saved |
| LOG-46 | none | Select a category | "#<category> can't hold messages. Pick a text channel." | Unchanged |
| LOG-47 | Fresh guild | `/Log User View` | Ephemeral, all five lines "None" **[auto]** | none |
| LOG-48 | Join log set, then the channel deleted | View | "**Join Log:** Deleted Channel" | none |
| LOG-49 | none | `mod` runs View or Set | "You do not have the required permissions to use this command." **[auto]** | none |
| LOG-50 | UserLog disabled | View or Set | "This module is disabled in this server." **[auto]** | none |

Automated in `Grimoire.Tests/Integration/Logging/UserLogTests.cs` (`View_OnAFreshGuild_ShowsNoLogChannels`,
`Set_StoresTheChannel_AndOffClearsIt`, `LogUser_NeedsManageGuildAndTheModule`).

---

## E. Saving messages (`AddMessageEvent`)

Saves each Default or Reply message in the guild when `ShouldLogMessage` says yes (module on and no Never override up
the tree). It stores the author, channel, the message it replies to, the content as a created entry, and each
attachment's full signed URL. The bot's own posts, DMs and system messages (pins, joins, boosts) are skipped. A
duplicate after a gateway replay is ignored; any other save failure is raised as an error.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-60 | MessageLog on, no log channels set | `user1` posts "hello" in `#general` | none | `Messages` row; `MessageHistory` created entry "hello". Saved even with no log channel |
| LOG-61 | none | `user1` posts with two attachments | none | Two `Attachments` rows with full CDN URLs |
| LOG-62 | none | `user1` replies to a message | none | `Messages.ReferencedMessageId` set |
| LOG-63 | none | The bot posts (any log or reply); pin a message (system message) | none **[auto]** (bot) | No row for either |
| LOG-64 | MessageLog disabled | `user1` posts | none **[auto]** | No row |
| LOG-65 | The DB refuses the insert (for example a column too short, test only) | Post | none | Error logged with the original content and raised to `#alerts` **[auto]** |

Automated in `MessageLogEventsTests` (`TheBotsOwnMessages_AreNotSaved`, `AMessageThatCannotBeSaved_IsRaisedAsAnError`,
`WithTheMessageLogOff_EditsAndDeletesAreIgnored`).

## F. Edits (`UpdateMessageEvent`)

Acts on saved messages only. An edit is logged when the new text differs from the newest stored text, ignoring case.
The log is an embed with the author "Message edited in #channel", the description "**[Jump Url](...)**", fields Channel,
Message Id, Author (or Original Author, System Id and Member Id for a PluralKit proxy), Before and After, and the
author's avatar. When Before and After together reach 5000 characters they go in two separate posts.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-70 | Edit log `#messagelog`; `user1` posted "before" | Edit to "after" | `#messagelog` edit embed with Before "before", After "after" **[auto]** (KI-38 fixed) | `MessageHistory` edited entry "after"; `OldLogMessages` row for the post |
| LOG-71 | As LOG-70 | Edit again to "third" | Before "after", After "third" | Second edited entry |
| LOG-72 | As LOG-70 | Edit "after" to "AFTER" | Nothing logged **[auto]** | No row |
| LOG-73 | A message posted before the bot started (not saved) | Edit it | Nothing logged **[auto]** | No row |
| LOG-74 | none | The bot edits its own message | Nothing logged **[auto]** | No row |
| LOG-75 | `user1` posted about 3000 characters | Edit to another 3000 characters | Two posts: one with Before, one with After **[auto]** | One edited entry |
| LOG-76 | A post with an image and text | Edit to remove all the text | **Expected:** logged with After "`blank`". **Actual:** ignored, because an edit with empty content returns early (see Code observations) | No row |
| LOG-77 | A link post | Wait for Discord to add the link preview (an update with the same text) | Nothing logged | No row |
| LOG-78 | Edit log off | Edit a saved message | Nothing posted | Edited entry still written |
| LOG-79 | Edit log `#private` (set in SQL, or permissions removed after Set) | Edit | Nothing in `#private`; `#modlog` setup report "I could not post in #private." **[auto]** | Edited entry written |
| LOG-80 | Edit log set to a channel that was then deleted | Edit | `#modlog` report "The MessageEdited log is set to #..., which no longer exists, so those logs are lost." **[auto]** | Edited entry written |
| LOG-81 | `pk` proxies a message, then edits the proxy | Edit | Embed shows Original Author, System Id, Member Id, and the original account's avatar **[auto]** | Edited entry |

Automated in `MessageLogEventsTests` (`Edit_*`, `Edits_TheLogDoesNotTrack_AreIgnored`, `ALogChannelTheBotCannotPostIn_IsReportedToTheGuild`,
`ADeletedLogChannel_IsReportedToTheGuild`) and `PluralKitTests.EditingAProxy_ShowsTheOriginalAuthorsAvatar`.

## G. Deletes (`DeleteMessageEvent`)

Acts on saved messages only, and skips the bot's own (cached or stored). It asks PluralKit first: if the deleted
message is an original that PluralKit replaced with a proxy, it only links the two and logs nothing. Otherwise it reads
the audit log for a message delete in the same channel against the same author in the last 10 minutes, whose count
went up since it last looked. A match records a deleted-by-moderator entry and adds a "Deleted By" field. The log has
the author "Message deleted in #channel", fields Channel, Message Id, Author, Deleted By, "**Content**", a
"**[Reply To](...)**" description for a reply, and re-uploads image attachments. With the delete log off, the entry is
written but nothing is downloaded.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-85 | Delete log `#messagelog` | `user1` posts "secret" and deletes it | `#messagelog` embed with Author @user1, Content "secret", no Deleted By **[auto]** | `MessageHistory` deleted entry; `OldLogMessages` row |
| LOG-86 | none | `user1` posts; `mod` deletes it | Embed shows "Deleted By" @mod **[auto]** (KI-39 fixed) | Deleted-by-moderator entry with mod's id **[auto]** (KI-51 fixed) |
| LOG-87 | As LOG-86 | `mod` deletes two of `user1`'s messages in the same channel, one after the other | Both attributed to `mod` (the audit entry's count goes 1, then 2) | Two deleted-by-moderator entries |
| LOG-88 | `mod` deleted one of `user1`'s messages in `#general` | Within 10 minutes `user1` deletes their own message there | Not attributed (the count didn't go up) | Plain deleted entry |
| LOG-89 | In `#general`, `user1` has one message posted before the bot started (unsaved) and one saved message | `mod` deletes the unsaved one; within 10 minutes `user1` deletes the saved one | **Expected:** the self-delete is not attributed. **Actual:** it shows "Deleted By" @mod, because the mod's delete of an unsaved message never moved the cached count (see Code observations) | Deleted-by-moderator entry with mod's id |
| LOG-90 | `user1` replies to a message, then deletes the reply | Delete | Description "**[Reply To](...)**" linking the original | Deleted entry |
| LOG-91 | `user1` posts two images and a `.txt`, then deletes it | Delete | Log post carries the images; other files are listed **[auto]** | Deleted entry |
| LOG-92 | An image over the upload limit | Delete | The image is listed, and the log still posts **[auto]** | Deleted entry |
| LOG-93 | A long message with four images | Delete | One post **[auto]** | Deleted entry |
| LOG-94 | Delete log off | Delete a saved message | Nothing posted; no CDN downloads **[auto]** | Deleted entry written |
| LOG-95 | Bot lacks View Audit Log | `mod` deletes `user1`'s message | Logged without Deleted By; `#modlog` report "I cannot read the audit log, so I cannot tell who deleted messages or banned members outside my commands." | Plain deleted entry |
| LOG-96 | A message posted before the bot started | Delete it | Nothing logged; PluralKit is not asked **[auto]** | No row |
| LOG-97 | none | Delete one of the bot's log posts by hand | Nothing logged **[auto]** | No row |
| LOG-98 | Message not in the bot's cache (restart the bot after posting) | Delete | Still logged with the stored author **[auto]** | Deleted entry |
| LOG-99 | `pk` sends a message PluralKit proxies | Watch `#messagelog` | The original's delete is not logged; deleting the proxy later logs Original Author, System Id, Member Id **[auto]** | `ProxiedMessageLink` row |
| LOG-100 | Delete log `#private` | Delete | `#modlog` "I could not post in #private." | Deleted entry |
| LOG-101 | MessageLog disabled | Delete a saved message | Nothing **[auto]** | No row |

Automated in `MessageLogEventsTests` (`Delete_*`, `DeletedByModeratorEntry_KeepsItsModerator`, `AfterALogPostTimesOut_LaterLogsAreStillPosted`)
and `PluralKitTests` (`TheOriginalBeingReplacedByAProxy_IsLinked_NotLogged`, `DeletingTheProxy_*`, `APluralKitFailure_StillLogsTheDelete`,
`ABurstOfDeletes_IsAllLogged`). LOG-88, 89 and 95 are not automated.

## H. Bulk deletes (`BulkMessageDeletedEvent`)

Looks up the saved messages among those deleted, writes a deleted entry for each, and posts an embed titled "Bulk
Message Delete" with "**Message Count:** n", "**Channel:** #channel" and "Full message dump attached.", plus a text file
named after the current time in RFC 1123 form. Each dump entry has "Author: <newest username> (id)", "Id:",
"Content:" and, if any, "Attachments:", in message order. There is no moderator attribution.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-110 | Bulk delete log `#messagelog`; `user1` and `user2` posted 5 messages | `mod` runs `/Purge All count:5` | Embed "Bulk Message Delete", Message Count 5, the channel; dump in order with each author's username **[auto]** | 5 deleted entries; `OldLogMessages` row |
| LOG-111 | 3 of the 5 were posted before the bot started | Purge 5 | **Expected:** count 5. **Actual:** "Message Count: 3": only saved messages are counted and dumped (see Code observations) | 3 deleted entries |
| LOG-112 | None of the purged messages were saved | Purge | Nothing posted | No rows |
| LOG-113 | A purged message had an attachment | Purge | Dump has an "Attachments:" line with the URL | Deleted entries |
| LOG-114 | Bulk delete log off | Purge | Nothing posted | Deleted entries written |
| LOG-115 | Bulk delete log `#noembed` (Attach Files removed after Set) | Purge | Post fails; `#modlog` report "I could not post in #noembed." if Discord answers 403 | Deleted entries written |
| LOG-116 | MessageLog disabled | Purge | Nothing | No rows |

Automated in `MessageLogEventsTests` (`BulkDelete_IsRecorded_AndLoggedWithADump`, `BulkDelete_DumpNamesTheAuthors_InMessageOrder`).

## I. Reactions on logged messages (`ReactionEvents`)

With MessageLog on, a reaction on a saved message is stored (message, user, emoji, time). Removing it sets
`RemovedAt` (a soft delete); reacting again clears `RemovedAt` and updates the time. Removing an emoji for everyone, or
clearing all reactions, soft-deletes the matching rows. Reactions on unsaved messages are skipped. Reactions are
deleted with their message by the cleanup's cascade. Nothing is posted to Discord; usage features come later.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-120 | A saved message | `user1` reacts with a custom emoji | none | `Reactions` row, `RemovedAt` null **[auto]** |
| LOG-121 | As LOG-120, plus a unicode emoji | React with 👍 | none | Second row |
| LOG-122 | LOG-120 done | `user1` removes the reaction, then adds it again | none | `RemovedAt` set, then cleared; `ReactedAt` updated **[auto]** |
| LOG-123 | `user1` and `user2` reacted 👍, `user1` also 🎉 | `mod` removes all 👍 (remove emoji) | none | Both 👍 rows soft-deleted; 🎉 untouched **[auto]** |
| LOG-124 | Several reactions | `mod` clears all reactions | none | Every row for the message soft-deleted **[auto]** |
| LOG-125 | A message posted before the bot started | React | none | No row **[auto]** |
| LOG-126 | MessageLog disabled | React to a saved message | none | No row **[auto]** |
| LOG-127 | Saved message 32 days old with active and removed reactions | Wait for a cleanup tick | none | Message and all its reactions deleted **[auto]** |
| LOG-128 | Restart the bot so the message and channel are uncached | Clear all reactions | **Expected:** rows soft-deleted. **Actual:** possibly skipped: the clear event finds the guild through the cached channel (see Code observations) | Check `RemovedAt` |

Automated in `Grimoire.Tests/Integration/Logging/ReactionTests.cs`.

---

## J. Join and leave (`GuildMemberAddedEvent`, `GuildMemberRemovedEvent`)

Both need the UserLog module. The join log, "User Joined", has Name, Created, Invite Used and a "Total Members: n"
footer. Invite Used is "<url> (n uses)\n**Created By:** <inviter>" when the used invite is found, "Vanity Invite" when
it isn't and the guild has a vanity URL, and "Unknown Invite" otherwise. Without Manage Server the invites can't be
listed. An account under 7 days old makes the embed yellow and adds a "New Account" field. The leave log, "User Left",
has Name, Created, Joined and "Roles[n]" (split over fields when long), in red. Kicks and bans show as plain leaves.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-140 | Join log `#userlog`; an invite made by `admin` with 2 uses | `user2` joins with it | "User Joined": Invite Used "<url> (3 uses)", "**Created By:** admin", green | none (member rows are area 08) |
| LOG-141 | A single-use invite | `user2` joins with it | Invite shown with "(1 uses)" **[auto]** (KI-50 fixed) | none |
| LOG-142 | Bot lacks Manage Server | `user2` joins | Invite Used "Unknown Invite" **[auto]**; with a vanity URL, "Vanity Invite" (see Code observations) | none |
| LOG-143 | An account under 7 days old | It joins | Yellow embed with "New Account" field | none |
| LOG-144 | Bot just started | `user2` joins in the first seconds | May be logged with no invite or not at all (KI-52) | none |
| LOG-145 | Leave log `#userlog`; `user2` has two roles | `user2` leaves | "User Left" with "Roles[2]" and both mentions, red **[auto]** | none |
| LOG-146 | `user2` has about 50 roles | Leaves | Roles split over several fields; still posted **[auto]** | none |
| LOG-147 | `user2` has no roles | Leaves | "Roles[0]": "None" | none |
| LOG-148 | none | `mod` kicks `user2` | "User Left" (no kick marker) | none |
| LOG-149 | Join log off | `user2` joins | Nothing posted | none |
| LOG-150 | UserLog disabled | Join and leave | Nothing **[auto]** | none |
| LOG-151 | Join log `#private` | Join | `#modlog` "I could not post in #private." | none |

Automated in `UserLogTests` (`Join_IsLogged_WithTheInviteUnknown`, `Leave_*`, `WithTheUserLogOff_NothingIsLogged`) and
`GuildSyncAndInviteTests` (`AJoin_ShowsTheInviteThatWasUsed`, `AJoin_WithASingleUseInvite_CountsTheUse`).

## K. Nickname, username and avatar changes

All three listen to member updates and need the UserLog module. Each compares against the newest stored row and does
nothing when there is no row yet (the first one comes from the member sync in `MemberAdded`, area 08).

- **Nickname** (per guild): ignores case-only changes. Logs "Nickname Updated" with User, Before and After; "`None`"
  for no nickname.
- **Username** (global, one history for all guilds): ignores rows from the last 2 seconds and case-only changes. Logs
  "Username Updated" with User, Before and After. Display name (global name) changes are not tracked.
- **Avatar** (per guild, the guild avatar if set): logs "Avatar Updated" with "**User:** @user", "Old avatar in
  thumbnail. New avatar down below", the old avatar as thumbnail and the new one uploaded. With the avatar log off, the
  history row is still written and nothing is downloaded.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-160 | Nickname log `#userlog`; `user1` nicknamed "One" | Change to "Uno" | "Nickname Updated": Before "One", After "Uno" **[auto]** | `NicknameHistory` row "Uno" |
| LOG-161 | `user1` has no nickname | Set "One" | Before "`None`", After "One" **[auto]** (KI-40 fixed) | Row |
| LOG-162 | As LOG-160 | Clear the nickname | Before "Uno", After "`None`" | Row with no nickname |
| LOG-163 | As LOG-160 | Change "Uno" to "UNO" | Nothing | No row |
| LOG-164 | none | Give `user1` a role | No nickname, username or avatar log **[auto]** | No rows |
| LOG-165 | Username log `#userlog` | `user1` changes their Discord username | "Username Updated": Before old, After new **[auto]** | `UsernameHistory` row |
| LOG-166 | `user1` is in Test-A and Test-B, both with the username log | Change username | Logged in both guilds | Two new `UsernameHistory` rows for one change (see Code observations) |
| LOG-167 | none | `user1` changes only their display name | Nothing | No row |
| LOG-168 | Avatar log `#userlog` | `user1` changes their avatar | "Avatar Updated" with the old thumbnail and the new image attached **[auto]** | `Avatars` row |
| LOG-169 | none | `user1` sets a guild avatar in Test-A | Logged in Test-A only **[auto]** | Test-A `Avatars` row |
| LOG-170 | Avatar log off | Change avatar | Nothing posted; no download **[auto]** | `Avatars` row still written |
| LOG-171 | `user1` has no stored avatar (row deleted) | Change avatar | Nothing **[auto]** | No row |
| LOG-172 | `user1` left and rejoins | Watch `#userlog` | Only "User Joined"; no avatar or username log **[auto]** (KI-49 fixed) | One `UsernameHistory` row |
| LOG-173 | Avatar log `#noembed` (Attach Files removed after Set) | Change avatar | Post fails; `#modlog` report if Discord answers 403 | `Avatars` row |
| LOG-174 | UserLog disabled | Nickname, username and avatar changes | Nothing **[auto]** | No rows |

Automated in `UserLogTests` (`NicknameChange_*`, `FirstNickname_*`, `UsernameChange_*`, `AvatarChange_*`,
`GuildAvatarChange_*`, `AnUpdateRightAfterAFirstJoin_IsNotLoggedAsAChange`, `AnAvatarUpdate_WithNoStoredAvatar_IsNotLogged`,
`RoleChange_IsNotLoggedAsAnAvatarChange`) and `GuildSyncAndInviteTests.ARejoin_DoesNotCauseAFalseAvatarChange`.

---

## L. `CleanupLogsBackgroundTask`

Ticks every minute. Each tick:

1. Deletes from Discord the log posts in `OldLogMessages` older than 30 days. A post that is deleted is forgotten. A
   post that can't be deleted (already gone, channel deleted, Discord error) has its try count raised, and is forgotten
   after the third try.
2. Deletes saved `Messages` created 31 or more days ago, with their history, attachments and reactions.
3. Once on the first tick and then once a day, trims `Avatars`, `NicknameHistory` and `UsernameHistory` to the newest
   three per member (usernames per user).

The module setting doesn't matter. To test by hand, backdate rows in SQL (`OldLogMessages.CreatedAt`,
`Messages.CreatedTimestamp`) and wait a minute.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LOG-180 | Two delete-log posts; backdate one to 31 days | Wait one tick | The old post is gone from `#messagelog`; the new one stays **[auto]** | Old `OldLogMessages` row deleted |
| LOG-181 | Backdated row whose post was deleted by hand | Wait three ticks | none | `TimesTried` 1, 2, then the row is deleted **[auto]** |
| LOG-182 | Backdated row in a channel that was deleted | Wait a tick | none | Counted as a failed try **[auto]** |
| LOG-183 | Two backdated posts; the bot can't delete one | Wait a tick | The other is still deleted **[auto]** | One row gone, one retried |
| LOG-184 | Saved messages 32 and 30 days old | Wait a tick | none | Only the 30-day message remains **[auto]** |
| LOG-185 | `user1` has 5 nicknames, 5 usernames, 5 avatars | Restart the bot, wait a tick | none | 3 of each remain, the newest **[auto]** |
| LOG-186 | LOG-185 done; add 2 more nicknames | Wait a tick (not a day) | none | Still 5 rows until the next daily trim **[auto]** |
| LOG-187 | A join log and a mod-log entry | Backdate nothing; check `OldLogMessages` | none | No rows for join, leave, user or mod logs: only delete, bulk delete and edit logs are queued |
| LOG-188 | A purge of the bot's log post | Watch `#messagelog` | The purge doesn't cause a new delete log | none |

Automated in `Grimoire.Tests/Integration/Logging/LogCleanupTests.cs`. LOG-187 and 188 are not automated (LOG-188 is
close to `MessageLogEventsTests.Delete_OfAnUncachedPostByTheBot_IsIgnored`).

---

## Code observations

1. **A mod's delete of an unsaved message can be credited to a later self-delete** (new, med).
   `DeleteMessageEvent.cs:71` returns before the audit log is read when the message was never saved (posted before
   the bot started, in a Never channel, or purged). The cached count in `DiscordAuditLogParserService.cs:75` is only
   moved by deletes that reach the parser. So if `mod` deletes such a message by `user1`, and `user1` deletes a saved
   message of their own in the same channel within 10 minutes, the second delete shows "Deleted By" @mod and is saved
   as a deleted-by-moderator entry. Confirm with LOG-89.
2. **Log Views still need their module** (new, low, maybe intended). `LogSettingsCommands.cs:30` and `:37` put
   `RequireModuleEnabled` on the whole group, so `/Log Message View` and `/Log User View` say "This module is disabled
   in this server." Commit a624c69 let `/ModSettings View` and `/LevelSettings View` run with their module off.
   Confirm with LOG-19, LOG-50 (and GEN-13, GEN-14).
3. **Set replies use code names** (new, low). `LogSettingsCommands.Message.Set.cs:52` and `User.Set.cs:58` pass
   `logSetting.ToString()`, so the reply and mod log read "Updated DeleteLog to ..." and "Disabled JoinLog." rather
   than "Delete Message Log". The tests pin the current text. LOG-01, LOG-40.
4. **"Vanity Invite" when the invite is simply unknown** (new, low). `GuildMemberAddedEvent.cs:35` shows "Vanity
   Invite" whenever no invite was matched and the guild has a vanity URL. That includes the bot lacking Manage Server
   and the startup window. LOG-142.
5. **A join right after startup can go unlogged** (KI-52). `InviteService.cs:41` throws "Could not find guild." when
   the startup sync hasn't stored the guild's invites yet, so the join log is lost. LOG-144.
6. **One username change writes a history row per guild** (new, low). `UpdatedUsernameEvent.cs:32` ignores rows from
   the last 2 seconds so every guild's update event can log the change. Each guild then adds its own
   `UsernameHistory` row for the same change. If a guild's event arrives more than 2 seconds after the first, that
   guild sees the new name as current and logs nothing. LOG-166.
7. **An edit that removes all text isn't logged** (new, low). `UpdateMessageEvent.cs:28` returns on empty content, so
   cutting a post down to its attachments leaves no edit entry or log. LOG-76.
8. **Bulk delete counts only saved messages** (new, low). `BulkMessageDeletedEvent.cs:79` reports `messages.Length`,
   the saved ones, as "Message Count", so a purge of partly unsaved messages under-reports. No moderator is named
   either. The dump's file name (`:90`, RFC 1123 time) has commas and colons; check how Discord shows it. LOG-111.
9. **ViewOverrides with nothing to show is a blank embed** (new, low). `LogSettingsCommands.Message.ViewOverrides.cs:33`
   sends the heading with an empty body. Overrides on deleted channels are hidden (`:27`) and their rows are never
   cleaned up. LOG-31, LOG-32.
10. **Clearing reactions on an uncached message may be missed** (new, low, unconfirmed). `ReactionEvents.cs:80` takes
    the guild from `args.Message.Channel?.Guild`; if DSharpPlus can't resolve the channel, the clear is skipped and the
    rows stay active. LOG-128.
11. **Fixed issues to re-check here:** KI-38 (edits logged, LOG-70), KI-39 (delete attribution, LOG-86), KI-40 (first
    nickname, LOG-161), KI-49 (rejoin avatar, LOG-172), KI-50 (single-use invite, LOG-141), KI-51 (moderator id saved,
    LOG-86).

Not bugs, noted for testers: a repeated Set or Override posts its mod-log line again (`ReplyResultAsync` treats "not
modified" as saved); an override set after a message was saved doesn't stop its edits and deletes being logged; kicks
and bans log as plain leaves.

## Findings

Record failures from manual runs here.
