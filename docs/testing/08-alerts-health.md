# 08 - Alerts and Health

Scope: `AlertService` (urgent alerts and their cooldown), the warning buffer, `WarningFlushService` and
`DailyWarningReportService`, `GuildIssueReporter` and its throttle, `GatewayEvents` and `GatewayWatchdogService`,
the guild log queue (`GuildLog`, `GuildLogPublisher`), and the guild sync events `UpdateAllGuilds`, `GuildAdded`,
`InviteEvents` and `MemberAdded`.

Source: `Grimoire/Features/Shared/Alerts/`, `Grimoire/Features/Shared/GuildIssues/`,
`Grimoire/Features/Shared/Gateway/`, `Grimoire/Features/Shared/Channels/GuildLog/`,
`Grimoire/Features/Shared/Events/`, `Grimoire/Features/Logging/UserLogging/InviteService.cs` and
`GuildMemberAddedEvent.cs`, `Grimoire/Utilities/GenericBackgroundService.cs`.

The join log's other fields (Name, Created, New Account) are area 05. This doc only checks its "Invite Used" field.

## Setup

Do the standard preflight from `00-environment.md` first. Also:

- Add a `#warnings` channel to Test-A for the daily report.
- In `appsettings.json` (or environment variables), set `channelId` to `#alerts`, `warningChannelId` to `#warnings`,
  and `warningReportTimeUtc` to a time a few minutes ahead in UTC (format `HH:mm`). Restart after each change:
  configuration is read when it's used, but the cases below assume a clean start.
- `/GeneralSettings ModLogChannel` set to `#modlog` in both guilds. Setup problems are posted there.
- User Log enabled, with the UserJoined log set to `#userlog`.
- Bot permissions for the happy paths: View Channel, Send Messages, Embed Links, Attach Files, Manage Server,
  View Audit Log. Remove one at a time for the failure cases and restore it afterwards.
- Tables (EF defaults, adjust to your schema): `AlertOccurrences`, `UsernameHistory`, `NicknameHistory`, `Avatars`,
  `OldLogMessages`.

## Timing and configuration

| What | Value | Configurable | How to shorten for a manual run |
|------|-------|--------------|---------------------------------|
| Urgent cooldown per alert key | 5 min | no | Restart the bot: the cooldown is in memory |
| Guild issue throttle per guild and code | 1 day | no | Restart the bot: the throttle is in memory |
| Watchdog urgent threshold | 2 min down | no | none |
| Watchdog tick | 30 s | no | none |
| Warning flush tick | 30 s | no | none |
| Daily report poll | 1 min | no | none |
| Daily report time | 09:00 UTC | `warningReportTimeUtc` | Set it a few minutes ahead |
| Warning retention | 30 days by `LastSeen` | no | Seed old rows with SQL |
| Background task urgent escalation | 3 consecutive failures | no | none |

Every `GenericBackgroundService` waits a random delay of up to one tick before its first run. Allow two ticks before
asserting.

Alert channels are bot-wide. An alert raised in Test-B is posted to `#alerts` in Test-A.

## Ways to raise alerts by hand

- **Urgent:** stop the database (`docker compose stop db`) for about 30 s. The 5 s background tasks
  (`LockBackgroundTasks`, `MuteBackgroundTasks`) fail three times in a row and raise an urgent `BackgroundTaskFailure`.
  A slash command that needs the database, such as `/SinLog`, raises an urgent `UnhandledException`. Start the
  database again afterwards.
- **Warning:** any setup problem sends a `GuildIssue` warning. The easiest is ALRT-40: remove View Audit Log from the
  bot and revoke an invite.

---

## A. Urgent alerts (`AlertService`)

Urgent alerts are posted at once to the `channelId` channel as plain text. The format is
`**[URGENT]** <Type>`, then `` `<Discriminator>` `` if there is one, then `(guild <id>)` if there is one, then
`— repeated N more times since the last alert` if repeats were held back. The next line is the message, then the
exception in a `csharp` code block (inner messages, then Grimoire stack frames only). The whole post is cut to
2000 characters, ending in `...`.

The key is the type, the discriminator, the exception type and the top Grimoire stack frame. The message text is not
part of the key. A repeat of the same key within 5 minutes is not posted; the next post after the window counts it.
The queue holds 256 alerts and drops new ones when full.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| ALRT-01 | Setup done | Stop the database for 30 s | `#alerts` gets `**[URGENT]** BackgroundTaskFailure` `` `MuteBackgroundTasks` `` (or `LockBackgroundTasks`), then "MuteBackgroundTasks failed 3 time(s) in a row: ..." and a code block. No `(guild ...)` part | Error log "Exception was thrown when running background task MuteBackgroundTasks (consecutive failures: N)" each tick. The first two failures are warnings, not posted |
| ALRT-02 | Database stopped | `admin` runs `/SinLog type:All` in Test-A **[auto]** `AlertGapTests.ACommandThatFails_RaisesAnUrgentAlert_WithTheCommandAndGuild` | `#alerts` gets `**[URGENT]** UnhandledException` with the command name as discriminator, `(guild <Test-A id>)`, and "Encountered exception while executing ..." (with `[Id ...]` when the command has an error id) | Error log for the command |
| ALRT-03 | ALRT-01 just posted, database still down | Wait 2 more minutes | No second post for the same task, though it keeps failing every 5 s | Error log every tick |
| ALRT-04 | As ALRT-03 | Keep the database down until 5 min after ALRT-01 | One more post, with "— repeated N more times since the last alert" in the header | none |
| ALRT-05 | As ALRT-01 | Watch for the other 5 s task | Its own post appears: a different discriminator is a different key, so it isn't held back | none |
| ALRT-06 | `channelId` empty, restart | Repeat ALRT-01 | Nothing posted anywhere | No error from `AlertService` |
| ALRT-07 | `channelId` set to `abc` | Repeat ALRT-01 | Nothing posted (an unparseable id counts as unset) | No error from `AlertService` |
| ALRT-08 | `channelId` set to a deleted channel's id | Repeat ALRT-01 **[auto]** `AlertGapTests.AMissingUrgentChannel_IsLogged` (skipped, KI-92) | Nothing posted | **Nothing is logged.** The alert is lost silently (see observation 2) |
| ALRT-09 | `channelId` set to `#private`, which the bot can't send in | Repeat ALRT-01 **[auto]** `AlertGapTests.AnUrgentPostThatIsRefused_IsLogged_AndNotAlertedAbout`, `AfterARefusedPost_TheNextRepeatIsPosted` (skipped, KI-91) | Nothing posted | Error log "Failed to process alert BackgroundTaskFailure" with a 403. No alert about the alert. The next repeat within 5 min is still held back (observation 1) |
| ALRT-10 | ALRT-01 posted less than 5 min ago | Restart the bot, then repeat ALRT-01 **[auto]** `AlertGapTests.AfterARestart_TheCooldownStartsEmpty` | Posted again at once, with no repeat count: the cooldown starts empty | none |
| ALRT-11 | An alert with a very long exception (hard to force by hand) | Check the post **[auto]** `AlertMessageFormatterTests.A_longer_post_is_cut_to_2000_characters_ending_in_an_ellipsis`; `A_cut_post_with_an_exception_still_closes_its_code_block` (skipped, KI-93) | Cut to 2000 characters ending in `...`. The closing code fence can be cut off (observation 3) | none |
| ALRT-12 | none | A post to `#alerts` times out | Later alerts are still processed | KI-54: the next post to the same channel may fail inside DSharpPlus |
| ALRT-13 | none | Queue more than 256 alerts at once **[auto]** `AlertGapTests.AFullQueue_DropsNewAlerts_AndKeepsTheQueuedOnes`, `AFullQueue_LogsEachDroppedAlert` (skipped, KI-110) | New alerts are dropped | Warning log "Dropped alert {AlertType} because the alert queue or warning buffer is full" |

ALRT-01, 05, 06, 12 and the cooldown in ALRT-03/04 are **[auto]**: `AlertTests.AnUrgentAlert_IsPostedAtOnce`,
`DifferentProblems_AreNotHeldBackByEachOther`, `WithoutAnUrgentChannel_NothingIsPosted`,
`AfterAPostTimesOut_LaterAlertsAreStillProcessed` and `RepeatsOfTheSameProblem_AreHeldBack_ThenCounted`. ALRT-02 is
**[auto]** in `AlertTests.ACommandThatFails_RaisesAnUrgentAlert_EvenWhenTheErrorReplyFails`. The key and cooldown
logic are unit-tested in `Grimoire.Tests/Features/Alerts/AlertKeyTests.cs` and `AlertCooldownTests.cs`. Phase 10 added ALRT-08
to 10 and 13 in `AlertGapTests`, and ALRT-11 in `AlertMessageFormatterTests`. ALRT-07 is not automated.

## B. Warnings, flush and the daily report

Warnings are not posted when they happen. `WarningBuffer` merges them by key in memory (up to 200 keys; new keys
beyond that are dropped, existing keys still count). Every 30 s, `WarningFlushService` drains the buffer into
`AlertOccurrences`, adding to the open row for each key. Once a day, at or after `warningReportTimeUtc`,
`DailyWarningReportService` deletes rows last seen more than 30 days ago, posts the open rows to the
`warningChannelId` channel and marks them reported.

The report is a yellow embed titled "Daily warning report". Each entry is `**N×** <Type>`, then the exception type in
backticks if any, then `Last <relative time>` and `· K guild(s)`, then `> ` and the sample message cut to 150
characters. At most 15 entries fit. When some don't, the last line is "…and N more warning types, in the attached
file." and `warning-report-YYYY-MM-DD.txt` lists every warning in full. The footer is "N warning types, M occurrences".
A warning's type in the report includes its discriminator, for example `GuildIssue (audit-log.forbidden)`.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| ALRT-20 | Report time later today | Raise one warning (ALRT-40) | Nothing in `#alerts` or `#warnings` | After about 30-60 s, one `AlertOccurrences` row: `AlertType` "GuildIssue (audit-log.forbidden)", `Count` 1, `GuildIds` holds Test-A, `ReportedAt` null |
| ALRT-21 | ALRT-20 done | Raise the same warning twice more, in Test-A and Test-B **[auto]** `AlertGapTests.RepeatsInTwoGuilds_AddToOneRow_AndTheReportShowsBoth` | Nothing posted | Same row, `Count` 3, `GuildIds` holds both guilds, `LastSeen` moved on |
| ALRT-22 | ALRT-21 done | Wait for the report time **[auto]** `AlertGapTests.RepeatsInTwoGuilds_AddToOneRow_AndTheReportShowsBoth` | `#warnings` gets "Daily warning report" with "**3×** GuildIssue (audit-log.forbidden)", "· 2 guild(s)" and the summary as the sample. Footer "1 warning types, 3 occurrences" | `ReportedAt` set on the row |
| ALRT-23 | ALRT-22 done | Raise the warning again and wait 5 min | No second report today | A new open row for the same key, `Count` 1. It goes in tomorrow's report |
| ALRT-24 | `warningReportTimeUtc` an hour ahead | Raise a warning, wait a few minutes | No report before that time | Row stays open |
| ALRT-25 | 16 or more distinct warning types open | Wait for the report **[auto]** `AlertGapTests.ALongReport_SaysHowManyMoreAreInTheFile` | Up to 15 entries, then "…and N more warning types, in the attached file.", and a `warning-report-<date>.txt` attachment with every type | Every open row is marked reported, shown or not |
| ALRT-26 | `warningChannelId` empty, restart | Raise a warning, pass the report time **[auto]** `AlertGapTests.WithoutAWarningChannel_TheDayIsStillMarkedDone` | No report | Row stays open. The day is marked done, so it is not retried until tomorrow |
| ALRT-27 | `warningChannelId` set to a deleted channel | Raise a warning, pass the report time **[auto]** `AlertGapTests.AMissingWarningChannel_KeepsRowsOpen_AndIsRetried`, `AMissingWarningChannel_IsLogged` (skipped, KI-92) | No report | Row stays open. Retried every minute with no log line. Rows still open after 30 days are deleted unreported |
| ALRT-28 | `warningChannelId` set to `#private` (bot can't send) | Raise a warning, pass the report time **[auto]** `AlertGapTests.ARefusedWarningChannel_FailsEachTick_AndKeepsRowsOpen` (in part: the urgent escalation needs the real one-minute timer) | After three failed minutes, `#alerts` gets `**[URGENT]** BackgroundTaskFailure` `` `DailyWarningReportService` `` "DailyWarningReportService failed 3 time(s) in a row: ...". Then one post every 5 min with a repeat count | Error log every minute. Rows stay open |
| ALRT-29 | A warning flushed (row open) | Restart the bot before the report time **[auto]** `AlertGapTests.AFlushedWarning_SurvivesARestart` | Report posts at the report time as usual | The row survived the restart |
| ALRT-30 | A warning raised less than 30 s ago, not yet flushed | Stop the bot cleanly, then start it **[auto]** `AlertGapTests.AnUnflushedWarning_SurvivesACleanRestart` (skipped, KI-94) | **Expected:** the warning is in the next report. **Actual:** it is lost: nothing flushes the buffer on shutdown (observation 4) | No row for it |
| ALRT-31 | Today's report already posted | Raise a new warning, let it flush, restart the bot **[auto]** `AlertGapTests.AfterTodaysReport_ARestartDoesNotPostASecondOne` (skipped, KI-95) | **Expected:** one report per day. **Actual:** a second report today with just the new warning, because the "reported today" date is kept in memory (observation 5) | The new row is marked reported |
| ALRT-32 | Database stopped | Raise a warning, wait 60 s, start the database | Nothing posted for it | Error log "Failed to persist 1 buffered warnings; the batch was dropped". The warning is lost by design |
| ALRT-33 | A row seeded with SQL with `LastSeen` 31 days ago | Pass the report time | The old row is not in the report | The row is deleted |
| ALRT-34 | `warningReportTimeUtc` set to `nine` | Restart, raise a warning | The report comes at 09:00 UTC (the default) | none |

**[auto]**: ALRT-20 to 23 in `AlertTests.Warnings_AreFlushed_ThenReportedOnceADay`, ALRT-24 in
`TheReport_WaitsForItsTimeOfDay`, ALRT-25 in `ALongReport_AttachesTheFullReportAsAFile`, ALRT-26 in
`WithoutAWarningChannel_NoReportIsPosted_AndWarningsStayOpen`. The buffer, store and formatter are unit-tested in
`Grimoire.Tests/Features/Alerts/` (`WarningBufferTests`, `AlertOccurrenceStoreTests`, including ALRT-33's
`DeleteOlderThan_removes_rows_last_seen_before_the_cutoff`, and `WarningReportFormatterTests`). Phase 10 added ALRT-27
to 31 in `AlertGapTests` (ALRT-28 only in part: its urgent escalation needs the real timer loop). ALRT-32 and 34 are
not automated.

## C. Setup problems (`GuildIssueReporter`) and the guild log queue

A setup problem is something only the guild's moderators can fix. Each report logs a warning ("Guild {GuildId} has a
setup problem: {Code}") and sends a `GuildIssue` warning alert every time. It is posted to the guild's Moderation log
at most once a day per guild and code. The post is a yellow embed with author "Setup problem", the description
`<summary>\n**Suggested fix:** <fix>`, then "This happened N more time(s) since it was last shown." when repeats were
held back. The footer is "Shown at most once a day for each problem. Run /Diagnostics Check to review mutes and
locks."

All guild logs go through one queue (`GuildLog`, 1024 messages; senders wait when it's full). `GuildLogPublisher`
skips a message when the log type's module is off or its channel is unset. The Moderation log belongs to the General
module. Discord 5xx errors are retried four times with backoff.

The codes, and where they come from:

| Code | Summary | Raised by | Area |
|------|---------|-----------|------|
| `bot-missing-permissions:<command>` | "Discord refused `<command>`: I do not have permission to do that." | Any command Discord refuses with 403 | 02 |
| `leveling.reward-roles-forbidden` | "I could not give the level reward roles <roles>." | `GainUserXp` | 06 |
| `leveling.reward-roles-deleted:<ids>` | "These level rewards were removed because their roles were deleted:" | `GainUserXp` | 06 |
| `channel.cannot-send:<id>` | "I could not post in <#id>." | `GuildLogPublisher`, `TextCustomCommandProcessor` | here, 07 |
| `log.channel-missing:<type>` | "The <type> log is set to <#id>, which no longer exists, so those logs are lost." | `GuildLogPublisher` | here |
| `lock.cannot-unlock:<id>` | "I could not unlock <#id>. It is still locked, and I will keep retrying." | `LockBackgroundTasks` | 02 |
| `mute.cannot-unmute:<id>` | "I could not remove the mute role from <@id>. They are still muted, and I will keep retrying." | `MuteBackgroundTasks` | 02 |
| `mute.role-missing` | "A mute expired but the configured mute role no longer exists, so I could not remove it from the member." | `MuteBackgroundTasks` | 02 |
| `audit-log.forbidden` | "I cannot read the audit log, so I cannot tell who deleted messages or banned members outside my commands." | `InviteEvents`, `BanAddedEvent`, `DiscordAuditLogParserService` | here, 02, 05 |

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| ALRT-40 | Bot lacks View Audit Log in Test-A | `admin` revokes an invite | `#modlog` gets "Setup problem": "I cannot read the audit log, so I cannot tell who deleted messages or banned members outside my commands.\n**Suggested fix:** Give my role the **View Audit Log** permission." with the footer above | Warning log "Guild <id> has a setup problem: audit-log.forbidden". A `GuildIssue` warning (ALRT-20) |
| ALRT-41 | ALRT-40 done | Revoke two more invites **[auto]** `GuildIssueThrottleTests.RepeatsTheSameDay_AreNotPosted_ButAreCounted` | No new post | Two more warning logs. The row's `Count` goes up after the flush |
| ALRT-42 | ALRT-41 done | Wait a day and revoke another (or automated only) | Posted again, ending "This happened 2 more time(s) since it was last shown." | none |
| ALRT-43 | ALRT-40 done | Trigger a different issue the same day, for example `/Lock` with Manage Channels removed | Its own "Setup problem" post ("Discord refused `Lock`: ...") as well as the command's reply | none |
| ALRT-44 | ALRT-40 done in Test-A | Repeat ALRT-40 in Test-B **[auto]** `GuildIssueThrottleTests.TheSameProblemInAnotherGuild_IsPostedThereToo` | Posted in Test-B's mod log: guilds are throttled separately | none |
| ALRT-45 | ALRT-40 done | Restart the bot, repeat ALRT-40 | Posted again at once, with no repeat count: the throttle starts empty | none |
| ALRT-46 | Mod log unset in Test-A | Repeat ALRT-40 **[auto]** `GuildIssueThrottleTests.WithoutAModLog_NothingIsPosted_ButTheProblemIsLoggedAndCounted` | Nothing posted | Warning log only |
| ALRT-47 | ALRT-46 done | Set the mod log to `#modlog`, repeat ALRT-40 the same day **[auto]** `GuildIssueThrottleTests.AProblemWithNowhereToGo_IsShownOnceAModLogIsSet` (skipped, KI-96) | **Expected:** the problem is shown now that there is a log. **Actual:** nothing, until a day after ALRT-46: the throttle counted the post that had nowhere to go (observation 6) | Warning log |
| ALRT-48 | MessageEdited log set to a channel, then that channel deleted | `user1` edits a logged message | `#modlog` gets "Setup problem": "The MessageEdited log is set to <#id>, which no longer exists, so those logs are lost.\n**Suggested fix:** Set a new channel for it, or turn that log off." | The edit log is lost |
| ALRT-49 | MessageEdited log set to `#private` (bot can see but not send, or can't see) | `user1` edits a logged message | `#modlog` gets "Setup problem": "I could not post in <#private>." with "Give my role **View Channel** and **Send Messages** in that channel, and check its permission overwrites." | Error log "Failed to process guild log. GuildId=..., GuildLogType=MessageEdited" |
| ALRT-50 | Mod log set to `#private` | Trigger any mod log entry, such as `/Warn` | Nothing posted | Error log for the entry, then one more for the "Setup problem" post about `#private`, which also fails. The throttle stops it there. Later entries fail with one error each, and nothing tells the guild except the daily report |
| ALRT-51 | Mod log channel deleted | Trigger a mod log entry **[auto]** `GuildIssueThrottleTests.ADeletedModLog_IsOnlyInTheDailyReport` | Nothing posted; the guild is not told | `log.channel-missing:Moderation` is reported, its post fails the same way and is then throttled. Only the daily warning report shows it |
| ALRT-52 | Message Log module off | Edit a logged message | Nothing posted | No error: disabled modules are skipped quietly |
| ALRT-53 | A message log entry posted | Check the DB | none | An `OldLogMessages` row for the post (edits, deletes and bulk deletes only). Area 05 checks the cleanup |
| ALRT-54 | Messages queued in the log | Stop the bot | Queued messages are not posted | Documented in `GuildLog.Complete` |

**[auto]**: ALRT-40 to 42 in `GatewayAndIssueTests.AnIssue_PostsOncePerDay_AndCountsRepeats`, ALRT-43 in
`DifferentIssues_AreThrottledSeparately`, ALRT-40's invite trigger in
`GuildSyncAndInviteTests.AnInviteDeleted_WithoutViewAuditLog_IsReportedToTheGuild_NotRaisedAsAnError`, ALRT-48 and 49
in `MessageLogEventsTests.ADeletedLogChannel_IsReportedToTheGuild` and
`ALogChannelTheBotCannotPostIn_IsReportedToTheGuild`. ALRT-44 is unit-tested in
`GuildIssueTests.Guilds_and_codes_are_throttled_independently`. The "Discord refused" reply for ALRT-43 is checked in
`LockCommandTests.Lock_ThatDiscordRefuses_ShowsTheMissingPermissionsHelp`. ALRT-45 to 47 and 50 to 54 are not
automated.

## D. Gateway health (`GatewayEvents`, `GatewayWatchdogService`)

`GatewayEvents` marks the gateway down on a socket close or a zombied connection, and up again on a new or resumed
session. A reconnect after known downtime sends a warning: `GatewayResumed` ("Gateway session resumed after Ns.") or
`GatewaySessionRecreated` ("Gateway reconnected with a new session after Ns; events during the gap were missed.").
The watchdog checks every 30 s. Once the gateway has been down for 2 minutes it sends an urgent `GatewayDisconnected`
alert: "Discord gateway has been disconnected for N minutes." The alert cooldown collapses the repeats.

KI-48 is open: DSharpPlus never dispatches the close and zombied events, and doesn't forward RESUMED. So the bot never
learns the gateway is down, and none of these alerts fire against real Discord.

To drop the gateway but keep REST working, block the gateway hosts (`gateway.discord.gg` and the
`gateway-*.discord.gg` resume hosts) with a firewall rule, and leave `discord.com` open. Without REST the urgent alert
can't be posted anyway.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| ALRT-60 | Bot stopped | Start the bot | No alert | Info log "Gateway session created for 2 guilds (down for s)" with no downtime value |
| ALRT-61 | Bot running | Block the gateway for 3 min | **Expected:** `#alerts` gets "**[URGENT]** GatewayDisconnected\nDiscord gateway has been disconnected for 2 minutes." about 2 min in. **Actual:** nothing (KI-48) | **Expected:** warning log "Gateway socket closed: ..." or "Gateway connection zombied after N failed heartbeats". **Actual:** neither (KI-48) |
| ALRT-62 | As ALRT-61 | Keep it blocked for 8 min | **Expected:** a second post about 5 min after the first, with "— repeated N more times since the last alert". **Actual:** nothing (KI-48) | none |
| ALRT-63 | As ALRT-61 | Unblock within a minute or two, so the session resumes | **Expected:** no urgent post; a `GatewayResumed` row in the next report. **Actual:** no row (KI-48) | **Expected:** info log "Gateway session resumed (down for Ns)" |
| ALRT-64 | As ALRT-61 | Keep it blocked long enough that Discord drops the session, then unblock | **Expected:** a `GatewaySessionRecreated` row in the next report. **Actual:** no row: the bot didn't know it was down (KI-48). The startup sync (section E) runs again on the new session | Info log "Gateway session created ..." |
| ALRT-65 | Bot running | Block the gateway for under 2 min | No urgent post | none |
| ALRT-66 | none | Discord asks the bot to reconnect (op 7) | The bot resumes and commands still work | Can't be forced by hand |

**[auto]**: the watchdog part of ALRT-61 in `GatewayAndIssueTests.TheWatchdog_AlertsAfterTwoMinutesDown` (it marks
the gateway down directly), ALRT-66 in `AReconnectRequest_IsResumed_AndCommandsStillWork`. The detection in ALRT-61
and the warning in ALRT-63 are skipped tests that fail until KI-48 is fixed: `ADroppedConnection_IsNoticed` and
`AResumeAfterDowntime_IsRecordedAsAWarning`. `GatewayHealthTests` covers the downtime clock. ALRT-60, 62, 64 and 65 are
not automated.

---

## E. Startup sync (`UpdateAllGuilds`)

When the guild download completes, the bot sets its status to "Watching N servers.", adds any missing username,
nickname and avatar history for every cached member of every guild, stores each guild's invites in `InviteService`,
and saves. Usernames are global; nicknames and avatars are per guild. A guild where the bot lacks Manage Server is
tracked with no invites.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GLD-01 | Fresh database, bot in Test-A and Test-B | Start the bot | Status "Watching 2 servers." | A `UsernameHistory` row per member, `NicknameHistory` and `Avatars` rows per member per guild |
| GLD-02 | GLD-01 done | Restart with no profile changes | none | No new rows |
| GLD-03 | Bot stopped | `user1` changes username, server nickname and avatar, then start the bot | No "Username Updated", "Nickname Updated" or "Avatar Updated" logs: the sync is silent **[auto]** `GuildSyncGapTests.GLD03_ProfileChangesWhileTheBotWasOff_AreStoredSilentlyAtStartup` | One new row in each table for `user1` |
| GLD-04 | Fresh database; `user1` is in both guilds | Start the bot | none **[auto]** `GuildSyncGapTests.GLD04_AMemberOfTwoGuilds_GetsOneUsernameRowAtStartup`, skipped (KI-97, confirmed) | **Expected:** one `UsernameHistory` row for `user1`. **Suspected actual:** two, one per guild, because the second guild's query can't see the first guild's unsaved row (observation 7) |
| GLD-05 | Bot lacks Manage Server in Test-B | Start the bot | Startup still completes **[auto]** `GuildSyncGapTests.GLD05_AGuildWithoutManageServer_DoesNotStopTheOtherGuildsSync` (two guilds) | History rows for Test-B. The bot does not request Test-B's invites |
| GLD-06 | Bot just started | A member joins within the first seconds | Join may be logged with "Unknown Invite", or not at all | KI-52 (production window). An invite created in that window throws "Could not find guild." and raises an urgent `UnhandledException` |
| GLD-07 | Discord fails the invite list for one guild during startup (can't be forced by hand) | Start the bot | **Expected:** other guilds still sync. **Actual:** the whole sync stops: no history is saved and no guild's invites are tracked, so every join throws "Could not find guild." until a restart (observation 8) **[auto]** `GuildSyncGapTests.GLD07_OneGuildsInviteListFailing_DoesNotStopTheOtherGuildsSync`, skipped (KI-89) | none |

**[auto]**: GLD-01 in `GuildSyncAndInviteTests.Startup_StoresEveryMembersUsernameNicknameAndAvatar`, GLD-02 in
`Restarting_DoesNotDuplicateHistory`, GLD-05 in `WithoutManageServer_StartupStillSyncs_AndJoinsAreLoggedWithNoInvite`.
Phase 10 added GLD-03, 04 and 07 in `GuildSyncGapTests`. GLD-06 stays manual (KI-52 timing).

## F. Joining a guild (`GuildAdded`)

When the bot is added to a guild, it updates its status, adds that guild's missing history and stores its invites.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GLD-20 | Bot kicked from Test-B | Re-add it with Manage Server | Status "Watching 2 servers." **[auto]** `GuildSyncGapTests.GLD20_ReaddingTheBot_SyncsTheGuild_AndTracksItsInvites` | History rows for any Test-B members missing them. A join in Test-B then shows the invite used (GLD-40) |
| GLD-21 | Bot in both guilds | Kick the bot from Test-B | **Expected:** status "Watching 1 servers.". **Actual:** still "Watching 2 servers." until the next guild join or new session: nothing handles a guild being removed (observation 9) **[auto]** `GuildSyncGapTests.GLD21_TheBotLeavingAGuild_UpdatesTheStatus`, skipped (KI-100) | none |
| GLD-22 | Bot kicked from Test-B | Re-add it without Manage Server | No error **[auto]** `GuildSyncGapTests.GLD22_ReaddingTheBotWithoutManageServer_SyncsTheGuild_WithNoInvites` | History rows. Joins in Test-B show "Unknown Invite" |
| GLD-23 | GLD-22 done; Test-B has two invites, A and B | Grant the bot Manage Server, then `user2` joins with invite B | **Expected:** "Invite Used" shows B. **Actual:** it shows whichever invite the bot lists first, A or B: the guild was tracked with no invites, so every invite looks new (observation 10). Each later join can be wrong the same way until each invite has been seen once, or until a restart **[auto]** `GuildSyncGapTests.GLD23_AfterGettingManageServer_AJoin_ShowsTheInviteUsed`, skipped (KI-90) | none |

None of section F is automated. The shared helpers are covered through section E's tests.

## G. Invites (`InviteEvents`, `InviteService`)

On each join, the bot lists the guild's invites and compares them with the tracked ones. The first invite whose use
count changed, or that is new, is the one used. If none changed, a tracked invite that disappeared and had one use left
is taken as a used-up invite, with its count raised by one. The "Invite Used" field reads
`<url> (<uses> uses)\n**Created By:** <creator>`, or "Vanity Invite" if the guild has a vanity URL, or
"Unknown Invite".

A created invite is added to the tracked list. A deleted invite is forgotten if it had expired, or if the newest
InviteDelete audit entry, at most 1.5 s old, is for it. Otherwise it is kept, as if it had been used up.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GLD-40 | Invite `abc` by `mod` with 4 uses, and another invite | `user2` joins with `abc` | `#userlog` "User Joined", "Invite Used" "https://discord.gg/abc (5 uses)\n**Created By:** mod" | none |
| GLD-41 | A single-use invite by `admin`, unused | `user2` joins with it | "https://discord.gg/<code> (1 uses)\n**Created By:** admin" | none |
| GLD-42 | Bot running | `admin` creates a new invite, `user2` joins with it | That invite, "(1 uses)" **[auto]** `GuildSyncGapTests.GLD42_AnInviteCreatedWhileRunning_IsTracked_AndShownWhenUsed` | none |
| GLD-43 | Invites `first` and `second` | `user1` joins with `first`, then `user2` with `second` | Each join shows its own invite | none |
| GLD-44 | An invite Discord sends with no inviter | Join with it | "**Created By:** Unknown" | Automated only |
| GLD-45 | Test-A has a vanity URL (needs boost level 3; skip if not) | `user2` joins with the vanity URL | "Vanity Invite" **[auto]** `GuildSyncGapTests.GLD45_AJoinThroughTheVanityUrl_ShowsVanityInvite` | none |
| GLD-46 | Bot lacks Manage Server | `user2` joins with any invite | "Unknown Invite" | No invite request, no error |
| GLD-47 | Bot has View Audit Log | `admin` revokes an invite | none **[auto]** `GuildSyncGapTests.GLD47_ARevokedInvite_IsForgotten` | Forgotten. No warning log |
| GLD-48 | Bot lacks View Audit Log | `admin` revokes an invite | ALRT-40's "Setup problem" | The invite is kept |
| GLD-49 | Bot lacks View Audit Log; two unused single-use invites A and B | Revoke A, then `user2` joins with B | **Expected:** B. **Actual:** A or B: both are tracked, both are gone, and both had one use left (observation 11) **[auto]** `GuildSyncGapTests.GLD49_WithoutViewAuditLog_AJoinThroughASingleUseInvite_ShowsThatInvite`, skipped (KI-98) | none |
| GLD-50 | Invite with a 30 min expiry | Wait for it to expire | none **[auto]** `GuildSyncGapTests.GLD50_AnExpiredInvite_IsForgottenWithoutAnAuditLookup`, skipped (KI-108) | Forgotten without an audit lookup |
| GLD-51 | User Log off | `user2` joins | No join log **[auto]** `GuildSyncGapTests.GLD51_GLD64_WithTheUserLogOff_AJoinIsNotLogged_ButHistoryIsStillAdded` | History is still recorded (section H) |

**[auto]**: GLD-40 in `GuildSyncAndInviteTests.AJoin_ShowsTheInviteThatWasUsed`, GLD-41 in
`AJoin_WithASingleUseInvite_ShowsIt` and `AJoin_WithASingleUseInvite_CountsTheUse` (KI-50, fixed), GLD-43 in
`ConsecutiveJoins_AreMatchedToTheirOwnInvites`, GLD-44 in `AJoin_WithAnInviteThatHasNoInviter_ShowsItsCreatorAsUnknown`,
GLD-46 in `WithoutManageServer_StartupStillSyncs_AndJoinsAreLoggedWithNoInvite`, GLD-48 in
`AnInviteDeleted_WithoutViewAuditLog_IsReportedToTheGuild_NotRaisedAsAnError`. "Unknown Invite" is also checked by
`UserLogTests.Join_IsLogged_WithTheInviteUnknown`. Phase 10 added GLD-42, 45, 47, 49, 50 and 51 in `GuildSyncGapTests`.

## H. Member joins (`MemberAdded`)

On every join, whatever modules are on, the bot compares the member's username, guild nickname and avatar with the
newest stored rows and adds a row for each that changed. Usernames and nicknames compare ignoring case. A member with
no nickname and no stored nickname gets no nickname row.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GLD-60 | A user the bot has never seen | They join Test-A | none | `UsernameHistory` and `Avatars` rows. A `NicknameHistory` row only if Discord gives a nickname on join |
| GLD-61 | `user1` left Test-A, profile unchanged | `user1` rejoins, then gets a role | No "Avatar Updated" log after the rejoin | No new rows (KI-49, fixed; rows stored the old way cause one last false log) |
| GLD-62 | `user1` left, then changed username | `user1` rejoins | No "Username Updated" log **[auto]** `GuildSyncGapTests.GLD62_ARejoinAfterAUsernameChange_StoresIt_WithoutAUsernameLog` | A new `UsernameHistory` row |
| GLD-63 | `user1` had a nickname, left (nickname lost) | `user1` rejoins | none **[auto]** `GuildSyncGapTests.GLD63_ARejoinAfterLosingANickname_StoresARowWithNoNickname` | A `NicknameHistory` row with no nickname |
| GLD-64 | User Log off | `user1` leaves and rejoins after a username change | none **[auto]** `GuildSyncGapTests.GLD51_GLD64_WithTheUserLogOff_AJoinIsNotLogged_ButHistoryIsStillAdded` | Rows are still added |

**[auto]**: GLD-60 in `GuildSyncAndInviteTests.ANewMember_GetsHistoryWhenTheyJoin`, GLD-61 in
`ARejoin_DoesNotCauseAFalseAvatarChange`. Phase 10 added GLD-62 to 64 in `GuildSyncGapTests`.

## Findings

Record failures from manual runs here.

## Code observations

1. **new. A failed urgent post still uses up the cooldown.** `AlertService.cs:66` takes the cooldown before the
   channel is fetched or the post is sent (`:68-69`). If the post fails (403, timeout, KI-54), that alert is lost,
   repeats in the next 5 minutes are held back, and their count is reset, so the next post under-reports them. Low.
2. **new. An urgent channel that doesn't exist loses alerts without a trace.** `AlertService.cs:68` returns quietly
   when the channel can't be found, and the cooldown was already taken. Nothing is logged, so a deleted `#alerts`
   goes unnoticed. Low. The daily report has the same silence at `DailyWarningReportService.cs:53`, though it keeps
   its rows open.
3. **new. Truncating an urgent post can cut off the code fence.** `AlertMessageFormatter.cs:27` cuts the whole text
   at 1997 characters, so a long exception leaves the `csharp` block unclosed. Cosmetic.
4. **new. Buffered warnings are lost on shutdown.** `WarningFlushService` (`WarningFlushService.cs:15`) has no
   `StopAsync` flush, and `AlertService.StopAsync` drops whatever is still queued. Up to 30 s of warnings are missing
   from the report after each restart, though the class says the report "survives restarts". Low.
5. **new. A restart after the day's report can post a second report.** `_lastReportDate`
   (`DailyWarningReportService.cs:26`) lives in memory. After a restart past the report time, the first tick posts
   any rows opened since the earlier report. Low; may be acceptable. A related small race: a flush between
   `GetOpenAsync` (`:49`) and `MarkReportedAsync` (`:56`) adds counts to a row that is then marked reported without
   them being shown.
6. **new. The issue throttle counts posts that went nowhere.** `GuildIssueReporter.cs:39` takes the daily throttle
   before the message is queued. If the mod log is unset, or the post fails, the problem stays hidden for a day even
   after the log is fixed (ALRT-47). It also means a guild whose mod log is broken never hears about it (ALRT-50, 51).
   Low.
7. **new (suspected). Startup sync can add duplicate username rows.** `UpdateAllGuilds.cs:30-47` uses one context for
   every guild and saves once at the end. `AddMissingUsernameHistoryAsync` (`UserDatabaseQueryHelpers.cs:17-29`)
   queries with `AsNoTracking`, so it can't see the rows added for an earlier guild. A member in two guilds whose
   username isn't stored gets one row per guild, a few microseconds apart (GLD-04). Low: duplicate history lines.
8. **new. One failed invite list stops the whole startup sync.** `UpdateAllGuilds.cs:41` awaits
   `GetGuildInvitesAsync` inside the loop with no handling. A Discord error for one guild skips the save
   (`:51-52`) and `UpdateAllInvites` (`:49`), so no guild is tracked and every join throws "Could not find guild."
   (`InviteService.cs:41`) until a restart. `GuildAdded.cs:36` has the same order: a failure there loses that
   guild's history. Med.
9. **new. The status count isn't updated when the bot leaves a guild.** Only `GuildAdded.cs:23` and
   `UpdateAllGuilds.cs:22` set it; there is no `GuildDeleted` handler. Cosmetic.
10. **new. A guild tracked with no invites matches joins to the wrong invite.** A guild synced without Manage Server
    is stored with an empty list (`GuildAdded.cs:36-37`, `UpdateAllGuilds.cs:41-42`). If the bot later gets Manage
    Server, `CalculateInviteUsed` (`InviteService.cs:46-51`) treats every listed invite as new and returns the first
    one, so the join log names an arbitrary invite. It only adds that one invite (`:54`), so later joins can be wrong
    too, until a restart. A missed InviteCreate (for example during a gateway gap, KI-48) causes the same mismatch
    for that invite. Med: wrong data in the join log.
11. **new. An invite kept after a delete can be blamed for a later join.** When the audit log can't confirm a delete,
    `InviteEvents.cs:44-64` keeps the invite. If it was an unused single-use invite, the fallback in
    `InviteService.cs:58-68` can pick it for a later join through another single-use invite (GLD-49). Low.
12. **KI-48.** The gateway alerts can't fire (section D).
13. **KI-52.** Joins and invite creates in the first moments after startup find no tracked guild (GLD-06). The
    `ArgumentException` from `InviteService.UpdateInvite` (`InviteService.cs:28`) is not in the noisy list in
    `CommandHandler.SendErrorAlert`, so each one is an urgent alert.
14. **KI-54.** A post after a timed-out request can fail inside DSharpPlus (ALRT-12).
15. **KI-49 and KI-50** are fixed. Their tests are listed in sections H and G.
