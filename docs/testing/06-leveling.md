# 06 - Leveling

Scope: XP gain (`GainUserXp`) and reward roles, `/Level`, `/Leaderboard`, `/LevelSettings` (View, Set, LogSet),
`/Rewards` (Add, Remove, View), `/Ignore` (Add, Remove, View), `/Award`, `/Reclaim`, and
`LeaderboardRefreshBackgroundTask`.

Source: `Grimoire/Features/Leveling/`, `Grimoire.Settings/Services/SettingsModule.Leveling.*.cs`,
`Grimoire.Settings/Domain/LevelingSettingEntry.cs` and `Domain/Values/`, `Grimoire.Domain/XpHistory.cs`, and the
`leaderboard_view` migration.

Do the standard preflight from `00-environment.md` first. Set the mod log to `#modlog` and run
`/LevelSettings LogSet option:Select Channel channel:#levelup` before section A.

Commands register with snake_case names: `/level_settings`, `log_set`, and the variadic `/Ignore` options show as
`items_0`, `items_1` and so on, on both Add and Remove. This doc uses the source names.

## Behaviour summary (what the code does)

| Command | User gate | Module check | Records | Reply | Log |
|---------|-----------|--------------|---------|-------|-----|
| `/Level [user]` | none | yes | none | embed; ephemeral (see C) | none |
| `/Leaderboard option [user]` | none | yes | none | embed; ephemeral (see D) | none |
| `/LevelSettings View` | ManageGuild | **no** (shows the state) | none | public embed | none |
| `/LevelSettings Set setting value` | ManageGuild | yes | guild setting row | public | mod log |
| `/LevelSettings LogSet option [channel]` | ManageGuild | yes | log channel setting | public | mod log |
| `/Rewards Add role level [message]` | ManageGuild | yes | `RewardAdded` row | public | **leveling** log |
| `/Rewards Remove role` | ManageGuild | yes | `RewardRemoved` row | public | **leveling** log |
| `/Rewards View` | ManageGuild | yes | none | public, paged | none |
| `/Ignore Add [channel] [items]` | ManageGuild | yes | `Ignored*` rows | public, green | mod log |
| `/Ignore Remove [channel] [items]` | ManageGuild | yes | `Watched*` rows | public, green | mod log |
| `/Ignore View` | ManageGuild | yes | none | public, paged | none |
| `/Award user xp` | ManageMessages | yes | `XpHistory` Awarded row | public | mod log |
| `/Reclaim user option [amount]` | ManageMessages | yes | `XpHistory` Reclaimed row | public | mod log |

All are `RequireGuild`. The permission error is "You do not have the required permissions to use this command." The
module error is "This module is disabled in this server." The `mod` account (Manage Messages, no Manage Guild) can
use `/Award` and `/Reclaim` but none of the settings commands.

Default curve: Base 15, Modifier 50, Amount 5 XP per message, timeout 3 minutes. With these, level 2 starts at 15 XP
(3 messages), level 3 at 23, level 4 at 45, level 5 at 81. Everyone starts at level 1. To save time, set
`TextTime` to 1. With `Amount` 100, one message jumps from level 1 to level 5.

XP is stored as rows in `XpHistory` (`Type` = Earned, Awarded, Reclaimed, Migrated); a member's XP is the sum of
`Xp`. The leaderboard reads `leaderboard_view`, a materialized view that only changes when
`LeaderboardRefreshBackgroundTask` refreshes it (every 15 minutes) or you run
`REFRESH MATERIALIZED VIEW leaderboard_view;` in psql.

---

## A. XP gain (`GainUserXp`)

Gates: the Leveling module must be on. Only Default and Reply messages count; bots, webhooks (including PluralKit
proxies) and system messages earn nothing. A message is skipped if the author, one of their roles, or the channel is
ignored. If the ignore list can't be read, nothing is earned. A member earns once per timeout: the newest row's
`TimeOut` must be in the past. Each earned row stores `TimeOut` = now + the timeout. Crossing a level posts an embed
to the leveling log: author = username, "@user has leveled to level N.", footer = user id.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-01 | Defaults; `user1` has no XP | `user1` posts in `#general` | none | One Earned row, `Xp` 5, `TimeOut` about now + 3 min. **[auto]** `GainXpTests.AMessage_EarnsXp_WithATimeout` |
| LVL-02 | LVL-01 just done | `user1` posts again within 3 min | none | No new row. **[auto]** `ASecondMessage_DuringTheTimeout_EarnsNothing` |
| LVL-03 | LVL-02 done | Wait past the timeout, post again | none | Second Earned row. **[auto]** `XpGainGapTests.AMessage_AfterTheTimeoutHasPassed_EarnsAgain` |
| LVL-04 | `TextTime` set to 1 | Post, wait 1 min, post | none | Two rows, one minute apart. **[auto]** `XpGainGapTests.AOneMinuteTimeout_LetsTheMemberEarnAgainAfterAMinute` (the minute is simulated by backdating the row) |
| LVL-05 | none | A bot posts; a webhook posts | none | No rows. **[auto]** `Messages_ThatShouldNotCount_EarnNothing("bot")` |
| LVL-06 | none | `user1` replies to a message | none | Earned row (Reply counts) **[auto]** `XpGainGapTests.AReply_EarnsXp` |
| LVL-07 | none | Pin a message (system message), or `user1` runs a slash command | none | No row for the system or command message. **[auto]** `XpGainGapTests.ASystemMessageOrASlashCommand_EarnsNothing` |
| LVL-08 | `user1` ignored (`/Ignore Add items:user1`) | `user1` posts | none | No row. **[auto]** `Messages_ThatShouldNotCount_EarnNothing("ignored member")` |
| LVL-09 | `mod` role ignored | `mod` posts | none | No row. **[auto]** `("ignored role")` |
| LVL-10 | `#ignored` ignored | `user1` posts in `#ignored`, then in `#general` | none | No row for `#ignored`; a row for `#general`. **[auto]** `("ignored channel")`, `AnIgnoredChannel_DoesNotAffectOtherChannels` |
| LVL-11 | `#ignored` ignored; a thread under it | `user1` posts in the thread | none | **Expected:** no row. **Actual:** a row, because only the exact channel id is matched (new, see Code observations 2) **[auto]** `XpGainGapTests.AThreadUnderAnIgnoredChannel_EarnsNothing` (skipped, KI-73) |
| LVL-12 | A category ignored with `/Ignore Add channel:<category>` | `user1` posts in a channel in that category | none | **Expected:** no row, or the category refused. **Actual:** a row (new, Code observations 2) |
| LVL-13 | Leveling disabled | `user1` posts | none | No row. **[auto]** `("module off")` |
| LVL-14 | `user1` has 10 XP (2 messages) | Third message after the timeout | `#levelup`: "@user1 has leveled to level 2." | Row; level embed. **[auto]** `CrossingALevel_PostsToTheLevelingLog` |
| LVL-15 | `user1` at 15 XP | Fourth message | none | Row; no level embed. **[auto]** `StayingOnALevel_PostsNothing` |
| LVL-16 | `Amount` 100; `user1` has 0 XP | One message | One embed: "has leveled to level 5." (no embeds for 2 to 4) | Row of 100. **[auto]** `XpGainGapTests.JumpingSeveralLevels_PostsOneLevelUp_ForTheNewLevel` |
| LVL-17 | Leveling log off (`LogSet option:Off`) | Cross a level | No level embed anywhere | Row saved. **[auto]** `XpGainGapTests.WithTheLevelingLogOff_ALevelUpIsPostedNowhere_ButTheXpIsSaved` |
| LVL-18 | `user1` and `user2` both earning | Both post | Each earns separately | Two rows, per user. **[auto]** `XpGainGapTests.TwoMembers_EachEarnTheirOwnXp` |
| LVL-19 | `user1` posts two messages at the same instant (paste fast, or two clients) | Two messages within ~100 ms | none | **Expected:** one row. **Actual (suspected):** possibly two rows, because the timeout check and the insert are not atomic (new, Code observations 1) |
| LVL-20 | `pk` with PluralKit | `pk` posts a proxied message | none | One row, from the original message; none from the webhook (more in area 09). **[auto]** `PluralKitAcrossFeaturesTests.ProxiedMessages_EarnXpOnlyForTheMember_OncePerTimeout` Webhook half also in `XpGainGapTests.AWebhookCopyOfAMessage_EarnsNothing_TheOriginalEarnsOnce`. |
| LVL-21 | XP in Test-A only | `user1` posts in Test-B | none | Test-B row is separate; Test-A total unchanged. **[auto]** `XpGainGapTests.XpInOneGuild_IsSeparateFromAnother` |

Automated in `Grimoire.Tests/Integration/Leveling/GainXpTests.cs`; `BuildEarnedXp` in
`Grimoire.Tests/Features/Leveling/Events/GainUserXpTests.cs`.

## B. Reward roles on XP gain

Gates: runs after every earned row, not only on level up, so a reward missed earlier is retried on the next gain.
Rewards at or below the member's level that they don't hold are granted one role at a time. A role the bot can't
grant (Discord returns Unauthorized) is skipped and reported as a guild issue in `#modlog`:
"I could not give the level reward roles @role." with "Give my role the **Manage Roles** permission and move it above
the reward roles in Server Settings > Roles." Each granted reward with a message is sent by DM: author
"Congratulations on earning <role>!", footer "Message from the moderators of <guild>.". A `\n` in the message
becomes a line break; other backslashes stay. Granted roles are logged in the leveling log: title = username,
"@user has earned @role ...", footer = user id. A reward whose role was deleted is removed, recorded as removed by the
bot, and reported once: "These level rewards were removed because their roles were deleted:" with
"**Level N:** role `id`".

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-30 | Reward `@L2` at level 2 with message "Well done\nfriend"; `user1` at 10 XP | Third message | `user1` gets `@L2`; DM with "Well done" and "friend" on two lines; `#levelup` level embed and "@user1 has earned @L2" | Audit log "Level reward". **[auto]** `GainXpTests.ReachingARewardLevel_GrantsTheRole_DmsTheMessage_AndLogsIt`, `ARewardMessageWithBackslashes_IsSentAsWritten_ExceptForLineBreaks` |
| LVL-31 | Reward at level 5; `user1` at level 2 | Post | No role | none. **[auto]** `ARewardForAHigherLevel_IsNotGrantedYet` |
| LVL-32 | Rewards at 2 and 3; `Amount` 100 | One message from 0 XP | Both roles; one "has earned @L2 @L3" log | none. **[auto]** `RewardGrantTests.TwoRewardsReachedAtOnce_AreBothGranted_AndLoggedInOneLine` |
| LVL-33 | Reward with no message | Reach it | Role granted, no DM | Log line only. **[auto]** `RewardGrantTests.ARewardWithoutAMessage_IsGranted_WithoutADm` |
| LVL-34 | `user1` has DMs closed | Reach a reward with a message | Role granted, no DM | Info log "Failure to send reward message"; no alert. **[auto]** `RewardGrantTests.ARewardForAMemberWithClosedDms_IsStillGranted_AndOnlyLoggedAsInformation` |
| LVL-35 | `user1` already holds the reward role | Post | Nothing new; no log | none. **[auto]** `RewardGrantTests.ARewardTheMemberAlreadyHolds_IsNotGrantedOrLoggedAgain` |
| LVL-36 | `user1` at level 3 had `@L2` removed by hand | Post after the timeout | `@L2` is granted again | Log line. **[auto]** `RewardGrantTests.ARewardRoleRemovedByHand_IsGrantedAgain_OnTheNextGain` |
| LVL-37 | Reward role moved above the bot after it was added | Reach it | No role; `#modlog` guild issue "I could not give the level reward roles @role." | No "has earned" log. **[auto]** `ARewardRoleTheBotCannotGrant_IsReported`, `ARewardRoleTheBotCannotGrant_IsNotLoggedAsEarned` (KI-41, fixed) |
| LVL-38 | Bot role loses Manage Roles | Reach a reward | Same guild issue as LVL-37 | none. **[auto]** `RewardGrantTests.ARewardWhileTheBotLacksManageRoles_IsReported` |
| LVL-39 | Rewards at 2 (grantable) and 3 (above the bot) | Reach level 3 | `@L2` granted and logged; issue names only `@L3` | **[auto]** `ARewardTheBotCannotGrant_DoesNotBlockTheOthers` |
| LVL-40 | `user1` is a Server Booster (managed role) | Reach a reward | Role granted; booster role kept | **[auto]** `AMemberWithAManagedRole_StillEarnsTheReward` |
| LVL-41 | Rewards at 2 and 3; delete the level 2 role in Discord | `user1` posts | Level 3 role granted; `#modlog` issue "These level rewards were removed because their roles were deleted:" "**Level 2:** role `id`" | `RewardRemoved` row by the bot. **[auto]** `ARewardForADeletedRole_IsRemoved_TheModeratorsAreTold_AndTheOthersAreGranted` |
| LVL-42 | LVL-41 done | Post again | No second issue | No second row. **[auto]** `ARewardForADeletedRole_IsOnlyRemovedOnce`, `ASecondRemovalOnTheSameDay_IsStillShown` |
| LVL-43 | `@everyone` added as a reward (see LVL-106) | Reach it | **Expected:** refused at Add. **Actual (suspected):** Discord refuses the grant with an error the loop doesn't catch, so the handler throws on every gain and later rewards in the loop are skipped (new, Code observations 3) | Check console for an unhandled exception. **[auto]** `RewardGrantTests.AnEveryoneReward_DoesNotStopTheOtherRewards` (skipped, KI-75; confirmed against the fake's 400) |
| LVL-44 | Reward reached through `/Award` only | Award enough XP | No role yet; the role comes on the member's next earned message | none. **[auto]** `RewardGrantTests.ARewardReachedThroughAward_IsGrantedOnTheNextEarnedMessage` Also `AwardReclaimTests.Award_GrantsTheRewardOnlyOnTheNextEarnedMessage`. |

Automated in `Grimoire.Tests/Integration/Leveling/GainXpTests.cs`.

## C. `/Level [user]`

Gates: `RequireGuild`, `RequireModuleEnabled(Leveling)`. Everyone can use it. The reply is ephemeral unless the invoker
has Manage Messages or runs it in the user command channel (`/GeneralSettings UserCommands`). The embed: title
"Level and EXP for <display name>", fields XP, Level, Progress (`xp into level/xp for the level`) and Next Reward
("@role\n at level N", "None", or "Unavailable" when the rewards can't be read). Color and avatar come from the member,
or from the user if they left.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-50 | `user1` has no XP | `user1` runs `/Level` | XP 0, Level 1, Progress 0/15, Next Reward None | none. **[auto]** `LevelAndLeaderboardTests.Level_WithNoXp_ShowsLevelOne` |
| LVL-51 | `user1` has 20 XP | `/Level` | XP 20, Level 2, Progress 5/8 | **[auto]** `Level_ShowsTheMembersXpAndProgress` |
| LVL-52 | `user2` has XP | `user1` runs `/Level user:user2` | `user2`'s numbers and name | **[auto]** `Level_ForAnotherUser_ShowsTheirXp` |
| LVL-53 | Rewards at 3 and 5; `user1` at level 2 | `/Level` | Next Reward "@L3\n at level 3" | **[auto]** `Level_ShowsTheNextRewardAndItsLevel`, `Level_ShowsTheNearestReward` (KI-42, fixed) |
| LVL-54 | No user command channel | `user1` runs `/Level` in `#general`; `mod` does the same | `user1`'s reply is ephemeral; `mod`'s is public | **[auto]** `Level_IsEphemeral_UnlessAModeratorOrInTheUserCommandChannel` |
| LVL-55 | User command channel `#general` | `user1` runs `/Level` there | Public | **[auto]** as LVL-54 |
| LVL-56 | A user who left, with XP | `/Level user:<them>` | Their XP; username, user avatar, banner color or blurple | none. **[auto]** `LevelAndLeaderboardGapTests.Level_ForAUserWhoLeft_ShowsTheirXp_WithTheirUserProfile` |
| LVL-57 | The nearest reward's role was deleted, a later reward exists, and no XP gain has cleaned it up | `/Level` | **Expected:** the later reward. **Actual:** "None" until the next XP gain removes the old reward (new, Code observations 7) | none. **[auto]** `LevelAndLeaderboardGapTests.Level_WhenTheNearestRewardsRoleWasDeleted_ShowsTheNextReward` (skipped, KI-79) |
| LVL-58 | Discord REST failing for role lookups | `/Level` with a next reward | The command fails and is reported, not "None" | **[auto]** `RewardCommandsTests.Level_DuringADiscordOutage_FailsInsteadOfShowingTheRewardAsMissing` |
| LVL-59 | Leveling disabled | `/Level` | "This module is disabled in this server." | **[auto]** `LevelingCommands_NeedTheModule("level")` |

Automated in `Grimoire.Tests/Integration/Leveling/LevelAndLeaderboardTests.cs`.

## D. `/Leaderboard option [user]` and `LeaderboardRefreshBackgroundTask`

Gates: `RequireGuild`, `RequireModuleEnabled(Leveling)`; ephemeral rules as `/Level`. Options: Top (the 15 highest),
Me, User (needs `user`). Me and User show 15 rows from five above the member, shifted up near the bottom. Rows read
"**rank** @user **XP:** n", title "LeaderBoard", footer "Total Users N". The view ranks every user with any
`XpHistory` row in the guild, including members who left, ignored members, and users whose XP is 0 after a reclaim.
Ties get distinct ranks in no set order. The refresh task runs
`REFRESH MATERIALIZED VIEW CONCURRENTLY leaderboard_view` every 15 minutes.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-60 | Three users with XP; view refreshed | `/Leaderboard option:Top` | Ranked by XP, "Total Users 3" | **[auto]** `Leaderboard_Top_RanksMembersByXp`; `GetLeaderboardTests.GetTopLeaderboardAsync_RanksUsersByXpDescending` |
| LVL-61 | 20 users with XP | Top | 15 rows, "Total Users 20" | **[auto]** `GetTopLeaderboardAsync_MoreThanFifteenUsers_ReturnsOnlyTopFifteen_ButFullTotalCount` |
| LVL-62 | `user1` ranked 10th of 30 | `user1` runs `option:Me` | Ranks 5 to 19 | **[auto]** `Leaderboard_User_ShowsTheMembersNeighbourhood` |
| LVL-63 | `user2` ranked 29th of 30 | `option:User user:user2` | Ranks 16 to 30 | **[auto]** `Leaderboard_User_NearTheBottom_StillShowsFifteen` |
| LVL-64 | `user1` has no XP | `option:Me` | Yellow "User not found on the leaderboard" | **[auto]** `Leaderboard_Me_WithNoXp_SaysNotFound` |
| LVL-65 | none | `option:User` with no user | Yellow "You must specify a user when selecting the 'User' option." It is public even where the reply would be ephemeral, since it is sent before the defer (Code observations 10) | **[auto]** `Leaderboard_UserOptionWithoutAUser_IsRejected` |
| LVL-66 | none | `option:Top user:user2` | The top list; `user` is ignored | none. **[auto]** `LevelAndLeaderboardGapTests.Leaderboard_Top_WithAUser_IgnoresTheUser` |
| LVL-67 | No XP rows in the guild | Top | Empty "LeaderBoard" embed, "Total Users 0" | **[auto]** `GetTopLeaderboardAsync_NoUsers_ReturnsEmptyTextAndZeroCount` |
| LVL-68 | XP in Test-A and Test-B | Top in Test-A | Test-A users only | **[auto]** `GetTopLeaderboardAsync_OnlyCountsUsersInRequestedGuild`, `GetUserCenteredLeaderboardAsync_OtherGuildUser_ReturnsNotFound` |
| LVL-69 | View refreshed; `user2` then earns or is awarded XP | Top straight away | Old totals; `user2` missing if new | **[auto]** `Leaderboard_PicksUpNewXp_AfterTheRefreshTask` |
| LVL-70 | LVL-69 done | Wait for the refresh tick (up to 15 min after startup, then every 15 min), or run the SQL | New totals | Debug log of the task run. **[auto]** as LVL-69 |
| LVL-71 | `user1` reclaimed to 0; view refreshed | Top | `user1` still listed with XP 0 | none. **[auto]** `LevelAndLeaderboardGapTests.Leaderboard_KeepsAMemberReclaimedToZero` |
| LVL-72 | Bot restarted | Check when the first refresh runs | About 15 min after startup; the random start delay doesn't move it (Code observations 8) | Startup log "Starting Background task LeaderboardRefreshBackgroundTask with delay ..." |
| LVL-73 | DB down during a tick | Stop the DB container through a tick | Nothing in Discord | Error log; `#alerts` warning "LeaderboardRefreshBackgroundTask failed 1 time(s) in a row"; urgent after 3 |
| LVL-74 | Leveling disabled | `/Leaderboard option:Top` | "This module is disabled in this server." | **[auto]** `LevelingCommands_NeedTheModule("leaderboard")` |

Automated in `Grimoire.Tests/Integration/Leveling/LevelAndLeaderboardTests.cs` and
`Grimoire.Tests/Features/Leveling/UserCommands/GetLeaderboardTests.cs`.

## E. `/LevelSettings View | Set | LogSet`

Gates: `RequireGuild` and Manage Guild for all three. Set and LogSet also need the Leveling module; View doesn't, so
it can show the module is off. Set limits: TextTime 1 to 60 minutes, Base 1 to 500, Modifier 1 to 200, Amount 1 to
100. Discord enforces a minimum of 1; the bot checks the maximum. The Set reply uses the option's code name
("TextTime"), not its display name. Setting the value it already has still replies and logs. Set and LogSet log to the
mod log, not the leveling log.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-80 | Fresh guild | `admin` runs `/LevelSettings View` | "Current Level System Settings": "**Module Enabled:** True", "**Text Time:** 3 minutes.", "**Base:** 15", "**Modifier:** 50", "**Reward Amount:** 5", "**Log-Channel:** None" | none. **[auto]** `LevelSettingsTests.View_ShowsTheModuleTimeoutAndLogChannel`, `View_ShowsTheNumbersPlainly` (KI-43, fixed) |
| LVL-81 | Log set to `#levelup` | View | "**Log-Channel:** #levelup" | **[auto]** `LogSet_StoresTheChannel_ViewShowsIt_AndOffClearsIt` |
| LVL-82 | Log set to a channel that was then deleted | View | **Expected:** the id, or a note it's gone. **Actual:** "**Log-Channel:** " is blank (new, Code observations 6) | none. **[auto]** `LevelSettingsGapTests.View_WithADeletedLogChannel_ShowsItsIdOrSaysItIsGone` (skipped, KI-78) |
| LVL-83 | none | `/LevelSettings Set setting:TextTime value:10` | "Updated TextTime level setting to 10" | Setting row; `#modlog` "@admin updated TextTime level setting to 10". **[auto]** `Set_StoresTheValue_AndLogsIt(TextTime, 10)` |
| LVL-84 | none | Set Base 30, Modifier 75, Amount 12 | "Updated Base level setting to 30" and so on | Rows; `#modlog` lines; View shows the new values. **[auto]** `Set_StoresTheValue_AndLogsIt` |
| LVL-85 | none | TextTime 61 | Red ephemeral "Xp timeout period must be a value between 1 and 60 in minutes." | No change. **[auto]** `Set_OutOfRange_IsRejected_AndNothingChanges` |
| LVL-86 | none | Base 501, Modifier 201, Amount 101 | "Level scaling base must be a value between 1 and 500.", "Level scaling modifier must be a value between 1 and 200.", "Xp gain amount must be a value between 1 and 100." | No change. **[auto]** as LVL-85 |
| LVL-87 | none | `value:0` | Discord's client refuses it (min 1) | none |
| LVL-88 | Amount 5 | Set Amount 5 again | Normal reply | `#modlog` line again. **[auto]** `LevelSettingsGapTests.Set_TheSameValueAgain_StillRepliesAndLogs` |
| LVL-89 | TextTime 10 | `user1` posts twice 5 min apart | Only the first earns | One row; `TimeOut` now + 10 min. **[auto]** `LevelSettingsGapTests.Set_TextTime_GivesThatTimeout` |
| LVL-90 | Base 30 | `/Level` for a member at 20 XP | Level 1, Progress 20/30 | none. **[auto]** `LevelSettingsGapTests.Set_Base_ChangesTheLevelCurve` |
| LVL-91 | none | `/LevelSettings LogSet option:Select Channel channel:#levelup` | "Updated the level log to #levelup" | Setting; `#modlog` "@admin updated the level log to #levelup." **[auto]** `LogSet_StoresTheChannel_ViewShowsIt_AndOffClearsIt` |
| LVL-92 | none | `option:Current Channel` in `#levelup` | Same as LVL-91 | Same. **[auto]** `LevelSettingsGapTests.LogSet_CurrentChannel_UsesTheChannelItRanIn` |
| LVL-93 | Log set | `option:Off` | "Disabled the level log." | Cleared; `#modlog` "@admin disabled the level log." **[auto]** `LevelSettingsGapTests.LogSet_Off_IsLogged` |
| LVL-94 | none | `option:Select Channel` with no channel | Red ephemeral "Select a channel when using the Select Channel option." | No change. **[auto]** `LevelSettingsGapTests.LogSet_SelectChannel_WithoutAChannel_IsRefused` |
| LVL-95 | Bot can't send in `#private` | Select `#private` | Red ephemeral "@Grimoire doesn't have permission to send messages in that channel." | No change. **[auto]** `LogSet_WhereTheBotCannotSend_IsRefused` |
| LVL-96 | Bot can send but lacks Embed Links in a channel | Select it | "@Grimoire also needs **Embed Links** in that channel to post these logs." | No change. **[auto]** `LevelSettingsGapTests.LogSet_WhereTheBotCannotEmbedLinks_IsRefused` |
| LVL-97 | none | Select a category or forum | "#x can't hold messages. Pick a text channel." | No change. **[auto]** `LevelSettingsGapTests.LogSet_ACategoryOrForum_IsRefused` |
| LVL-98 | none | `mod` (no Manage Guild) runs View, Set, LogSet | "You do not have the required permissions to use this command." | none. **[auto]** `LevelSettings_NeedManageGuild` |
| LVL-99 | Leveling disabled | View, then Set and LogSet | View shows "**Module Enabled:** False"; Set and LogSet say "This module is disabled in this server." | **[auto]** `LevelSettings_WithLevelingDisabled_ViewShowsIt_AndChangesAreRefused` |

Automated in `Grimoire.Tests/Integration/Leveling/LevelSettingsTests.cs`.

## F. `/Rewards Add | Remove | View`

Gates: `RequireGuild`, `RequireModuleEnabled(Leveling)`, Manage Guild for all three (KI-45, fixed). Add refuses a
managed role and a role at or above the bot's top role. It does not check that the bot has Manage Roles. `level` is
0 or more; `message` is up to 4096 characters, trimmed, and may not be blank. Adding a role that is already a reward
updates it. Add and Remove log to the **leveling** log. Errors are red, public, and start "Was not able to update the
rewards for the server:" (Code observations 9).

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-100 | none | `admin` runs `/Rewards Add role:@L2 level:2 message:"Well done"` | "Successfully updated the rewards to include @L2 at level 2." | `RewardAdded` row; same text in `#levelup`. **[auto]** `RewardCommandsTests.Add_StoresTheReward_AndLogsIt` |
| LVL-101 | LVL-100 done | Add `@L2` at level 4 with no message | "... at level 4." | New row; View shows one entry for `@L2`, no message. **[auto]** `Add_ForARoleThatIsAlreadyAReward_UpdatesIt` |
| LVL-102 | none | Add `level:0` | Accepted | Granted on any member's next earned message. **[auto]** `RewardCommandGapTests.Add_AtLevelZero_IsGrantedOnTheNextEarnedMessage` |
| LVL-103 | none | Add the `higher` role | "Was not able to update the rewards for the server:\nGrimoire will not be able to apply this reward role because the role has the same or a higher rank than it does." | No row. **[auto]** `Add_ARoleAboveTheBot_IsRefused` |
| LVL-104 | none | Add the bot's own role | Same as LVL-103 | **[auto]** `Add_ARoleLevelWithTheBot_IsRefused` |
| LVL-105 | none | Add a managed role (Server Booster, a bot's role) | "...:\nThat role is managed by an integration, such as a bot or Server Booster, so it can't be given out." | No row. **[auto]** `Add_AManagedRole_IsRefused` |
| LVL-106 | none | Add `@everyone` | **Expected:** refused. **Actual:** accepted (new, Code observations 3; see LVL-43) | Row. **[auto]** `RewardGrantTests.AddingEveryoneAsAReward_IsRefused` (skipped, KI-75) |
| LVL-107 | Bot role lacks Manage Roles | Add `@L2` | **Expected:** a warning that the bot can't grant roles. **Actual:** accepted; the problem shows only at grant time (LVL-38) (new, Code observations 4) | Row. **[auto]** `RewardGrantTests.AddingARewardWhileTheBotLacksManageRoles_Warns` (skipped, KI-76) |
| LVL-108 | none | `message:"   "` | "...:\nReward message must be 1–4096 non-whitespace characters." | No row. **[auto]** `RewardCommandGapTests.Add_WithABlankMessage_IsRefused` |
| LVL-109 | none | Negative level | Discord's client refuses it | none |
| LVL-110 | Rewards `@L2` and `@L4` | `/Rewards View` | "Rewards": "Level:2 Role:@L2 Reward Message: Well done", "Level:4 Role:@L4", ordered by level | **[auto]** `View_ListsEveryReward` |
| LVL-111 | No rewards | View | "Rewards" / "This server does not have any rewards." | **[auto]** `View_WithNoRewards_SaysSo` |
| LVL-112 | A reward whose role was deleted, not yet cleaned up | View | Shown as a mention of the id | **[auto]** `View_ListsADeletedRoleByItsId` |
| LVL-113 | Several rewards with 4096-character messages | View | Paged with buttons; each message whole | **[auto]** `View_WithLongRewardMessages_IsPaged_WithEachMessageWhole`, `PaginationTests.RewardsView_SecondPage_ShowsTheNextRewardWhole` |
| LVL-114 | `@L2` is a reward | `/Rewards Remove role:@L2` | "@admin removed @L2 reward" | `RewardRemoved` row; same text in `#levelup`; members keep the role. **[auto]** `Remove_DeletesTheReward_AndLogsIt` |
| LVL-115 | `@L3` is not a reward | Remove `@L3` | "...:\nDid not find a saved reward for @L3." | No row. **[auto]** `Remove_OfARoleThatIsNotAReward_IsRefused` |
| LVL-116 | none | `mod` runs Add, Remove, View | "You do not have the required permissions to use this command." | none. **[auto]** `Add_ByAMemberWithoutManageGuild_IsRefused`, `Remove_ByAMemberWithoutManageGuild_IsRefused` |
| LVL-117 | Leveling disabled | Add, Remove, View | "This module is disabled in this server." | **[auto]** `Rewards_NeedTheModule`, `Remove_NeedsTheModule` |
| LVL-118 | Rewards in Test-B | View in Test-A | Test-A only | none. **[auto]** `LevelingGuildIsolationTests.RewardsInB_DoNotShowInA` |

Automated in `Grimoire.Tests/Integration/Leveling/RewardCommandsTests.cs`.

## G. `/Ignore Add | Remove | View`

Gates: `RequireGuild`, `RequireModuleEnabled(Leveling)`, Manage Guild (KI-45, fixed). Add and Remove take an optional
`channel` and up to 24 users or roles (KI-44, fixed). The `channel` option accepts any channel type. Each call appends
rows; the newest row per id wins. Remove does not check the item was ignored. Replies are green and are logged to the
mod log.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-120 | none | `admin` runs `/Ignore Add items_0:user1 items_1:@mod-role` | "@user1 @mod-role are now ignored for xp gain." (any order) | `IgnoredMember` and `IgnoredRole` rows; `#modlog` same text; `user1` earns nothing. **[auto]** `IgnoreAndAwardTests.IgnoreAdd_StopsXp_AndIsLogged` |
| LVL-121 | none | `/Ignore Add channel:#ignored` | "#ignored are now ignored for xp gain." | `IgnoredChannel` row. **[auto]** `Ignore_AChannel_CanBeAddedAndRemoved`, `Ignore_OffersAChannelOption` |
| LVL-122 | none | Add with no options | Yellow "Could not parse any ids from the submitted values." | none. **[auto]** `IgnoreAdd_WithNothingSelected_SaysSo` |
| LVL-123 | `user1` ignored | Add `user1` again | Same reply | A second row; no error. **[auto]** `IgnoreCommandTests.Add_SomeoneAlreadyIgnored_RepliesTheSame_AndAddsARow` |
| LVL-124 | `user1` ignored | `/Ignore Remove items_0:user1` | "@user1 are no longer ignored for xp gain." | `WatchedMember` row; `user1` earns again. **[auto]** `IgnoreRemove_LetsXpThroughAgain`, `IgnoreRemove_SaysWhatIsNoLongerIgnored` (KI-46, fixed) |
| LVL-125 | `#ignored` ignored | Remove `channel:#ignored` | "#ignored are no longer ignored for xp gain." | `WatchedChannel` row. **[auto]** `Ignore_AChannel_CanBeAddedAndRemoved` |
| LVL-126 | `user2` not ignored | Remove `user2` | Same "no longer ignored" reply | A `WatchedMember` row; no error. **[auto]** `IgnoreCommandTests.Remove_SomeoneNotIgnored_StillReplies_AndRecordsIt` |
| LVL-127 | One of each ignored | `/Ignore View` | "Ignored Channels Roles and Users.": "**Channels**", "**Roles**", "**Users**" sections with mentions | none. **[auto]** `IgnoreView_ListsChannelsRolesAndUsers` |
| LVL-128 | Nothing ignored | View | "This server does not have any ignored channels, roles or users." | **[auto]** `IgnoreView_WithNothingIgnored_SaysSo` |
| LVL-129 | Only users ignored | View | Empty Channels and Roles headings still shown | none. **[auto]** `IgnoreCommandTests.View_WithOnlyUsers_StillShowsTheEmptyHeadings` |
| LVL-130 | Hundreds of ignored items | View | Paged | **[auto]** `IgnoreView_WithHundredsOfItems_IsSplitAcrossPages`, `PaginationTests.IgnoreView_TheInvokerPagesForwardBackAndToEitherEnd`, `Stop_KeepsThePage_AndDisablesTheButtons`, `AShortList_HasNoButtons` |
| LVL-131 | none | Add a category or voice channel | Accepted. A category has no effect on its channels (LVL-12) | `IgnoredChannel` row. **[auto]** `XpGainGapTests.IgnoringACategory_IsRefusedOrWarned` (skipped, KI-73) |
| LVL-132 | none | `mod` runs Add, Remove, View | "You do not have the required permissions to use this command." | **[auto]** `IgnoreAdd_ByAMemberWithoutManageGuild_IsRefused` (Add only) |
| LVL-133 | Leveling disabled | Add, Remove, View | "This module is disabled in this server." | none. **[auto]** `IgnoreCommandTests.WithLevelingDisabled_IsRefused` |
| LVL-134 | `user1` ignored in Test-A | `user1` posts in Test-B | Earns in Test-B | Test-B row. **[auto]** `LevelingGuildIsolationTests.AMemberIgnoredInA_StillEarnsInB` |

Automated in `Grimoire.Tests/Integration/Leveling/IgnoreAndAwardTests.cs`.

## H. `/Award user xp` and `/Reclaim user option [amount]`

Gates: `RequireGuild`, `RequireModuleEnabled(Leveling)`, Manage Messages. There is no rank check: a moderator can
award or reclaim anyone, including themselves and members above them. `/Award` needs a member and `xp` of at least 1,
with no maximum. It doesn't post a level-up or grant rewards; that happens on the member's next earned message.
`/Reclaim` takes any user, including one who left. Options: "Take all their xp." or "Take a specific amount." (never
more than they have). It doesn't remove reward roles. The Reclaimed row stores no moderator (Code observations 5).
Both log to the mod log.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-140 | none | `mod` runs `/Award user:user1 xp:50` | "@user1 has been awarded 50 xp." | Awarded row, `AwarderId` = `mod`; `#modlog` "@user1 has been awarded 50 xp by @mod." **[auto]** `IgnoreAndAwardTests.Award_AddsXp_AndIsLogged` |
| LVL-141 | `user1` at 10 XP | Award 100 | Reply; no level embed, no reward role | Row. Next earned message posts the level embed and grants rewards (LVL-44). **[auto]** `AwardReclaimTests.Award_GrantsTheRewardOnlyOnTheNextEarnedMessage`; the level-up is never posted: `Award_LevelUpIsAnnouncedOnTheNextEarnedMessage` (skipped, KI-109) |
| LVL-142 | none | `xp:0` or negative | Discord's client refuses it (min 1) | none |
| LVL-143 | none | Award `2147483647` twice | Both accepted | Total 4294967294; `/Level` still answers. **[auto]** `AwardReclaimTests.Award_TheMaximumTwice_AddsUp_AndLevelStillAnswers` |
| LVL-144 | none | `mod` awards themselves; `mod` awards `admin` and `higher` | All accepted | Rows (no rank check, by design so far). **[auto]** `AwardReclaimTests.Award_HasNoRankCheck` |
| LVL-145 | A user who left | `/Award user:<them>` | Argument error; nothing saved | none. **[auto]** `AwardReclaimTests.Award_ToAUserWhoLeft_IsRefused_AndNothingIsSaved` ("Failed to parse user.") |
| LVL-146 | `user1` ignored | Award `user1` | Accepted; ignores only stop earning | Row. **[auto]** `AwardReclaimTests.Award_ToAnIgnoredMember_IsAccepted` |
| LVL-147 | `user1` has 50 XP | `/Reclaim user:user1 option:Take a specific amount. amount:20` | "20 xp has been taken from @user1." | Reclaimed row −20; total 30; `#modlog` "20 xp has been taken from @user1 by @mod." **[auto]** `Reclaim_TakesXp_NeverMoreThanTheyHave(Amount, 20, 20, 30)` |
| LVL-148 | `user1` has 50 XP | `amount:80` | "50 xp has been taken from @user1." | Total 0. **[auto]** `(Amount, 80, 50, 0)` |
| LVL-149 | `user1` has 50 XP | `option:Take all their xp.` | "50 xp has been taken from @user1." | Total 0. **[auto]** `(All, 0, 50, 0)` |
| LVL-150 | none | `option:All amount:10` | All XP taken; `amount` ignored | **[auto]** `ReclaimUserXpTests.AllOption_NonZeroAmount_StillSucceeds_AmountIsIgnored` |
| LVL-151 | `user1` has 50 XP | `option:Amount` with no amount, or 0 | Red ephemeral "Specify an amount greater than 0" | No row. **[auto]** `Reclaim_AnAmountOfZero_IsRejected`, `ReclaimUserXpTests.AmountOption_ZeroAmount_Fails` |
| LVL-152 | `user1` has no XP, or 0 after a reclaim | Reclaim All | Red ephemeral "@user1 has no xp to take." | No row. **[auto]** `Reclaim_FromAMemberWithNoXp_SaysSo` |
| LVL-153 | `user1` at level 3 with `@L2` and `@L3` | Reclaim All | Reply; roles kept | `/Level` shows level 1. **[auto]** `AwardReclaimTests.ReclaimAll_KeepsTheRewardRoles_AndTheLevelDropsToOne` |
| LVL-154 | A user who left, with XP | `/Reclaim user:<them> option:All` | Works | Row. **[auto]** `AwardReclaimTests.Reclaim_FromAUserWhoLeft_Works` |
| LVL-155 | none | `user1` (no Manage Messages) runs either | "You do not have the required permissions to use this command." | none. **[auto]** `AwardAndReclaim_NeedManageMessages` |
| LVL-156 | Leveling disabled | Run either | "This module is disabled in this server." | none. **[auto]** `AwardReclaimTests.WithLevelingDisabled_IsRefused` |
| LVL-157 | XP in Test-B | Reclaim All in Test-A | Only Test-A XP counted | none. **[auto]** `LevelingGuildIsolationTests.ReclaimAllInA_OnlyCountsAsXp` |

Automated in `Grimoire.Tests/Integration/Leveling/IgnoreAndAwardTests.cs` and
`Grimoire.Tests/Features/Leveling/Awards/ReclaimUserXpTests.cs`.

## I. Cross-checks for this area

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| LVL-170 | Settings changed by SQL | Post a message | Old settings until restart (cache) | none. **[auto]** `LevelingCacheTests.ASettingChangedInTheDatabase_IsOnlySeenAfterARestart` |
| LVL-171 | `/LevelSettings Set` | Post straight after | New settings apply at once (cache cleared on save) | none. **[auto]** `LevelingCacheTests.ASettingSetByCommand_AppliesToTheNextMessage` |
| LVL-172 | `/Rewards Add` then `/Ignore Add` | XP gain straight after | Both apply at once | none. **[auto]** `LevelingCacheTests.ARewardAndAnIgnoreAddedByCommand_BothApplyAtOnce` |
| LVL-173 | Leveling disabled, then enabled | Post | Earns again; the timeout from before still applies | none. **[auto]** `LevelingCacheTests.TurningTheModuleOffAndOn_KeepsTheTimeout_AndOthersEarnAgain` |
| LVL-174 | Run the area with `#levelup` and `#modlog` both unset | Every case above | Commands still reply; nothing is posted | No errors in the console |

## Code observations

1. **Two messages at once can both earn XP** (new, low, suspected). `GainUserXp.cs:62-90` reads the newest `TimeOut`,
   then inserts later. Nothing makes that atomic, and the key `(UserId, GuildId, TimeOut)` differs by microseconds,
   so two messages handled in parallel both pass. Impact: a fast typist or a script earns more than once per timeout.
   Confirm with LVL-19.
2. **An ignored channel doesn't cover its threads or category** (new, med). `SettingsModule.Leveling.Ignores.cs:29`
   compares the message's channel id only, and `GainUserXp.cs:57` passes the thread's id. `/Ignore Add channel:`
   (`IgnoreCommandGroup.AddIgnore.cs:24-25`) accepts categories and forums without a warning, but ignoring them does
   nothing. Impact: XP is earned in places moderators think are ignored. LVL-11, LVL-12, LVL-131.
3. **`/Rewards Add` accepts `@everyone`, and the grant loop only catches Unauthorized** (new, low, suspected).
   `RewardCommandGroup.AddReward.cs:60-71` checks only managed and rank; `@everyone` is neither. At grant time
   (`GainUserXp.cs:161-169`) any other Discord error, such as a refusal to grant `@everyone` or a NotFound for a role
   deleted after the cache was read, escapes the handler. The rest of the rewards in that loop and the "has earned"
   log are skipped, on every XP gain. LVL-43, LVL-106.
4. **`/Rewards Add` doesn't check Manage Roles** (new, low). `RewardCommandGroup.AddReward.cs:60-71` checks the role's
   rank, not whether the bot can manage roles at all. The problem only shows as a guild issue when someone reaches the
   reward. LVL-107.
5. **`/Reclaim` doesn't record who reclaimed** (new, low). `ReclaimedXp` (`Grimoire.Domain/XpHistory.cs`) has no
   moderator, and `ReclaimUserXp.cs:96` has none to pass. `/Award` stores `AwarderId`. Impact: who took XP is only
   in the mod log, which may be off.
6. **`/LevelSettings View` shows a blank log channel when it was deleted** (new, low).
   `LevelSettingsCommandGroup.GetLevelSettings.cs:44-46`: a configured channel that's gone gives a null mention, so
   the line reads "**Log-Channel:** ". A failed settings lookup shows "None", which looks like "off". LVL-82.
7. **`/Level` Next Reward shows "None" when the nearest reward's role was deleted** (new, low). `GetLevel.cs:66-90`
   picks the nearest reward, then looks up its role; a missing role gives "None" even if later rewards exist. It
   corrects itself after the next XP gain removes the orphaned reward. LVL-57.
8. **The random start delay in background services doesn't stagger them** (new, low, cross-area).
   `GenericBackgroundService.cs:26` creates the `PeriodicTimer` when the service is built, and the timer starts
   counting then. The delay at lines 39-43 is always shorter than the period, so the first tick still comes one period
   after startup. Impact: the leaderboard first refreshes about 15 minutes after a restart, and services with the same
   period tick together. LVL-72. Belongs with area 08.
9. **`/Rewards` errors are public** (new, low). `RewardCommandGroup.AddReward.cs:73-76` (`OnFail`, also used by
   Remove) uses `ReplyAsync` on the public defer, not `SendErrorResponseAsync`, unlike the other settings commands
   (see KI-21).
10. **`/Leaderboard option:User` without a user replies before the defer** (new, low). `GetLeaderboard.cs:40-44`
    replies before the ephemeral choice at line 48, so the warning is public even outside the user command channel.
    LVL-65.
11. **`/Ignore Remove` names its list `item`, `/Ignore Add` names it `items`** (fixed 2026-11-08, rank 26 of the
    branch review). Both are `items` now, so the options show as `items_0` on both.

Previously reported and fixed, re-checked by the cases above: KI-19 (`/Level` member lookup), KI-41 (LVL-37), KI-42
(LVL-53), KI-43 (LVL-80), KI-44 (LVL-121), KI-45 (LVL-116, LVL-132), KI-46 (LVL-124).

## Findings

Record failures from manual runs here.
