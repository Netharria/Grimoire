# Known Issues (to fix later)

Found while writing the manual test docs. Not fixed yet. Each entry points at the test cases that confirm it, so
re-run those after a fix. Severity is a rough guess: **high** = wrong data or wrong security outcome,
**med** = wrong or misleading behaviour, **low** = polish.

Intentional behaviour is not listed. For example, `/Ban`, `/Unban`, `/Mute`, `/Unmute` and `/Lock` are gated on
ManageMessages for legacy compatibility (see 02).

Still open after the 2026-10-31 pass: KI-48 (undecided). KI-39 was fixed on 2026-11-01. KI-52 and KI-53 were fixed and KI-54 found on 2026-11-07. KI-55 to KI-107 were found on 2026-11-08 while drafting areas 04 to 09 (see the last section).

## Area 02 - Moderation actions

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-01 | fixed | **Fixed 2026-10-31. No invoker hierarchy check.** Ban, Kick and Mute refuse a target ranked at or above the invoker. The owner is exempt, and so is acting on yourself. `/Warn` is left out on purpose, so channel moderators can warn anyone. `/Lock` has no target. The mute role is ignored when ranking the target, because it can sit above a moderator's role. | `AddBanCommand`, `KickUser`, `MuteUser`, `Warn` | MOD-21, 53, 66, 109 |
| KI-02 | fixed | **Fixed 2026-10-31. `/Kick` and `/Mute` have no bot hierarchy pre-check.** `ModerationRank` gives Ban, Kick and Mute the same "I do not have permissions to ... that user." message. | `KickUser`, `MuteUser` | MOD-51, 52, 107, 108 |
| KI-03 | fixed | **Fixed 2026-10-31. `/Ban` saves the Sin before calling Discord.** `BanAddedEvent` is now the only writer of ban sins. `/Ban` leaves the moderator and reason in `BanHandoff`, calls Discord, and waits up to 5 seconds for the event to record the sin. A refused ban records nothing. The DM has the reason but no sin id. Banning someone already banned sends no event, so there is no second sin and the reply shows "Not recorded yet". | `AddBanCommand.BanAsync` | MOD-26 |
| KI-04 | fixed | **Fixed 2026-10-31. `/Mute` saves the Sin and `Mutes` row before checking the role exists or granting it.** `/Mute` checks the role, grants it, then saves. | `MuteUser.MuteUserAsync` | MOD-106, 107, 108 |
| KI-05 | fixed | **Fixed 2026-10-31. `/Mute` with no reason.** The DM leaves out the "for" clause, and the audit log reason is empty. | `MuteUser` | MOD-92 |
| KI-06 | fixed | **Fixed 2026-10-31. `/Lock` ignores the `Result`.** The overwrite is applied before the lock is saved, failures are reported from the `Result`, and the bot gate also needs Manage Roles. | `LockChannel` | MOD-173, 174, 177 |
| KI-07 | fixed | **Fixed 2026-10-31. `duration_amount` minimum is 0.** The minimum is 1 on `/Mute` and `/Lock`, which DSharpPlus also enforces itself. If a mute can't be recorded after the grant, the role and sin are taken back and an error is shown. This also exposed that `MuteAdded` refuses self-mutes, which used to leave the moderator muted with no record, so `/Mute` now refuses them up front. | **`duration_amount` minimum is 0** for `/Lock` and `/Mute`. For `/Mute` it is worse than an instant expiry: confirmed by `MuteTests.Mute_ForZeroMinutes_IsRecordedSoItExpires` (skipped), `MuteAdded` rejects an end time that isn't after its set time, `/Mute` ignores the failed `AddMute`, and the role is still granted, so the member stays muted with no record and nothing ever lifts it. `/Lock` is fine: it computes the end time just after the set time, so a zero-length lock is recorded and lifted on the next tick (`LockCommandTests.Lock_ForZeroMinutes_LocksAndIsRecorded`). Fix: make the minimum 1, and stop when `AddMute` fails. | MOD-93, 157, 189 |
| KI-08 | fixed | **Fixed 2026-10-31. `BanAddedEvent` 30 s de-dup reuses stale data.** The 30-second de-dup is gone. A ban the audit log can't attribute is recorded with a System actor, and its reason comes from the ban lookup. | `BanAddedEvent` | MOD-29, 30 |
| KI-09 | fixed | **Fixed 2026-10-31. `BanRemovedEvent` shows the original banner as `Mod`.** The log entry shows "Banned by" and "Unbanned by". `/Unban` passes its moderator through `BanHandoff`, and outside unbans use the audit log. Nothing new is stored. | `BanRemovedEvent`, `RemoveBanCommand` | MOD-33, 42 |
| KI-10 | fixed | **Fixed 2026-10-31. `/Unban` error text.** It now says "user is not banned." | `RemoveBanCommand` | MOD-36 |
| KI-11 | fixed | **Fixed 2026-10-31. `/Unmute` on a member who is not muted.** With no record and no mute role it says the member is not muted. With no record but holding the role, it still removes the role. | `UnmuteUser` | MOD-114 |
| KI-12 | fixed | **Fixed 2026-10-31. `LockedTreadEvents` (sic) does not check the Moderation module.** The handler checks the Moderation module and is renamed `LockedThreadEvents`. Bots stay subject to thread locks. | `LockedTreadEvents` | MOD-162, 168 |
| KI-13 | fixed | **Fixed 2026-10-31. `/Mutes Set` accepts any role.** `/Mutes Set` refuses @everyone, managed roles and roles at or above the bot. | `MuteAdminCommands.Set` | MOD-75 |
| KI-14 | fixed | **Fixed 2026-10-31. `/Mutes View`.** Expired mutes are left out, members who left are shown by id, and long lists split across fields. | `MuteAdminCommands.View` | MOD-141, 142, 143 |
| KI-15 | fixed | **Fixed 2026-10-31. `/Mutes Create`.** `/Mutes Create` stops and points to `/Mutes Refresh` when the configured role still exists. | `MuteAdminCommands.Create/Refresh` | MOD-83, 86 |
| KI-16 | fixed | **Fixed 2026-10-31. Inconsistent DM failure handling.** `DirectMessageAsync` handles every DM: it catches Discord failures and posts to the mod log. `/Kick` now DMs before kicking. | Ban, Warn, Mute, Unmute, Kick | MOD-11, 43, 58, 95, 113 |
| KI-17 | intended | **Not a bug. `/Warn` has no `RequireUserGuildPermissions` on purpose.** In the main guild, some users moderate specific channels. They can warn in those channels without the guild-wide permissions the other actions need. Don't add a guild-wide gate or a rank check. | `Warn` | MOD-63, 64, 65 |

## Found by automated tests

Each entry has a skipped test, or a planned one, that fails until the issue is fixed. The work items are in
`automation-backlog.md`.

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-18 | fixed | **Fixed 2026-10-25. The guild logs never posted.** `GuildLog` was registered as a singleton and again as a separate hosted-service instance, each with its own queue, so nothing read the queue commands wrote to. After 1024 messages, senders would also have blocked forever. It is now split into `GuildLog`, the queue singleton, and `GuildLogPublisher`, the hosted service that reads it. | `Features/Shared/Channels/GuildLog/` | `WarnTests.Warn_PostsTheWarnEmbedToTheModLog` |
| KI-19 | fixed | **Fixed 2026-10-31. Slash commands never get a `DiscordMember` for a user option.** Commands use `GetMemberOrDefaultAsync`, which checks the cache first. It now only treats a real NotFound as "not a member", so an outage can't skip a rank check. | `Warn.cs`, `UserInfoCommands.cs`, `AddBanCommand.cs`, `GetLevel.cs` | `WarnTests.Warn_DirectMessagesTheWarnedMember`, `UserInfoCommandTests.ForAMember_ShowsTheirJoinDate` and `CanGainXp_IsNoForAMemberWithAnIgnoredRole`, `BanTests.Ban_OfSomeoneTheBotCantOutrank_IsRefusedWithNoSin` (skipped) |
| KI-20 | fixed | **Fixed 2026-10-25. Locked threads enforced nothing.** Threads inherit their parent's permissions, so thread locks rely on `LockedTreadEvents` to delete new messages and reactions until the lock is lifted. The handler replaced `ModerationEvents` in the DSharpPlus 5 migration but was never registered. It is now registered in `GrimoireServiceRegistration.cs`. KI-12, the missing module check, is still open. | `GrimoireServiceRegistration.cs` | `ThreadLockTests`, MOD-159 to MOD-167 |
| KI-21 | fixed | **Fixed 2026-10-31. Errors meant to be ephemeral are public.** `SendErrorResponseAsync` and `SendWarningResponseAsync` delete the deferred response, then send an ephemeral follow-up. Discord's docs say a follow-up straight after a defer only edits it, which is why the delete comes first. Not yet checked against real Discord. | `CommandContextExtension.ReplyAsync` and the commands that defer | `GeneralSettingsCommandsTests.SelectChannelWithoutAChannel_ErrorIsEphemeral` (skipped), GEN-37 |
| KI-22 | fixed | **Fixed 2026-10-31. `/GeneralSettings ModLogChannel option:Off` reply ends with a dangling "to ".** The reply reads "Disabled the moderation log." since I-4 (2026-11-07). | `ChannelSettingCommand.cs` | `GeneralSettingsCommandsTests.ModLogChannel_Off_ClearsTheSetting_IgnoresAnyChannel_AndPostsNothing`, GEN-32 |
| KI-23 | fixed | **Fixed 2026-10-31. `/UserInfo` throws whenever the User Log module is on.** The query now selects the nickname's string value. | `UserInfoCommands.cs` (`GetAndAddUsernames`) | `UserInfoCommandTests`: three skipped tests, GEN-100, GEN-101, GEN-109 |
| KI-24 | fixed | **Fixed 2026-10-31. `/Purge` never handles more than 100 messages.** Purge All fetches `count` messages. Purge User scans the newest 1000, stopping at `count` or the 14-day limit. | `DiscordChannelExtensions.PurgeMessagesAsync` | `PurgeCommandsTests`: two skipped tests, GEN-135, GEN-136 |
| KI-25 | fixed | **Fixed 2026-10-31. `/Kick` doesn't exist.** `KickUser` is registered. | `GrimoireServiceRegistration.cs` | `KickTests.Kick_IsRegisteredByTheBot` (skipped), MOD-43 to MOD-56 |
| KI-26 | fixed | **Fixed 2026-10-31. `/Mutes View` never lists muted members.** The lookups are awaited. See KI-14. | `MuteAdminCommands.View.cs` | `MuteAdminTests.View_ListsMutedMembers` (skipped) |
| KI-27 | fixed | **Fixed 2026-10-31. `DiscordRetryPolicy.RetryDiscordCall(function, defaultValue)` never returns the default.** The overload is async and returns the default for any `DiscordException`. | `Utilities/DiscordRetryPolicy.cs` | `MuteAdminTests.Create_ListsChannelsItCouldNotSet` (skipped), MOD-81 |
| KI-28 | fixed | **Fixed 2026-10-31. `/Mutes Refresh` says "Succussfully refreshed permissions".** Typo fixed. | `MuteAdminCommands.Refresh.cs` | `MuteAdminTests.Refresh_FixesExistingOverwrites_AndKeepsUnrelatedBits` pins the current text |
| KI-29 | fixed | **Fixed 2026-10-31. `/SinLog type:Mod` ignores the user option.** It counts the selected user, or the invoker if blank, and has a Kicks field. | `SinLog.cs` | `SinLogTests.Mod_ForAnotherModerator_CountsTheirActions` (skipped), SIN-11, SIN-12 |
| KI-30 | fixed | **Fixed 2026-10-31. A sin log longer than one page shows only the last page.** The first page edits the response. Later pages are follow-ups with the same visibility. | `SinLog.cs`, `CommandContextExtension.ReplyAsync` | `SinLogTests.ALongLog_ShowsEveryPage` (skipped), SIN-13 |
| KI-31 | fixed | **Fixed 2026-10-31. A sin with a `System` actor can't be saved.** The actor converters on `Sin` and `SinReasonHistory` use `decimal?`, `convertsNulls` and an explicit `numeric(20,0)` column type. No schema change. The spam filter works again, and now obeys the AntiSpam module. | `Grimoire.Data/Configuration/SinConfiguration.cs` (actor converter) | `BanTests.ClientBan_TheAuditLogCannotAttribute_IsRecordedAsSystem`, `SinLogTests.ASinWithNoKnownModerator_ShowsSystem`, `SpamFilterTests.Spam_MutesTheSpammer_DeletesTheMessage_AndLogs` (skipped) |
| KI-32 | fixed | **Fixed 2026-10-31. `/Pardon` without a reason throws.** The reason is a required option, and a blank reason is refused. | `PardonSin.cs` | `SinAdminTests.Pardon_WithoutAReason_StillPardons` (skipped), SIN-21 |
| KI-33 | fixed | **Fixed 2026-10-31. `/Pardon` and `/Reason` can't find sins of users with no username history.** The sin is looked up first, and a user with no history falls back to a mention. | `PardonSin.cs`, `UpdateSinReason.cs`, `PublishCommands.*.cs` | `SinAdminTests`: two skipped tests, `PublishTests.PublishBan_ForAUserWithNoUsernameHistory_StillPublishes` (skipped), SIN-24, SIN-42, SIN-79 |
| KI-34 | fixed | **Fixed 2026-10-31. `/Forget`'s mod-log title starts with a space.** The title uses the display name. | `ForgetSin.cs` | SIN-32 |
| KI-35 | fixed | **Fixed 2026-10-31. `/ModSettings AutoPardon` crashes on very long durations.** The total is capped at 100 years. | `ModSettings.AutoPardon.cs`, `Enums/Duration.cs` | `ModSettingsTests.AutoPardon_TooLongToRepresent_IsRefusedClearly` (skipped), SIN-54 |
| KI-36 | fixed | **Fixed 2026-10-31. Republishing after the public post was deleted creates duplicates.** `SavePublishedMessageAsync` updates the stored id when a replacement is posted. | `PublishCommands.*.cs` | `PublishTests.PublishBan_AfterThePostWasDeleted_PostsOnceAndKeepsEditingIt` (skipped), SIN-72 |
| KI-37 | fixed | **Fixed 2026-10-31. `/SpamFilter Override option:Inherit` shows an error.** Inherit has its own reply. | `SpamFilterOverrideCommands.cs` | `SpamFilterTests.Override_Inherit_ClearsTheOverrideAndSaysSo` (skipped) |
| KI-38 | fixed | **Fixed 2026-10-31. Message edits are never logged.** The proxy fields are projected as nullable. | `UpdateMessageEvent.cs` (query, line 39) | `MessageLogEventsTests.Edit_IsRecorded_AndLoggedWithBeforeAndAfter` (skipped) |
| KI-39 | fixed | **Fixed 2026-11-01. Deletes are never attributed to a moderator** (dependency bug). DSharpPlus 5.0.0-nightly-02611 built a message-delete audit entry's `Target` from the responsible user's id, not the target's, so `DiscordAuditLogParserService` never matched the message's author and "Deleted By" never showed. It regressed in PR #1612, which closed #1569. The fix went upstream and shipped in 5.0.0-nightly-02614, which Grimoire now uses. Grimoire's code didn't change. | `Utilities/DiscordAuditLogParserService.cs`, DSharpPlus `AuditLogParser` | `MessageLogEventsTests.Delete_ByAModerator_IsAttributed` |
| KI-40 | fixed | **Fixed 2026-10-31. A member's first nickname is never logged.** Only a missing row counts as no history. | `UserLogging/UpdatedNicknameEvent.cs` | `UserLogTests.FirstNickname_IsRecorded_AndLogged` (skipped) |
| KI-41 | fixed | **Fixed 2026-10-31. A reward role the bot couldn't grant is still logged as earned.** It returns after reporting the failure. | `Leveling/Events/GainUserXp.cs` (`ApplyRewards`) | `GainXpTests.ARewardRoleTheBotCannotGrant_IsNotLoggedAsEarned` (skipped) |
| KI-42 | fixed | **Fixed 2026-10-31. `/Level`'s Next Reward is wrong in two ways.** Rewards are ordered by level, and the reward's level is printed. | `Leveling/UserCommands/GetLevel.cs` | `LevelAndLeaderboardTests.Level_ShowsTheNextRewardAndItsLevel` and `Level_ShowsTheNearestReward` (skipped) |
| KI-43 | fixed | **Fixed 2026-10-31. `/LevelSettings View` shows record dumps instead of numbers.** The three types override `ToString`, like the repo's other value types. | `Leveling/Settings/LevelSettingsCommandGroup.GetLevelSettings.cs` | `LevelSettingsTests.View_ShowsTheNumbersPlainly` (skipped) |
| KI-44 | fixed | **Fixed 2026-10-31. The leveling `/Ignore Add` can't ignore a channel.** `/Ignore Add` and `/Ignore Remove` have an optional `channel` option, and their items are optional too. | `Leveling/Settings/IgnoreCommandGroup.AddIgnore.cs` | `IgnoreAndAwardTests.IgnoreAdd_AcceptsChannels` (skipped) |
| KI-45 | fixed | **Fixed 2026-10-31. Any member can remove level rewards.** Rewards Remove and Ignore View have the guild, Manage Guild and Leveling checks. | `Leveling/Rewards/RewardCommandGroup.RemoveReward.cs`, `Settings/IgnoreCommandGroup.GetIgnoredItems.cs` | `RewardCommandsTests.Remove_ByAMemberWithoutManageGuild_IsRefused` and `Remove_NeedsTheModule` (skipped) |
| KI-46 | fixed | **Fixed 2026-10-31. `/Ignore Remove` doesn't say what it removed.** The formatter handles the Watched types. | `Leveling/Settings/IgnoreCommandGroup.cs` | `IgnoreAndAwardTests.IgnoreRemove_SaysWhatIsNoLongerIgnored` (skipped) |
| KI-47 | fixed | **Fixed 2026-10-31. `/Commands Forget`'s mod log doesn't mention the bot.** Both messages use `.Mention`. | `CustomCommands/CustomCommandSettings.Forget.cs` | `CustomCommandSettingsTests.Forget_IsLoggedWithTheBotsMention` (skipped) |
| KI-48 | high | **Gateway outages are never detected** (dependency gap). In DSharpPlus 5.0.0-nightly-02611, `SocketClosedEventArgs` and `ZombiedEventArgs` have handler registration but are never dispatched. The resume loop also consumes the RESUMED payload without forwarding it, so `SessionResumedEventArgs` doesn't fire after a resume. `GatewayEvents` therefore never marks the gateway down: the watchdog's "disconnected for N minutes" alert can't fire, and the resumed and recreated warnings never fire either. Fix: report upstream. Meanwhile, track health another way, for example from heartbeat acks or the time since the last dispatch. | `Features/Shared/Gateway/GatewayEvents.cs`, DSharpPlus `GatewayClient` | `GatewayAndIssueTests.ADroppedConnection_IsNoticed` and `AResumeAfterDowntime_IsRecordedAsAWarning` (skipped) |
| KI-49 | fixed | **Fixed 2026-10-31. A rejoin causes a false "Avatar Updated" log.** `MemberAdded` stores the plain `AvatarUrl`. Rows already stored the old way cause one last false log. | `Features/Shared/Events/MemberAdded.cs` | `GuildSyncAndInviteTests.ARejoin_DoesNotCauseAFalseAvatarChange` (skipped) |
| KI-50 | fixed | **Fixed 2026-10-31. A used-up single-use invite shows its old use count.** The fallback returns `Uses + 1`. | `Logging/UserLogging/InviteService.cs` | `GuildSyncAndInviteTests.AJoin_WithASingleUseInvite_CountsTheUse` (skipped) |

| KI-51 | fixed | **Fixed 2026-10-31. `MessageDeletedByModeratorEntry.ModeratorId` was never saved.** The configuration declared a shadow property of the same name on the base type, so the entry's own property read back as 0. It is now mapped on the entry itself. No schema change. | `Grimoire.Data/Configuration/MessageHistoryConfiguration.cs` | `MessageLogEventsTests.DeletedByModeratorEntry_KeepsItsModerator` |
| KI-52 | fixed | **Fixed 2026-11-07. `AJoin_ShowsTheInviteThatWasUsed` was flaky.** The bot is ready before its startup sync has stored the guild's invites, so a join right after startup found no guild ("Could not find guild.") or no invites ("Unknown Invite"). The test class now waits for the sync, which stores the invites before the history. The same window exists in production: a join in the first moments after startup is logged with no invite, or not at all. | `GuildSyncAndInviteTests` | `GuildSyncAndInviteTests.AJoin_ShowsTheInviteThatWasUsed` (no longer skipped) |
| KI-53 | fixed | **Fixed 2026-11-07. One spam burst could record several mutes.** `SpamTrackerModule` never reset a member's points after a verdict, so each message still in flight tripped the filter again, each mute doubling the next. The tracker now gives one verdict per burst, resetting the points under a lock. | `SpamTrackerModule.cs` | `SpamFilterTests.Spam_MutesTheSpammer_DeletesTheMessage_AndLogs` expects exactly one sin and one mute |
| KI-54 | med | **After a timed-out request, the next post to that channel can fail inside DSharpPlus.** Found while testing I-1 with a fake `HttpClient` timeout. The alert loop survives the timeout, but in about 1 run in 5 the next `DiscordChannel.SendMessageAsync` to the same channel throws `NullReferenceException` from inside DSharpPlus. With no timeout, 10 of 10 runs were clean. Not checked against real Discord, and the cause inside DSharpPlus (possibly rate-limit bucket state) is unknown. If real timeouts do this, the next log or alert to that channel is lost. | DSharpPlus `DiscordChannel.SendMessageAsync`; seen from `AlertService.cs` | `AlertTests.AfterAPostTimesOut_LaterAlertsAreStillProcessed` avoids it by sending a warning; post a second urgent alert there to reproduce |

## Area 03 - Sins

Area 03's own findings are listed under "Found by automated tests" above (KI-29 onward), since that doc was written
alongside its tests.

## Area 01 - General settings

All the open items in the Code observations section of `01-general-settings.md` were triaged on 2026-10-31.

## Areas 04 to 09 - drafted 2026-11-08

Found while writing areas 04 to 09 from the source. None of these has been confirmed on a live guild yet. Entries
marked **(decide)** may be intended. Each area doc's Code observations section has more detail. Line numbers are from
2026-11-08 and may have moved since.

Left out on purpose, because the review docs already track them: C-2, C-3 and C-6 (custom commands, see area 07) and
I-18 (stale `CLAUDE.md`, see area 09).

### Area 04 - Spam filter and diagnostics

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-55 | med | **A spam mute is saved before it is known to work.** The sin and `MuteAdded` row are saved, then the role is looked up and granted. If the role is gone, both rows stay. If the grant fails (no Manage Roles, or the role is above the bot), it throws: no mod log, no DM, and the spam isn't deleted. This is KI-04 in the spam path. | `SpamFilter/SpamEvents.cs:62-75` | SPAM-27, 28, 29 |
| KI-56 | med | **Muted members can still type in voice and stage channel chat.** The mute overwrite on those channels denies only Connect and Speak, not Send Messages. The diagnostics use the same set, so they don't flag it. | `Structs/PermissionValues.cs:16-17`, `MuteAdminCommands.cs:63-64`, `ModerationDiagnostics.cs:134-135` | DIAG-39 |
| KI-57 | low | **Diagnostics check hidden channels.** View Channel is ignored, so a staff-only channel is reported if its send bits aren't denied. | `Diagnostics/EffectivePermissions.cs:14-41` | DIAG-38 |
| KI-58 | low | **The bot's own role is reported as leaking.** Only @everyone, the mute role, Administrator roles and ignored roles are left out, so the bot's role and other bots' managed roles show up. | `Diagnostics/ModerationDiagnostics.cs:25-27` | DIAG-37 |
| KI-59 | low | **Media channels get lock findings, but `/Lock` refuses them.** | `DiagnosticsSnapshotBuilder.cs:32-33`, `ModerationDiagnostics.cs:91`, `LockChannel.cs:34-35` | DIAG-36 |
| KI-60 | low | **`/SpamFilter` has no module gate and no mod log.** There is no `RequireModuleEnabled(AntiSpam)` (or `RequireGuild`), so overrides save while AntiSpam is off. Override posts nothing to the mod log, unlike every other settings change. | `SpamFilter/Commands/SpamFilterOverrideCommands.cs:13-21, 52-55` | SPAM-40, SPAM-50, XC-89 |
| KI-61 | low | **`/SpamFilter View` with no overrides shows an empty embed** instead of "None". | `SpamFilterOverrideCommands.cs:77-78` | SPAM-61 |
| KI-62 | low | **The diagnostics ignore list can't be fully managed.** View cuts off at 1000 characters, which can split a mention. A deleted role can't be picked, so it stays on the list (harmless). | `Diagnostics/DiagnosticsCommands.cs:116` | DIAG-67, DIAG-68 |
| KI-63 | low | **(decide) Spam mute escalation counts pardoned and manual mutes, with no cap.** Every Mute sin in the last 24 hours doubles the next spam mute. | `SpamFilter/SpamEvents.cs:52-55` | SPAM-05, SPAM-06 |

### Area 05 - Logging

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-64 | med | **A mod's delete of an unsaved message is credited to a later self-delete.** The handler returns before the audit log is read when the message was never saved, so the parser's cached count doesn't move. If the same author then deletes one of their own saved messages in that channel within 10 minutes, it shows "Deleted By" the mod and is saved as a moderator delete. | `MessageLogging/DeleteMessageEvent.cs:71`, `Utilities/DiscordAuditLogParserService.cs:75` | LOG-89 |
| KI-65 | low | **(decide) `/Log Message View` and `/Log User View` still need their module.** Commit a624c69 let `/ModSettings View` and `/LevelSettings View` run with their module off. The Log Views don't show the module state, so either add it and move the gate to the setters, or leave as is. | `Logging/Settings/LogSettingsCommands.cs:30, 37` | LOG-19, LOG-50, XC-73 |
| KI-66 | low | **Set replies use code names**, e.g. "Updated DeleteLog to ..." and "Disabled JoinLog.", not the display names. The tests pin the current text. | `LogSettingsCommands.Message.Set.cs:52`, `LogSettingsCommands.User.Set.cs:58` | LOG-01, LOG-40 |
| KI-67 | low | **A join shows "Vanity Invite" whenever the invite is unknown** and the guild has a vanity URL, including when the bot lacks Manage Server. | `UserLogging/GuildMemberAddedEvent.cs:35` | LOG-142 |
| KI-68 | low | **One username change writes a `UsernameHistory` row per guild.** A guild whose update arrives more than 2 seconds after the first logs nothing. | `UserLogging/UpdatedUsernameEvent.cs:32` | LOG-166 |
| KI-69 | low | **An edit that removes all the text is not logged.** | `MessageLogging/UpdateMessageEvent.cs:28` | LOG-76 |
| KI-70 | low | **Bulk delete "Message Count" counts only saved messages**, and no moderator is named. The dump's file name has commas and colons. | `MessageLogging/BulkMessageDeletedEvent.cs:79, 90` | LOG-111 |
| KI-71 | low | **`/Log Message ViewOverrides` with nothing to show is a blank embed.** Overrides on deleted channels are hidden and their rows are never removed. | `LogSettingsCommands.Message.ViewOverrides.cs:27, 33` | LOG-31, LOG-32 |
| KI-72 | low | **(unconfirmed) Clearing all reactions on an uncached message may be skipped**, because the guild is found through the cached channel. The rows stay active. | `MessageLogging/ReactionEvents.cs:80` | LOG-128 |

### Area 06 - Leveling

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-73 | med | **An ignored channel doesn't cover its threads or category.** Only the message's own channel id is compared, and threads pass their own id. `/Ignore Add channel:` accepts categories and forums without a warning, but ignoring them does nothing. | `SettingsModule.Leveling.Ignores.cs:29`, `GainUserXp.cs:57`, `IgnoreCommandGroup.AddIgnore.cs:24-25` | LVL-11, LVL-12, LVL-131 |
| KI-74 | low | **(suspected) Two messages at once can both earn XP.** The timeout check and the insert aren't atomic. | `Leveling/Events/GainUserXp.cs:62-90` | LVL-19 |
| KI-75 | low | **(suspected) `/Rewards Add` accepts @everyone, and the grant loop only catches `UnauthorizedException`.** Any other Discord error skips the remaining rewards and the "has earned" log, on every XP gain. | `RewardCommandGroup.AddReward.cs:60-71`, `GainUserXp.cs:161-169` | LVL-43, LVL-106 |
| KI-76 | low | **`/Rewards Add` doesn't check that the bot has Manage Roles.** The problem only shows when someone reaches the reward. | `RewardCommandGroup.AddReward.cs:60-71` | LVL-107 |
| KI-77 | low | **`/Reclaim` doesn't record who reclaimed.** `ReclaimedXp` has no moderator, unlike Award's `AwarderId`. | `Awards/ReclaimUserXp.cs:96`, `Grimoire.Domain/XpHistory.cs` | none (DB check) |
| KI-78 | low | **`/LevelSettings View` shows a blank Log-Channel when the channel was deleted.** A failed lookup shows "None", which looks like "off". | `LevelSettingsCommandGroup.GetLevelSettings.cs:44-46` | LVL-82 |
| KI-79 | low | **`/Level` Next Reward shows "None" when the nearest reward's role was deleted**, even if later rewards exist. | `UserCommands/GetLevel.cs:66-90` | LVL-57 |
| KI-80 | low | **`/Rewards` errors are public**, not ephemeral (see KI-21). | `RewardCommandGroup.AddReward.cs:73-76` | none |
| KI-81 | low | **`/Leaderboard option:User` with no user warns before the defer**, so the warning is always public. | `UserCommands/GetLeaderboard.cs:40-44` | LVL-65 |
| KI-82 | low | **`/Ignore Remove`'s option is `item`, `/Ignore Add`'s is `items`.** They show as `item_0` and `items_0`. | `IgnoreCommandGroup.RemoveIgnore.cs:27` | none |

### Area 07 - Custom commands

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-83 | low | **`/Commands Revert` crashes on an out-of-range version.** A hand-typed negative or very large version throws in `new DateTimeOffset(ticks)`, giving the generic error and an alert instead of "Version not found". | `CustomCommandSettings.Revert.cs:45, 116` | CC-55 |
| KI-84 | low | **(unconfirmed) Embed commands without Embed Links.** Only `UnauthorizedException` is caught. If Discord refuses the empty message with a 400, every use raises an event-handler alert. | `TextCustomCommandProcessor.cs:135-147` | CC-119 |
| KI-85 | low | **(unconfirmed) DSharpPlus's `!` text processor overlaps the text trigger.** `/Commands Stats` and Leaderboard take `CommandContext`, so they look runnable as `!commands ...`. A custom command named like a text-runnable command could get two replies. | `GrimoireServiceRegistration.cs:207-210` | CC-126 |
| KI-86 | low | **Restricted commands are shown to members who can't run them**, in the autocomplete and in Stats and Leaderboard. | `GetCustomCommandOptions.cs:46-50`, `CustomCommandSettings.Stats.cs`, `.Leaderboard.cs` | CC-68, CC-103 |
| KI-87 | low | **(decide) Names are case-sensitive, but autocomplete isn't.** `!Hello` doesn't run `hello`. | `DatabaseQueryHelpers/CustomCommandDatabaseQueryHelpers.cs:24` | CC-19, CC-121 |
| KI-88 | low | **Learn says "Added" when it updates an existing command.** | `CustomCommandSettings.Learn.cs:127` | CC-11 |

### Area 08 - Alerts and health

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-89 | med | **One failed invite fetch stops the whole startup sync.** No history is saved and no invites are tracked, so every join throws "Could not find guild." until a restart. `GuildAdded` has the same order. | `Shared/Events/UpdateAllGuilds.cs:41`, `GuildAdded.cs:36` | GLD-07 |
| KI-90 | med | **A guild tracked with no invites matches joins to an arbitrary invite.** This happens when the bot synced without Manage Server and got it later. Every listed invite looks new, and the first is returned. A missed InviteCreate causes the same mismatch. | `UserLogging/InviteService.cs:46-54` | GLD-23 |
| KI-91 | low | **A failed urgent post still uses up the cooldown.** The alert is lost, repeats are held back, and their count is reset. | `Alerts/AlertService.cs:66-69` | ALRT-09 |
| KI-92 | low | **A missing urgent channel drops alerts with nothing logged.** The daily report is silent the same way. | `AlertService.cs:68`, `DailyWarningReportService.cs:53` | ALRT-08, ALRT-27 |
| KI-93 | low | **Cutting an urgent post to 2000 characters can leave the code block unclosed.** Cosmetic. | `AlertMessageFormatter.cs:27` | ALRT-11 |
| KI-94 | low | **Buffered warnings are lost on shutdown.** Nothing flushes the buffer, so up to 30 s of warnings are lost on each restart. | `WarningFlushService.cs:15`, `AlertService.StopAsync` | ALRT-30 |
| KI-95 | low | **A restart after the day's report can post a second one.** The reported date is kept in memory. A small race between `GetOpenAsync` (`:49`) and `MarkReportedAsync` (`:56`) can also mark counts reported without showing them. | `DailyWarningReportService.cs:26, 49-56` | ALRT-31 |
| KI-96 | low | **The issue throttle is used up when the post goes nowhere** (mod log unset or failing), so the problem stays hidden for a day. | `GuildIssues/GuildIssueReporter.cs:39` | ALRT-47, 50, 51 |
| KI-97 | low | **(suspected) Startup sync adds duplicate username rows** for a member in two guilds. The lookup uses `AsNoTracking`, so it misses rows added earlier in the same unsaved context. | `UpdateAllGuilds.cs:30-47`, `UserDatabaseQueryHelpers.cs:17-29` | GLD-04 |
| KI-98 | low | **An invite kept after an unconfirmed delete can be blamed for a later join** through a single-use invite. | `InviteEvents.cs:44-64`, `InviteService.cs:58-68` | GLD-49 |
| KI-99 | low | **The random start delay in background services staggers nothing.** The `PeriodicTimer` starts counting when the service is built, so the first tick still comes one period after startup. The leaderboard first refreshes about 15 minutes after a restart. | `GenericBackgroundService.cs:26, 39-43` | LVL-72 |
| KI-100 | low | **The "Watching N servers." status isn't updated when the bot leaves a guild.** There is no `GuildDeleted` handler. Cosmetic. | `GuildAdded.cs:23`, `UpdateAllGuilds.cs:22` | GLD-21 |

### Area 09 - Cross-cutting

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-101 | med | **A missing `pluralkitApiEndpoint` breaks the delete log.** PluralKit counts as configured without the endpoint. Creating the client then throws outside the `try`, so every delete of a logged message under a minute old is lost. | `PluralKit/PluralkitService.cs:37-38, 52`, `GrimoireServiceRegistration.cs:247` | XC-07 |
| KI-102 | low | **`/Unlock` doesn't require Manage Roles for the bot**, unlike `/Lock`. The gate passes and Discord refuses the revert. | `Lock/Commands/UnlockChannel.cs:18` | XC-61 |
| KI-103 | low | **A "not modified" write never clears a stale cache.** Only reachable after a direct SQL edit. | `SettingsModule.cs:89`, `SettingsModule.MessageLog.ChannelOverrides.cs:82`, `SettingsModule.Moderation.SpamFilterOverrides.cs:83` | XC-105 |
| KI-104 | low | **The bulk delete log shows proxied messages under the webhook id**, not the member behind the proxy. | `BulkMessageDeletedEvent.cs:41-60` | XC-45 |
| KI-105 | low | **A settings DB outage reads as "This module is disabled in this server."** | `Attributes/RequireModuleEnabledAttribute.cs:27-29` | XC-106 |
| KI-106 | low | **The warning flush and daily report services start before migrations finish.** Failures are caught. | `GrimoireServiceRegistration.cs:224-226` | XC-28 |
| KI-107 | low | **(decide) With Moderation off and AntiSpam on, spam is detected but nothing happens.** There is no mute role, so `SpamEvents` returns before deleting the message. Either document that AntiSpam needs Moderation, or still delete the spam. | `SettingsModule.Moderation.Mutes.cs:22-24`, `SpamEvents.cs` | XC-83 |
