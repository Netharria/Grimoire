# 04 - Spam Filter and Diagnostics

Scope: the spam filter (`SpamEvents`, `SpamTrackerModule`), `/SpamFilter` (Override, View), `/Diagnostics` (Check,
Ignore Add/Remove/View), and the diagnostics follow-up report that `/Mutes Create` and `/Mutes Refresh` post. Area 02
(MOD-79, MOD-86, MOD-87) only checks that the follow-up appears. This doc checks what it says.

Source: `Grimoire/Features/Moderation/SpamFilter/`, `Grimoire/Features/Moderation/Diagnostics/`,
`Grimoire/Features/Moderation/Mute/Commands/MuteAdminCommands*.cs`,
`Grimoire.Settings/Services/SettingsModule.Moderation.{SpamFilterOverrides,DiagnosticsIgnores,Mutes}.cs`,
`Grimoire/Structs/PermissionValues.cs`.

Do the standard preflight from `00-environment.md` first. Also:

- `/GeneralSettings ModLogChannel` set to `#modlog`.
- Moderation and AntiSpam modules enabled (`/Modules Set`).
- A mute role `@Muted` configured with `/Mutes Create` or `/Mutes Set`, and `/Mutes Refresh` run once.
- Reset the DB first, so `user1` and `mod` have no mute sins from the last 24 hours. Spam mutes get longer with each
  mute sin in that window.
- "Sins" are in `GrimoireDbContext`. `Mutes`, `SpamFilterOverrides` and `DiagnosticsIgnoredItems` are in
  `GrimoireSettingsDbContext`. Table names assume EF defaults.

---

## A. The spam filter (`SpamEvents`)

Gates: runs on every new message in a guild. It skips bots (this includes PluralKit's webhook messages), the guild
owner, and any member with Manage Channels at guild level (so `admin` is skipped, but `mod` is not). It does nothing
unless AntiSpam is on. It also does nothing when there is no effective mute role: Moderation off, or no mute role set.
In that case the spam is not deleted or logged either.

Channel overrides are checked from the message's channel up through its parents (thread, then channel, then category).
The first Always stops the walk and filters. The first Never turns the filter off. Inherit, or no row, moves up. With
no override anywhere, the channel is filtered.

Scoring, per member per guild, kept in memory for 30 minutes: each message adds 10 points, plus 4.15 if it has any
attachments (however many), 4.15 per embed, 0.00625 per character, 0.714 per line break, 2.5 per user or role
mention, and 10 if it is the same text as the member's last message. Points decay at 2 per second. Over 60 points is
spam. The points then reset to 0, so one burst gives one mute (KI-53).

On spam: a Mute sin with a System actor and reason "Auto mod: Spam detected." is saved, then a `MuteAdded` row, then
the mute role is granted, then the mod log and DM are sent, then the triggering message is deleted. The mute lasts
2^n minutes, where n is the number of Mute sins the member got in the last 24 hours (pardoned ones count too).

Spam recipe: send the same short message ("buy cheap gold now") 5 times, each within about a second of the last.
Four quick repeats is just over the threshold.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| SPAM-01 | Preflight done | `user1` spams in `#general` **[auto]** `SpamFilterTests.Spam_MutesTheSpammer_DeletesTheMessage_AndLogs` | `user1` gets `@Muted` (audit log reason "Auto mod: Spam detected."). The message that tripped the filter is deleted; earlier ones stay. DM: author "Mute Id n", "You have been muted for 1 minute(s) by @Grimoire for spamming." | One `Sins` row: Mute, System actor, reason "Auto mod: Spam detected.". One `MuteAdded` row: moderator = the bot, `EndTime` about now + 1 min. `#modlog` red embed, author "Mute": User, Sin Id `**n**`, Moderator @Grimoire, Length "1 minute(s)", Reason "Auto mod: Spam detected." |
| SPAM-02 | SPAM-01 done | Keep sending fast for a few more seconds during the same burst | Still only one mute | Still one sin and one `MuteAdded` row (KI-53) |
| SPAM-03 | SPAM-01 done; wait for the mute to expire (about 1 min plus the 5 s tick) | `user1` spams again **[auto]** `SpamEventsTests.RepeatedSpam_DoublesTheMuteEachTime` | Muted for 2 minutes. Mod log Length "2 minute(s)" | Second Mute sin; `EndTime` about now + 2 min |
| SPAM-04 | SPAM-03 done and expired | Spam a third time **[auto]** `SpamEventsTests.RepeatedSpam_DoublesTheMuteEachTime` | Length "4 minute(s)" | Third sin |
| SPAM-05 | `user1` was muted with `/Mute` an hour ago, then unmuted | `user1` spams **[auto]** `SpamEventsTests.AManualMuteEarlierToday_DoublesTheSpamMute` | Length "2 minute(s)": manual mutes count toward the escalation | none extra |
| SPAM-06 | A spam mute sin pardoned with `/Pardon` | Spam again within 24 h **[auto]** `SpamEventsTests.APardonedSpamMute_StillDoublesTheNext` | Length still doubles: pardoned mutes count | none extra |
| SPAM-07 | none | `user1` sends "hello", waits 5 s, sends "how is everyone" **[auto]** `SpamFilterTests.OrdinaryMessages_AreNotFiltered` | Nothing happens | No sin |
| SPAM-08 | none | `user1` sends two messages with 10 images each, quickly **[auto]** `SpamFilterTests.ImagesSharedTogether_AreNotSpam` | Not spam (about 28 points) | No sin |
| SPAM-09 | none | `user1` sends 3 quick different messages that each mention 6 users **[auto]** `SpamEventsTests.MessagesFullOfMentions_AreSpamOnTheThird` | Spam on the third (each is about 25 points) | One Mute sin |
| SPAM-10 | none | `user1` sends 7 different one-word messages within about one second **[auto]** `SpamEventsTests.SevenQuickOneWordMessages_AreSpamOnTheSeventh` | Spam on the 7th (6 x 10 = 60 is not over the threshold) | One Mute sin |
| SPAM-11 | none | `user1` sends a 40-line message, then repeats it at once **[auto]** `SpamEventsTests.ALongMessageRepeated_IsSpamOnTheSecondCopy` | Spam on the second copy (about 39 points, then about 49) | One Mute sin |
| SPAM-12 | none | `admin` spams; then `owner` spams **[auto]** `SpamFilterTests.Staff_AreNeverFiltered` | Nothing happens | No sin |
| SPAM-13 | `mod` has no Manage Channels | `mod` spams **[auto]** `SpamEventsTests.MembersWithoutManageChannels_AreFiltered("mod")` | `mod` is muted like `user1`. Only guild-level Manage Channels exempts | Mute sin for `mod` |
| SPAM-14 | Give `user1` a Manage Channels allow overwrite on `#spam` only | `user1` spams in `#spam` | Muted: a channel-level Manage Channels does not exempt | Mute sin. **[auto]** `ChannelPermissionTests.Spam_ByAMemberWithManageChannelsOnlyInTheChannel_IsStillMuted` |
| SPAM-15 | AntiSpam disabled **[auto]** `SpamFilterTests.Spam_WithAntiSpamDisabled_IsIgnored` | `user1` spams | Nothing happens | No sin |
| SPAM-16 | Moderation disabled, AntiSpam on **[auto]** `SpamFilterTests.WithoutAMuteRole_SpamIsIgnored("moderation off")` | `user1` spams | Nothing happens. The spam is not deleted or logged | No sin |
| SPAM-17 | No mute role set **[auto]** `SpamFilterTests.WithoutAMuteRole_SpamIsIgnored("no mute role")` | `user1` spams | Nothing happens, as SPAM-16 | No sin |
| SPAM-18 | `#general` override Never **[auto]** `SpamFilterTests.NeverOverride_TurnsTheFilterOff("channel")` | `user1` spams in `#general` | Nothing happens | No sin |
| SPAM-19 | Forum override Never **[auto]** `SpamFilterTests.NeverOverride_TurnsTheFilterOff("parent")` | `user1` spams in a post in that forum | Nothing happens | No sin |
| SPAM-20 | `#general`'s category set to Never | `user1` spams in `#general` **[auto]** `SpamEventsTests.Overrides_AreInheritedFromTheNearestSetting("SPAM-20")` | Nothing happens | No sin |
| SPAM-21 | Category Never, `#general` Always | `user1` spams in `#general` **[auto]** `SpamEventsTests.Overrides_AreInheritedFromTheNearestSetting("SPAM-21")` | Muted: the nearer Always wins | Mute sin |
| SPAM-22 | Category Never, `#general` Inherit | `user1` spams in `#general` **[auto]** `SpamEventsTests.Overrides_AreInheritedFromTheNearestSetting("SPAM-22")` | Nothing happens: Inherit follows the category | No sin |
| SPAM-23 | `#general` Never; a thread in `#general` set to Always | `user1` spams in the thread **[auto]** `SpamEventsTests.Overrides_AreInheritedFromTheNearestSetting("SPAM-23")` | Muted | Mute sin |
| SPAM-24 | none | `user1` spams in a voice channel's text chat **[auto]** `SpamEventsTests.SpamInAVoiceChannelsChat_IsFiltered` | Muted (voice chat is filtered like any channel) | Mute sin |
| SPAM-25 | Bot lacks Manage Messages in `#general` **[auto]** `SpamFilterTests.Spam_WhoseMessageCannotBeDeleted_IsStillLoggedAndTold` | `user1` spams | Muted, logged and DMed; the message stays | Sin and `MuteAdded` rows; `#modlog` embed |
| SPAM-26 | `user1` has DMs closed | `user1` spams **[auto]** `SpamEventsTests.SpamFromAMemberWithClosedDms_IsStillMuted_AndTheModLogTold` | Muted. The DM failure is posted to the mod log (KI-16) | Sin, `MuteAdded`, `#modlog` embed and DM-failure line |
| SPAM-27 | Remove Manage Roles from the bot | `user1` spams **[auto]** `SpamEventsTests.WhenTheMuteRoleCannotBeGiven_NothingIsRecorded("SPAM-27")` | Fixed (KI-55): the grant is tried first. Discord refuses (403), so nothing is recorded, the spam is deleted, and `#modlog` gets a "Setup problem" naming the missing permission (at most once a day) | No Mute sin, no `MuteAdded` row |
| SPAM-28 | Drag `@Muted` above the bot's role | `user1` spams **[auto]** `SpamEventsTests.WhenTheMuteRoleCannotBeGiven_NothingIsRecorded("SPAM-28")` | Same as SPAM-27 (403 on the grant) | As SPAM-27 |
| SPAM-29 | Delete `@Muted` in Discord without changing the setting | `user1` spams **[auto]** `SpamEventsTests.WhenTheMuteRoleCannotBeGiven_NothingIsRecorded("SPAM-29")` | Fixed (KI-55): nothing recorded, the spam is deleted, and `#modlog` gets a "Setup problem" saying the mute role is missing | No Mute sin, no `MuteAdded` row |
| SPAM-30 | `higher` has no Manage Channels | `higher` spams **[auto]** `SpamEventsTests.MembersWithoutManageChannels_AreFiltered("higher")` | Muted like anyone: there is no rank check, and Discord only checks that `@Muted` is below the bot. If Discord refuses the grant, it behaves as SPAM-27; record which | Mute sin |
| SPAM-31 | `pk` account (optional) | `pk` sends 5 quick proxied messages | The proxied (webhook) copies are never counted. The originals are counted; if PluralKit deletes them first, the delete fails quietly as in SPAM-25 | Mute sin if the originals trip it. **[auto]** `PluralKitAcrossFeaturesTests.SpamThroughProxies_MutesTheMember_AndLeavesTheProxies` |
| SPAM-32 | `user1` built up 50 points in Test-A | `user1` sends 2 quick messages in Test-B **[auto]** `SpamEventsTests.Points_AreKeptPerGuild` | Not spam in Test-B: points are per guild | No Test-B sin |
| SPAM-33 | `user1` built up 50 points | Restart the bot, then send 2 quick messages **[auto]** `SpamEventsTests.Points_AreForgottenOnRestart` | Not spam: points live in memory only | No sin |
| SPAM-34 | `user1` is muted, but `#leaky` has an allow Send Messages overwrite for a role `user1` holds | `user1` spams in `#leaky` **[auto]** `SpamEventsTests.SpamWhileStillMuted_MutesAgain_ForTwiceAsLong` | Muted again; Length doubles | A second Mute sin and `MuteAdded` row while the first is active |

Automated in `Grimoire.Tests/Integration/Moderation/SpamFilterTests.cs` (SPAM-01, 02, 07, 08, 12, 15 to 19, 25) and
`SpamEventsTests.cs` (SPAM-03 to 06, 09 to 11, 13, 20 to 24, 26 to 30, 32 to 34).
SPAM-14 needs channel-resolved permissions (H-13). SPAM-31 (PluralKit) is not automated here.

## B. `/SpamFilter Override option [channel]`

Gates: Manage Guild is not needed. The command needs Manage Channels, checked by the bot (`RequireUserGuildPermissions`;
the owner and Administrators pass). Everyone sees the command. It has no `RequireGuild` and no module check: it works
with AntiSpam and Moderation both off. Outside a guild the permission check replies "This command can only be used in a
server.". `channel` defaults to the current channel and takes any channel type, including categories and threads. The
reply is public and purple. Nothing is posted to the mod log. Setting the value a channel already has replies the
same as a change. Inherit is stored as a row, not deleted.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| SPAM-40 | none | `admin` runs `/SpamFilter Override option:Never channel:#general` **[auto]** `SpamFilterTests.Override_StoresTheSetting_AndViewListsIt` and `SpamFilterOverrideTests.Override_PostsToTheModLog` | "Will now never filter spam messages from #general and its sub channels/threads." | `SpamFilterOverrides` row NeverFilter, `SetBy` admin. `#modlog`: "@admin set the spam filter override of #general to Never." (KI-60) |
| SPAM-41 | none | `option:Always channel:<forum>` **[auto]** same test | "Will now always filter spam messages from #forum and its sub channels/threads." | Row AlwaysFilter |
| SPAM-42 | SPAM-40 done | `option:Inherit channel:#general` **[auto]** `SpamFilterTests.Override_Inherit_ClearsTheOverrideAndSaysSo` | "#general and its sub channels/threads will now follow the parent channel's spam filter setting." (KI-37) | New row Inherit; `/SpamFilter View` no longer lists `#general` |
| SPAM-43 | none | `option:Never` with no channel, run in `#spam` **[auto]** `SpamFilterOverrideTests.Override_WithNoChannel_IsForTheCurrentChannel` | Reply names `#spam` | Row for `#spam` |
| SPAM-44 | none | `option:Never channel:<a thread>` **[auto]** `SpamFilterTests.Override_OfAThread_IsAccepted` | "Will now never filter spam messages from <thread> and its sub channels/threads." | Row for the thread |
| SPAM-45 | none | `option:Never channel:<a category>` **[auto]** `SpamFilterOverrideTests.Override_OfACategory_IsAccepted` | Accepted; the channels under it stop being filtered (SPAM-20) | Row for the category |
| SPAM-46 | `#general` already Never | `option:Never channel:#general` again **[auto]** `SpamFilterOverrideTests.Override_ToTheValueItAlreadyHas_RepliesTheSame_AndStoresNoNewRow` | The same "Will now never filter..." reply | No new row |
| SPAM-47 | No override on `#spam` | `option:Inherit channel:#spam` **[auto]** `SpamFilterOverrideTests.Override_Inherit_OnAChannelWithNoOverride_StoresNoRow` | The Inherit reply | No new row (already Inherit) |
| SPAM-48 | none | `mod` (no Manage Channels) runs it **[auto]** `SpamFilterTests.SpamFilter_NeedsManageChannels("override")` | "You do not have the required permissions to use this command." | No row |
| SPAM-49 | A member with Manage Channels but not Manage Guild | Runs it **[auto]** `SpamFilterOverrideTests.Override_ByAMemberWithManageChannelsOnly_IsAllowed` | Allowed | Row saved |
| SPAM-50 | AntiSpam disabled | `admin` runs `option:Never` **[auto]** `SpamFilterOverrideTests.Override_WithAntiSpamDisabled_IsRefused` | "This module is disabled in this server." (fixed, KI-60) | No row |
| SPAM-51 | Settings DB unreachable **[auto]** `SettingSaveFailureTests.ASettingThatCannotBeSaved_IsReportedPrivately_AndNotLogged("spam_filter override")` | Run it | Ephemeral red "Could not save the spam filter override." | No row |
| SPAM-52 | none | Text command `!spamfilter override Never <id of a Test-B channel>` in Test-A | Yellow "That channel does not exist in this server." if the id converts; otherwise an argument error. Slash commands can't pick another guild's channel | No row |
| SPAM-53 | Bot can't see `#private` | `admin` overrides `#private` **[auto]** `SpamFilterOverrideTests.Override_OfAChannelTheBotCannotSee_IsAccepted` | Accepted: the command makes no Discord call for the channel | Row saved |
| SPAM-54 | Override set in Test-A | `/SpamFilter View` in Test-B **[auto]** `SpamFilterOverrideTests.View_InAnotherGuild_DoesNotListThisGuildsOverrides` | Test-B does not list it | none |

Automated in `Grimoire.Tests/Integration/Moderation/SpamFilterTests.cs`,
`Grimoire.Tests/Integration/Moderation/SpamFilterOverrideTests.cs` and
`Grimoire.Tests/Integration/Shared/SettingSaveFailureTests.cs`.

## C. `/SpamFilter View`

Gates: as Override. Lists the latest override per channel, skipping Inherit and channels that no longer exist. Each
line is "#channel - Always Filter" or "#channel - Never Filter". A long list is paged with buttons for the invoker.
The reply is public.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| SPAM-60 | `#general` Never, forum Always | `admin` runs `/SpamFilter View` **[auto]** `SpamFilterTests.Override_StoresTheSetting_AndViewListsIt` | Purple embed, author "Spam Filter Override Settings", lines "#general - Never Filter" and "#forum - Always Filter" | none |
| SPAM-61 | No overrides | View **[auto]** `SpamFilterOverrideTests.View_WithNoOverrides_SaysNone` | "None" (fixed, KI-61) | none |
| SPAM-62 | `#general` set Never then Inherit | View **[auto]** `SpamFilterOverrideTests.View_LeavesOutAChannelSetBackToInherit` | `#general` is not listed | none |
| SPAM-63 | An override on a channel, then the channel deleted | View **[auto]** `SpamFilterOverrideTests.View_LeavesOutADeletedChannel_ButKeepsItsRow` | The deleted channel is not listed | Row still there |
| SPAM-64 | Overrides on about 150 channels (script or SQL; restart the bot after SQL) | View **[auto]** `PaginationTests.SpamFilterView_With150Overrides_IsPaged`, `Buttons_WorkForTheInvokerOnly` | Paged: "Page N of M" footer, buttons work for the invoker only | none |
| SPAM-65 | none | `mod` runs View **[auto]** `SpamFilterTests.SpamFilter_NeedsManageChannels("view")` | "You do not have the required permissions to use this command." | none |
| SPAM-66 | AntiSpam disabled | View **[auto]** `SpamFilterOverrideTests.View_WithAntiSpamDisabled_IsRefused` | "This module is disabled in this server." The whole `/SpamFilter` group needs AntiSpam since KI-60 | none |

Automated in `Grimoire.Tests/Integration/Moderation/SpamFilterTests.cs` and
`Grimoire.Tests/Integration/Moderation/SpamFilterOverrideTests.cs`, and SPAM-64 (paging) in
`Grimoire.Tests/Integration/Shared/PaginationTests.cs`.

---

## D. `/Diagnostics Check`

Gates (on the group, so they apply to every subcommand): `RequireGuild`, `RequireModuleEnabled(Moderation)`, and Manage
Guild checked by the bot. Everyone sees the command. Check makes no Discord calls of its own. It reads the bot's cache
of roles, channels and overwrites, so after changing permissions by hand wait a few seconds. The slash reply is
ephemeral: a "Moderation diagnostics" embed with the full report attached as `moderation-diagnostics.txt`.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| DIAG-01 | Preflight done, `@Muted` refreshed, no allow overwrites in the guild | `admin` runs `/Diagnostics Check` **[auto]** `DiagnosticsTests.Check_RepliesPrivately_WithTheReportAttached` | Ephemeral embed titled "Moderation diagnostics" with `moderation-diagnostics.txt` attached. Green "No problems found." if nothing is wrong (the Administrator line, if any, comes first) | none |
| DIAG-02 | none | `owner` runs it; then a member with Manage Guild only **[auto]** `DiagnosticsFindingsTests.Check_IsAllowedForTheOwner_AndAManageGuildMember` | Allowed | none |
| DIAG-03 | none | `mod` (no Manage Guild) runs it **[auto]** `DiagnosticsTests.Diagnostics_NeedsManageGuildAndTheModule("permission")` | "You do not have the required permissions to use this command." | none |
| DIAG-04 | Moderation disabled | `admin` runs it **[auto]** `DiagnosticsTests.Diagnostics_NeedsManageGuildAndTheModule("module")` | "This module is disabled in this server." | none |
| DIAG-05 | none | Run it in a DM with the bot **[auto]** `DiagnosticsFindingsTests.Diagnostics_IsNotOfferedInDirectMessages` | Refused by `RequireGuild` | none |
| DIAG-06 | Remove Manage Roles and Manage Channels from the bot | Run it **[auto]** `DiagnosticsFindingsTests.Check_WithoutManageRolesOrManageChannels_RunsAnywhere_AndReportsBothAlone` | Runs, and reports both as setup problems (DIAG-23, DIAG-25) | none |
| DIAG-07 | Bot can't send in `#private` | Run it in `#private` **[auto]** `DiagnosticsFindingsTests.Check_WithoutManageRolesOrManageChannels_RunsAnywhere_AndReportsBothAlone` | Works: an interaction reply needs no channel permission | none |
| DIAG-08 | Different setups in Test-A and Test-B | Run it in each **[auto]** `DiagnosticsFindingsTests.Check_InEachGuild_ReportsOnlyThatGuild` | Each report covers only its own guild | none |

Automated in `Grimoire.Tests/Integration/Moderation/DiagnosticsTests.cs`.

## E. What the report says

Same report for `/Diagnostics Check` and the `/Mutes` follow-up. Checked channels: text, forum and media use the text
set (Send Messages, Send Messages in Threads, Send TTS, Add Reactions); voice and stage use the voice set (Connect,
Speak). Announcement channels, categories and threads are skipped. Only text-like channels are lock-checked. Roles with
Administrator, ignored roles, the mute role and @everyone are never reported as leaking. A role or member "leaks" if
it keeps any one permission of the set.

The embed lists the excluded roles, then each setup problem as "- <problem> **Fix:** <fix>", then "Mutes may not work in
**N** channel(s). Locks may not work in **M** channel(s). See the attached report for details." It is yellow when
there are findings, green when not, and cut to 4000 characters. The file starts "Moderation diagnostics" and a line of
`=`, then the excluded roles, then "Setup problems", "Mutes may not work in these channels" and "Locks may not work in
these channels" sections, each with "#channel" and "  - <finding>" lines.

Run `/Diagnostics Check` as `admin` for each case, then undo the setup.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| DIAG-20 | No mute role set **[auto]** `ModerationDiagnosticsTests.Without_a_mute_role_no_channel_mute_findings_are_produced` | Check | Setup problem "No mute role is configured." Fix "run /Mutes Create to make one, or /Mutes Set to choose an existing role." No mute channel findings; lock findings still appear | none |
| DIAG-21 | Mute role set, then deleted in Discord **[auto]** `ModerationDiagnosticsTests.Configured_mute_role_that_no_longer_exists_is_reported` | Check | "The configured mute role no longer exists." Fix "run /Mutes Create to make a new one, or /Mutes Set to choose another role." | none |
| DIAG-22 | `@Muted` dragged above the bot's role **[auto]** `ModerationDiagnosticsTests.Mute_role_at_or_above_the_bots_top_role_is_reported` | Check | "The mute role is at or above the bot's highest role, so the bot cannot assign it." Fix "in Server Settings > Roles, drag the bot's role above the mute role." | none |
| DIAG-23 | Bot lacks Manage Roles **[auto]** `ModerationDiagnosticsTests.Bot_without_manage_roles_is_reported` | Check | "The bot does not have the Manage Roles permission, so it cannot mute or unmute anyone." Fix "give the bot's role Manage Roles." The role-position check is skipped | none |
| DIAG-24 | Bot lacks Manage Roles and `@Muted` is above it | Check **[auto]** `DiagnosticsFindingsTests.Check_WithoutManageRolesOrManageChannels_RunsAnywhere_AndReportsBothAlone` | Only the Manage Roles problem is shown | none |
| DIAG-25 | Bot lacks Manage Channels | Check **[auto]** `DiagnosticsFindingsTests.Check_WithoutManageRolesOrManageChannels_RunsAnywhere_AndReportsBothAlone` | "The bot does not have the Manage Channels permission, so it cannot lock channels." Fix "give the bot's role Manage Channels." | none |
| DIAG-26 | A new text channel `#fresh` made after `/Mutes Refresh` **[auto]** `ModerationDiagnosticsTests.Channel_without_a_mute_role_overwrite_is_reported_once` | Check | Embed counts `#fresh` once under mutes. File, under "Mutes may not work in these channels": "#fresh" then "The mute role does not deny the needed permissions here, so muted members can still use this channel. Fix: run /Mutes Refresh." No per-role findings for that channel | none |
| DIAG-27 | `#fresh` but @everyone is denied Send Messages, Send in Threads, TTS and Add Reactions there **[auto]** `ModerationDiagnosticsTests.Channel_where_everyone_cannot_speak_needs_no_mute_overwrite` | Check | `#fresh` not listed under mutes | none |
| DIAG-28 | `mod` role has an allow Send Messages overwrite in `#general` **[auto]** `ModerationDiagnosticsTests.Role_with_an_allow_overwrite_is_reported_as_speaking_while_muted` and `Role_with_an_allow_overwrite_keeps_speaking_in_a_locked_channel` | Check | File, under both mutes and locks for `#general`: "Members with these roles can still use this channel while muted: mod. Fix: remove the explicit allow for those roles in this channel, or ignore the roles if that is intended." and the "while it is locked" version | none |
| DIAG-29 | `mod` role has Send Messages at role level only, no overwrite **[auto]** `ModerationDiagnosticsTests.Lock_is_fine_when_only_role_level_permissions_allow_speaking` | Check | Not reported: the lock's @everyone deny and the mute role's deny both win | none |
| DIAG-30 | @everyone has an allow Send Messages overwrite in `#general` **[auto]** `ModerationDiagnosticsTests.Existing_everyone_allow_is_replaced_by_the_lock` | Check | No lock finding: the lock replaces that allow | none |
| DIAG-31 | `user2` has a member allow Send Messages overwrite in `#general` **[auto]** `ModerationDiagnosticsTests.Member_overwrite_that_allows_speaking_is_reported` | Check | Under mutes: "These members have their own permission overwrite that overrides the mute (user IDs): <user2 id>. Fix: remove their overwrite in this channel." Under locks: "...that overrides a lock (user IDs): <user2 id>..." | none |
| DIAG-32 | The `admin` role has Administrator and an allow overwrite in `#general` **[auto]** `ModerationDiagnosticsTests.Administrator_roles_are_not_reported_and_are_listed_separately` | Check | "Administrator roles (excluded, Administrator ignores channel permissions): admin" in embed and file; `admin` not reported for `#general` | none |
| DIAG-33 | A role overwrite on a voice channel allows Speak **[auto]** `ModerationDiagnosticsTests.Voice_channels_are_checked_with_voice_permissions` and `ModerationDiagnosticsTests.Voice_channels_are_lock_checked_with_voice_permissions` | Check | Reported under mutes and under locks for that voice channel. `/Lock` accepts voice and stage channels since 2026-11-10, so they are lock-checked with the voice set | none |
| DIAG-34 | The bot's role has an explicit deny Manage Channels overwrite in `#locktest` **[auto]** `ModerationDiagnosticsTests.Bot_without_manage_channels_in_a_channel_cannot_lock_it` | Check | Under locks for `#locktest`: "The bot does not have Manage Channels in this channel, so it cannot lock it. Fix: check this channel's permission overwrites for the bot's role." | none |
| DIAG-35 | An announcement channel, a category and a thread, none with a mute overwrite | Check **[auto]** `DiagnosticsFindingsTests.Check_SkipsAnnouncementChannels_Categories_AndThreads` | None of them are listed | none |
| DIAG-36 | A forum and a media channel without a mute overwrite | Check **[auto]** `DiagnosticsFindingsTests.Check_ListsForumAndMediaChannels_WithoutAMuteOverwrite` and `DiagnosticsFindingsTests.Check_DoesNotLockCheckMediaChannels` | Both listed under mutes. Only the forum is checked for locks: `/Lock` refuses media channels (fixed, KI-59) | none |
| DIAG-37 | The bot's role has an allow Send Messages overwrite in `#modlog` (common setup); separately, a plain `Staff` role the bot also holds | Check **[auto]** `DiagnosticsFindingsTests.Check_LeavesOutTheBotsOwnRole` and `DiagnosticsFindingsTests.Check_StillReportsARoleTheBotHolds_ThatIsNotManaged` | The bot's role, and any role managed by a bot, is left out and listed as "Bot roles (excluded, bots need their own channel permissions)" (fixed, KI-58). `Staff` is still reported: only bot-managed roles are left out | none |
| DIAG-38 | `#private`: @everyone denied View Channel only; Send Messages left default | Check **[auto]** `DiagnosticsFindingsTests.Check_LeavesOutAChannelThatEveryoneCannotSee` | Not reported, since no one but `admin` can see it (fixed, KI-57) | none |
| DIAG-39 | `user1` muted; a voice channel with default permissions | `user1` types, reacts or sends TTS in the voice channel's text chat **[auto]** `DiagnosticsFindingsTests.MutesCreate_DeniesSendMessagesInVoiceAndStageChat` and `DiagnosticsFindingsTests.Check_FlagsAVoiceChannelWhoseChatMutedMembersCanUse` | Refused while muted: on voice and stage channels the mute denies Connect, Speak, Send Messages, Send TTS Messages and Add Reactions (fixed, KI-56). A voice overwrite made before the fix (Connect and Speak only) is flagged until `/Mutes Refresh` | none |
| DIAG-40 | 40+ channels with findings | Check | Embed description cut to 4000 characters ending "..."; the file has everything **[auto]** `DiagnosticsReportFormatterTests.Embed_summarizes_channel_counts_and_stays_within_the_description_limit` | none |
| DIAG-41 | Change an overwrite by hand | Run Check straight away, then again 5 s later **[auto]** `DiagnosticsFindingsTests.Check_AfterAnOverwriteIsChangedByHand_ReflectsTheChange` | The second run reflects the change. Note if the first run is stale | none |

Automated in `Grimoire.Tests/Features/Moderation/Diagnostics/` (`ModerationDiagnosticsTests`,
`EffectivePermissionsTests`, `DiagnosticsReportFormatterTests`). These run on plain snapshots, not on a guild, so
DIAG-41 and how the cache is read are manual only.

## F. `/Diagnostics Ignore Add|Remove|View`

Gates: the group's gates from section D. The ignore subcommands are a nested group. Only Check is automated for the
gates, so check DIAG-63 and DIAG-64 by hand. `items` takes one or more roles or members (`items_0`, `items_1`, ...).
@everyone is skipped. Any user can be added, in the guild or not. Add and Remove reply publicly in green and post
"@admin: <same text>" to the mod log. Their warnings are ephemeral and yellow, with no mod log. View is public.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| DIAG-50 | Empty list | `admin` runs `/Diagnostics Ignore Add items_0:@mod items_1:@user1` **[auto]** `DiagnosticsTests.IgnoreAdd_StoresRolesAndMembers_AndLogs` | "@mod @user1 are now ignored by the diagnostics." | Two `DiagnosticsIgnoredItems` rows (role, member) with `SetBy` admin. `#modlog` dark purple "@admin: @mod @user1 are now ignored by the diagnostics." |
| DIAG-51 | Empty list | Add one item only **[auto]** `DiagnosticsIgnoreTests.Add_OfOneItem_SaysIs_AndStoresOneRow` | "<mention> is now ignored by the diagnostics." | One row |
| DIAG-52 | none | Add `items_0:@everyone` only **[auto]** `DiagnosticsTests.IgnoreAdd_OfOnlyEveryone_Warns` | Ephemeral "Only roles and members can be ignored. Administrator roles are always left out and @everyone cannot be." | No row, no `#modlog` |
| DIAG-53 | none | Add `@everyone` and `@mod` **[auto]** `DiagnosticsIgnoreTests.Add_WithEveryoneAmongOthers_SkipsIt_AndSaysSo` | "@mod is now ignored by the diagnostics." then "Skipped 1 item(s) that are not roles or members." | One row |
| DIAG-54 | `@user1` already ignored **[auto]** `DiagnosticsTests.IgnoreAddAgain_AndRemoveOfSomethingNotIgnored_Explain` | Add `@user1` again | Ephemeral "Those roles and members are already ignored." | No new row |
| DIAG-55 | `@user1` already ignored | Add `@user1` and `@user2` **[auto]** `DiagnosticsIgnoreTests.Add_WithOneAlreadyIgnored_AddsAndNamesOnlyTheNewOne` | Only "@user2 is now ignored by the diagnostics." | One new row |
| DIAG-56 | none | Add `@user1` twice in one call **[auto]** `DiagnosticsIgnoreTests.Add_OfTheSameMemberTwice_AddsItOnce` | "@user1 is now ignored..." once | One row |
| DIAG-57 | none | Add the `admin` role (Administrator) **[auto]** `DiagnosticsIgnoreTests.Add_OfAnAdministratorRole_IsAccepted` | Accepted: "@admin is now ignored by the diagnostics." The report still lists it only under Administrator roles (`ModerationDiagnosticsTests.Ignoring_an_administrator_role_does_not_list_it_twice`) | Row saved |
| DIAG-58 | `@mod` and `@user1` ignored **[auto]** `DiagnosticsTests.IgnoreRemove_TakesThemOff_AndViewShowsTheList` | `/Diagnostics Ignore Remove items_0:@user1` | "@user1 is no longer ignored by the diagnostics." | Row deleted; `#modlog` line |
| DIAG-59 | `@mod` not ignored **[auto]** `DiagnosticsTests.IgnoreAddAgain_AndRemoveOfSomethingNotIgnored_Explain` | Remove `@mod` | Ephemeral "None of those roles or members are ignored." | none |
| DIAG-60 | none | Remove `@everyone` only **[auto]** `DiagnosticsIgnoreTests.Remove_OfOnlyEveryone_Warns` | Ephemeral "Only roles and members can be on the ignore list." | none |
| DIAG-61 | As DIAG-58 after the remove **[auto]** same test | `/Diagnostics Ignore View` | Embed "Ignored by the diagnostics": Roles "@mod", Members "None" | none |
| DIAG-62 | Empty list | View **[auto]** `DiagnosticsIgnoreTests.View_OfAnEmptyList_ShowsNoneForBoth` | Roles "None", Members "None" | none |
| DIAG-63 | none | `mod` (no Manage Guild) runs Ignore Add, Remove and View **[auto]** `DiagnosticsIgnoreTests.Ignore_NeedsManageGuildAndTheModule` | "You do not have the required permissions to use this command." for each | No rows |
| DIAG-64 | Moderation disabled | `admin` runs Ignore Add, Remove and View **[auto]** `DiagnosticsIgnoreTests.Ignore_NeedsManageGuildAndTheModule` | "This module is disabled in this server." for each | No rows |
| DIAG-65 | `@mod` ignored, `@mod` has an allow overwrite in `#general` **[auto]** `ModerationDiagnosticsTests.Ignored_roles_are_not_reported_and_are_listed` | `/Diagnostics Check` | `mod` not reported. "Ignored roles (excluded on request): mod" in embed and file | none |
| DIAG-66 | `user2` ignored, with a member overwrite in `#general` **[auto]** `ModerationDiagnosticsTests.Ignored_members_are_not_reported_and_are_listed` | Check | `user2` not reported. "Ignored members (excluded on request, user IDs): <id>" | none |
| DIAG-67 | An ignored role, then deleted in Discord | View **[auto]** `DiagnosticsIgnoreTests.AnIgnoredRoleDeletedInDiscord_IsTakenOffTheList_AndTheModLogTold` | The role is taken off the list when Discord deletes it. `#modlog`: "The role **name** (`id`) was deleted, so it is no longer ignored by the diagnostics." (fixed, KI-62). A role deleted while the bot is offline stays on the list | Row removed |
| DIAG-68 | 60+ roles ignored (SQL; restart the bot) | View **[auto]** `DiagnosticsIgnoreTests.View_OfALongList_SplitsBetweenMentions_AndShowsThemAll` | Every item is shown. Mentions are split between fields of up to 1024 characters ("Roles (continued)"), never mid-mention; a list too long for one embed is paged with buttons (fixed, KI-62) | none |
| DIAG-69 | Settings DB unreachable | Add **[auto]** `DiagnosticsIgnoreTests.Add_WhenTheListCannotBeSaved_SaysSoPrivately` | Ephemeral "Could not save the diagnostics ignore list." | none |
| DIAG-70 | `@mod` ignored in Test-A | View in Test-B **[auto]** `DiagnosticsIgnoreTests.View_InAnotherGuild_ShowsThatGuildsList` | Test-B's own list | none |

Automated in `Grimoire.Tests/Integration/Moderation/DiagnosticsTests.cs` and
`Grimoire.Tests/Features/Moderation/Diagnostics/DiagnosticsIgnoresTests.cs`.

## G. The follow-up report from `/Mutes Create` and `/Mutes Refresh`

Gates: those of `/Mutes` (area 02). After setting the overwrites and replying, the command waits 2 seconds for the
gateway to update the cache, then posts the same report as Check as a **public** follow-up message (a new message,
not an edit). It uses the new or refreshed role and the guild's ignore list. If Discord refuses the follow-up, it is
only logged as a warning, "Could not publish moderation diagnostics for guild <id>". There is no follow-up when the
command stops early.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| DIAG-80 | No mute role set | `admin` runs `/Mutes Create` **[auto]** `DiagnosticsTests.MutesCreate_FollowsUpWithTheReport`, `MuteAdminTests.Create_MakesTheRole_SavesIt_DeniesEachChannel_AndFollowsUpWithDiagnostics` | After "Successfully created role @Muted and set permissions for channels", about 2 s later a separate public message: "Moderation diagnostics" embed and `moderation-diagnostics.txt` | none beyond MOD-79 |
| DIAG-81 | DIAG-80 done, no allow overwrites | Read the follow-up **[auto]** `DiagnosticsFindingsTests.MutesCreate_FollowUp_UsesTheNewRole_AndFindsNoProblems` | Green "No problems found." (after any Administrator line). No "No mute role" problem, so the new role was used | none |
| DIAG-82 | `mod` has an allow Send Messages overwrite in `#general` | `/Mutes Refresh` **[auto]** `DiagnosticsFindingsTests.MutesRefresh_FollowUp_ReportsAnAllowedRole_UnlessIgnored` | Follow-up reports `mod` for `#general` under mutes and locks (as DIAG-28) | none |
| DIAG-83 | As DIAG-82, `@mod` ignored | `/Mutes Refresh` **[auto]** `DiagnosticsFindingsTests.MutesRefresh_FollowUp_ReportsAnAllowedRole_UnlessIgnored` | Follow-up leaves `mod` out and lists "Ignored roles (excluded on request): mod" | none |
| DIAG-84 | Bot can't see `#private` | `/Mutes Create` **[auto]** `DiagnosticsFindingsTests.MutesCreate_FollowUp_ListsAChannelItCouldNotSet` | The yellow "...was not able to set permissions for the following channels. #private" message, then the follow-up lists `#private` with "The mute role does not deny the needed permissions here..." | none |
| DIAG-85 | Mute role configured and present | `/Mutes Create` | Ephemeral "@Muted is already the mute role. Use /Mutes Refresh to update its permissions." and no follow-up | none |
| DIAG-86 | No mute role set | `/Mutes Refresh` | "No mute role is configured." and no follow-up | none |
| DIAG-87 | Configured role deleted in Discord | `/Mutes Refresh` | "Could not find configured mute role." and no follow-up | none |
| DIAG-88 | Settings DB unreachable when saving | `/Mutes Create` | Error "Created @Muted, but couldn't save it as the mute role: ... Use /Mutes Set to try again." and no follow-up (`SettingSaveFailureTests.MutesCreate_WhenTheRoleCannotBeSaved_SaysSo_AndStops`) | none |
| DIAG-89 | A guild with 50+ channels | `/Mutes Refresh`, then compare with `/Diagnostics Check` run 10 s later | Same findings. If the follow-up lists channels as not denied that Check doesn't, the 2 s wait was too short for the gateway; record it | none |
| DIAG-90 | `/Mutes Refresh` run in `#general` | Compare visibility with `/Diagnostics Check` | The follow-up is public, Check is ephemeral. Note whether a public report is wanted | none |

Automated in `Grimoire.Tests/Integration/Moderation/DiagnosticsTests.cs` and `MuteAdminTests.cs` (DIAG-80, 85 to 88
via MOD-83 to MOD-85). The report contents in a follow-up are not checked by any test.

---

## Code observations

1. **Fixed 2026-11-10 (KI-55). A spam mute is recorded before it is known to work** (was new, med). `SpamEvents.cs:62-63` saves the sin, then
   `:66` saves the `MuteAdded` row, then `:71-73` looks up the role and `:75` grants it. If the role is gone, the
   handler returns with both rows saved. If the grant fails (no Manage Roles, or `@Muted` above the bot), it throws,
   so there is no log, no DM and the message is not deleted. If `AddMute` fails, the sin is left on its own. This is
   KI-04's problem in the spam path. SPAM-27, 28, 29.
2. **Fixed 2026-11-10 (KI-56). Muted members can still type in voice and stage channel chat** (was new, med). `PermissionValues.cs:16-17` denies only
   Connect and Speak, which `MuteAdminCommands.cs:63-64` uses for voice and stage channels. Those channels have a text
   chat that uses Send Messages. The diagnostics use the same set (`ModerationDiagnostics.cs:134-135`), so they don't
   flag it. DIAG-39.
3. **Fixed 2026-11-10 (KI-57). Hidden channels are still checked** (was new, low). `EffectivePermissions.cs:14-41` ignores View Channel. A channel no
   one but staff can see is reported if its send bits are not denied. DIAG-38.
4. **Fixed 2026-11-10 (KI-58). The bot's own role is reported as leaking** (was new, low). `ModerationDiagnostics.cs:25-27` only leaves out
   @everyone, the mute role, Administrator roles and ignored roles. The bot's role, and other bots' managed roles,
   often have allow overwrites, so they show up until someone ignores them. DIAG-37.
5. **Fixed 2026-11-10 (KI-59). Media channels are lock-checked, but `/Lock` refuses them** (was new, low). `DiagnosticsSnapshotBuilder.cs:32-33`
   treats media channels as text, and `ModerationDiagnostics.cs:91` lock-checks every text kind. `LockChannel.cs:34-35`
   does not accept media channels. DIAG-36.
6. **Fixed 2026-11-10 (KI-60). `/SpamFilter` has no module check, no `RequireGuild`, and no mod log** (was new, low).
   `SpamFilterOverrideCommands.cs:13-21` has only the Manage Channels check, so Override works with AntiSpam off.
   Every other settings change that saves posts to the mod log, but `:52-55` posts nothing. SPAM-40, SPAM-50.
7. **Fixed 2026-11-10 (KI-61). `/SpamFilter View` with no overrides is an empty embed** (was new, low). `SpamFilterOverrideCommands.cs:77-78` pages an
   empty list, which gives an embed with only its author. Other views say "None". SPAM-61.
8. **Fixed 2026-11-10 (KI-62). The diagnostics ignore list can't be fully managed** (was new, low). `DiagnosticsCommands.cs:116` cuts View at 1000
   characters, which can split a mention and hides how many are left. A deleted role can't be picked, so it stays on
   the list (harmless, as the checks only use existing roles). DIAG-67, DIAG-68.
9. **Spam mute escalation counts pardoned mutes** (new, low, maybe intended). `SpamEvents.cs:52-55` counts every Mute
   sin in the last 24 hours, pardoned or manual. Each spam mute doubles, with no cap. SPAM-05, SPAM-06.
10. **Only guild-level Manage Channels exempts staff** (intended?). `SpamTrackerModule.cs:43` checks guild permissions,
    so `mod` (Manage Messages but not Manage Channels) and channel moderators are filtered. Not listed as a bug.
    SPAM-13, SPAM-14.
11. Already tracked: `/SpamFilter Override option:Inherit` reply (KI-37, fixed) and one burst giving several mutes
    (KI-53, fixed). SPAM-02 and SPAM-42 re-check them.

## Findings

Record failures from manual runs here.
