# Branch Review: feature/reorganizing vs main

A section-by-section review of the working tree against `main`. Findings only: nothing here is changed until the
owner picks what to fix.

## How to run a section

Each section can start from a fresh session (`/clear`, then "continue the branch review").

1. Read this file, then `docs/domain-model` only if the section needs it.
2. Take the first section marked `[ ]`. Read only its paths, and `git show main:<path>` where the old version matters.
3. Check each finding against the code before writing it down.
4. Add findings to the section, mark it `[x]`, and stop to report.

## What counts as a finding

Check against the ten coding principles in project memory, plus:

- **Correctness and reliability**: bugs, races, lost data, behaviour that regressed from `main`.
- **Performance**: repeated queries, missing indexes, work done on every message or event.
- **Without bloat**: a finding should remove code or complexity, or fix a real defect. If a fix adds more code than it
  saves and only improves style, leave it out or mark it `optional`.

Known issues in `docs/testing/known-issues.md` and past decisions in project memory are not re-reported.

Each finding has a severity (**high**, **med**, **low**, **optional**), a location, the problem and a suggested fix.

## Sections

| # | Status | Section | Paths | Lines |
|---|--------|---------|-------|-------|
| 1 | `[x]` | Domain model | `Grimoire.Domain` | 2,250 |
| 2 | `[x]` | Persistence | `Grimoire.Data/Configuration`, `GrimoireDbContext`, recent migrations | 1,300 |
| 3 | `[x]` | Settings | `Grimoire.Settings` | 3,250 |
| 4 | `[x]` | Shared infrastructure | `Grimoire/Extensions`, `Grimoire/Utilities`, `Grimoire/Features/Shared` | 4,000 |
| 5 | `[x]` | Moderation | `Grimoire/Features/Moderation` | 4,000 |
| 6 | `[x]` | Logging | `Grimoire/Features/Logging`, `LogCleanup` | 1,750 |
| 7 | `[x]` | Leveling | `Grimoire/Features/Leveling` | 1,300 |
| 8 | `[x]` | Custom commands | `Grimoire/Features/CustomCommands` | 1,200 |
| 9 | `[x]` | Roll-up | Duplication across sections, regressions from `main`, a ranked fix list | n/a |

## Findings

### Section 1: Domain model

Reviewed 2026-10-31. Member usage was checked by building a copy with every `Result` and `Validation` member marked
`[Obsolete]`, so "unused" means no production code calls it. Tests in `Grimoire.Domain.Test.Unit` do cover some of these
and would go with them.

**D-1 (med): one bad legacy row can break every query that loads it.** `CustomCommandName`, `CustomCommandContent` and
`CustomCommandEmbedColor` map through `ParseFromDatabase`, which reruns `Create` and throws on failure. `main` was looser:
it let a whitespace-only embed colour through, and `Create` refuses that. The `ModelRedesign` migration doesn't clean these
columns, so one such row throws for any query that materialises it, including lists. Every other value object uses
`FromDatabase`, and `XpHistory.cs` states the rule: "DB data is trusted post-migration". Content also gets its `\n`
replacement rerun on every read. Fix: use `FromDatabase` for these three, or check production data first. Removes code.

**D-2 (med): the sin-with-reason block is copied five times.** `Warn`, `KickUser`, `MuteUser`, `SpamEvents` and
`BanAddedEvent` each build a `SinReasonHistory` by hand, with `SinId = default` and two `throw new UnreachableException()`
guards. That is 10 of the 27 unreachable throws outside the domain. Fix: give the `Sin.ForX` factories an optional
`ModerationReason?` and build the history inside `Sin`. Saves about 70 lines, and `SinId = default` goes away.

**D-3 (med): `Result` carries unused cases and repeats itself.** `Conflict` and `Forbidden` are never constructed.
`Match` and `MatchAsync` take four optional handlers that no caller passes. Each of `Map`, `Bind`, `BindAsync` and the
`Match` overloads repeats the same five failure arms. Fix: delete the two cases and the optional handlers, then have one
private conversion carry a failure to `Result<TOut>`. `error-result-validation-strategy.md` needs updating to match.

**D-4 (low): unused functional helpers.** All of these can be deleted:

- `ValidationTaskExtensions.cs`, the whole file (64 lines).
- `Validation`: `BindAsync`, `MapAsync`, `MatchAsync`, `OrElse`, `Tap`, `TapAsync`.
- `Result`: `Tap`, `MapAsync`, `ToValidation`.
- `ResultTaskExtensions`: `Bind`, `MapAsync`, `Tap`, `ToResult`.
- `Validation.Combine`: only the four-argument form is used, out of five.
- `Result.WhenAll`: only the six-argument form is used, out of six overloads.

**D-5 (low): factories that can't fail still return `Validation`.** `EarnedXp.Create`, `ReclaimedXp.Create`,
`TextCustomCommand.Create` and `EmbedCustomCommand.Create` have no failure path, so callers need an impossible branch, as
`GainUserXp` and `ReclaimUserXp` do. Principle 10 says the signature should show this. Fix: return the type directly.

**D-6 (low): validated constructors are dead while the unvalidated path is live.** `Create` has no production caller for
`Username`, `Nickname`, `AvatarFileName`, `AttachmentFileName`, `InviteCode` or `InviteUrl`. The bot calls the internal
`FromDatabase` instead, in five places, on data straight from Discord. That is why `Grimoire` needs `InternalsVisibleTo`,
which `CLAUDE.md` doesn't mention. `RawXp` is the only other internal it uses. Fix: replace `FromDatabase` at those call
sites with one public trusted factory such as `FromDiscord`, and delete the unused `Create` methods and their limits, or
call `Create` at the boundary if the limits matter.

**D-7 (low): `default` is a reachable state for value objects.** They are structs, so `FirstOrDefaultAsync` returns one
with a null `Value`. `UpdatedAvatarEvent` and `UpdatedUsernameEvent` both do this, so a member with no history logs a change
from nothing. KI-40 was the same trap. Fix in section 6: cast to the nullable type in the `Select`, as `MemberAdded` does.

**D-8 (low): the custom command factories duplicate a switch they don't need.** Both `Create` methods switch on the role
type to apply `with { CreatedAt = now }`. `with` on the base record already keeps the runtime type, so one shared line
replaces both, and the `UnreachableException` goes. They also read `DateTimeOffset.UtcNow` themselves, while `Sin` takes the
time as a parameter.

**D-9 (low): unused members on the ids.** `TryParse` exists on eight id types and only `ChannelId`'s is called. The two
explicit `UserId` and `ModeratorId` conversions have no production callers.

### Section 2: Persistence

Reviewed 2026-10-31. `dotnet ef migrations has-pending-model-changes` reports no drift. Index findings come from reading
every query in `Grimoire/Features` against the model, not from `EXPLAIN` on production data. Nobody has run the checks
below against production yet.

**P-1 (med): the migration turns legacy deny roles into allow roles.** On `main`, `RestrictedUse = false` makes a command's
roles a deny list (`GetCustomCommand.IsUserAuthorized`). `ModelRedesign` phase 4 marks every role `Allow`, and its comment
and `spec-custom-command-role-policy.md` both say all legacy roles were allow-type. So a command that blocked a role would
become usable only by that role. `main`'s `Learn` can't add roles any more, so only older rows are affected. The reverse
also changes: `RestrictedUse = true` with no roles meant nobody on `main`, and with no roles the branch allows everyone.
Fix: `"RoleType" = CASE WHEN cc."RestrictedUse" THEN 'Allow' ELSE 'Deny' END`, which is one line in an undeployed
migration, and correct the spec. The no-roles case needs an owner decision if the check finds any rows.

**P-2 (med): who reclaimed XP is lost for good.** `main`'s `ReclaimUserXp` stores the moderator in `AwarderId`.
`spec-xp-history-redesign.md` treats that column as valid only for `Awarded`, so `ModelRedesign` phase 7 nulls it on
`Reclaimed` rows as "stale", and `ReclaimedXp` has nowhere to store a moderator. Existing attribution is erased and new
reclaims have none in the database. Fix: add a `ModeratorId?` to `ReclaimedXp` mapped to `AwarderId`, nullable because old
rows may lack it, and change the cleanup to skip type 2. This adds a little code but fixes a regression. It must be decided
before deploy, because the spec itself notes the nulls can't be recovered.

**P-3 (med): the first deploy can hit the 30-second command timeout.** Migrations run at startup with no command timeout
set (`DiscordStartService.cs:43`, `SettingsServiceRegistration.cs:41`), and nothing in the repo overrides Npgsql's 30-second
default. `ModelRedesign` updates every `MessageHistory` row and rewrites `XpHistory` with its indexes through
`ALTER COLUMN "Type"`. On large tables a statement times out, the migration rolls back and the bot won't start. No data is
lost, but the deploy fails. Fix: call `context.Database.SetCommandTimeout(TimeSpan.FromMinutes(30))` before each
`MigrateAsync`, or set `Command Timeout` in the production connection string.

**P-4 (med): the id conversions are repeated 41 times or more.** `GrimoireDbContext` configurations write the same
`HasConversion(e => e.Value, value => new GuildId(value))` for every id property. Fix: one `ConfigureConventions` with a
converter per id type. Tested on a scratch copy: this removed 104 lines from the configurations, the solution built, and
EF reported no model change. That copy added about 20 lines, so the net saving is about 80. `Grimoire.Settings` has 24 more
of these, not tested. Removes code.

**P-5 (low): five indexes cost writes and serve no query.** Dropping them needs one new migration.

- `IX_XpHistory_GuildId_Xp`: its comment says leaderboards, but those read `leaderboard_view`, and every `XpHistory` query
  filters by user and guild. It's written on every XP gain, so this is the one on a hot path.
- `IX_Sin_Id_GuildId`: `Id` is already the primary key, so `GuildId` narrows nothing.
- `IX_Pardons_SinId_SetAt` and `IX_SinReasonHistory_SinId_SetAt`: same columns as the primary key. Every query filters one
  `SinId`, and Postgres can scan the key backwards for the newest row.
- `IX_CustomCommandUsages_GuildId_Name_UserId`: the primary key `(Name, GuildId, UserId, UsedAt)` serves the same filters.

**P-6 (low): the moderation actor converter is copied.** `SinConfiguration` and `SinReasonHistoryConfiguration` hold the same
converter, and the decimal fix had to be made twice. Fix: one shared `ValueConverter` class that calls static methods, which
can use a `switch` instead of the `as` and cast pair.

**P-7 (low): dropping the legacy tables has a silent trap.** Nothing in `Grimoire` reads the 18 obsolete `DbSet`s, which keep
454 lines of configuration alive. But startup migrates `GrimoireDbContext` first, and `Grimoire.Settings`' `Initial`
migration copies from those tables behind `IF EXISTS`. A drop migration shipped before `Initial` has run on production
would skip the settings copy with no error. Fix: run the settings migration first, and put the drop in a later release.

**P-8 (optional): pool the contexts.** Several handlers create a context for each message, and neither context holds state,
so `AddPooledDbContextFactory` is a one-word change in each registration. The gain hasn't been measured.

**Checked, no finding:** `MessageHistory` keys can't realistically collide, because each entry takes its own timestamp. The
`Reaction` conversion can't leave a null `ReactedAt`, because the old foreign key guarantees the message exists. Background
services that start before migrations catch their failures and don't touch the database at startup. Dropping `Trackers`
is deliberate, per `spec-migration-plan.md`.

**Run on production before deploying.** The first two size P-1, the third sizes P-2, and the last two abort a migration if
they aren't zero:

```sql
SELECT count(*) FROM "CustomCommandsRole" r JOIN "CustomCommands" c
    ON c."Name" = r."CustomCommandName" AND c."GuildId" = r."GuildId" WHERE NOT c."RestrictedUse";
SELECT count(*) FROM "CustomCommands" c WHERE c."RestrictedUse" AND NOT EXISTS (SELECT 1 FROM "CustomCommandsRole" r
    WHERE r."CustomCommandName" = c."Name" AND r."GuildId" = c."GuildId");
SELECT count(*) FROM "XpHistory" WHERE "Type" = 2 AND "AwarderId" IS NOT NULL;
SELECT count(*) FROM "XpHistory" WHERE "Type" = 1 AND "AwarderId" IS NULL;
SELECT count(*) FROM "Pardons" WHERE "ModeratorId" IS NULL;
```

### Section 3: Settings

Reviewed 2026-11-01 by reading the code and the `Initial` data migration against `main`. Nothing here was run against a
real database or Discord. "Unused" comes from searching `Grimoire` for each public `SettingsModule` method.

**S-1 (high): legacy level settings outside the new limits stop leveling for the whole guild.** `main`'s `/LevelSettings Set`
accepted anything from 1 to `int.MaxValue`. The branch limits `Base` to 500, `Modifier` to 200, `Amount` to 100 and the
timeout to 1-60 minutes, and `Initial` copies old values across unchanged. Its comment says the timeout "is constrained
1–60 minutes", which was never true on `main`. `FromDatabase` validates on read, so one such value makes
`GetLevelingSettings` fail on every call. `GainUserXp` then gives no XP and sends an alert per message, the failure isn't
cached so each message queries the database again, and `/Level` and `/UserInfo` lose their level data too. Fail-on-read was
chosen in `spec-settings-layer-fixes.md` Fix 8, but that spec assumed bad values were corruption, not valid legacy data. Fix:
decide before deploy. Recommended: `FromDatabase` trusts the stored value, which keeps each guild's XP curve, follows the
`XpHistory.cs` rule from D-1, and removes code (see S-2 and S-7). This reverses Fix 8. The alternative is to clamp in `Initial`,
which silently changes those guilds' level curves.

**S-2 (med): a database failure caches default level settings for a day.** `GetGuildSettings` catches every exception and
yields nothing. `GetLevelingSettingsCacheEntry` reads that as "no custom settings" and builds all four defaults, and
HybridCache keeps the entry for a day. After an outage that outlasts EF's retries, a guild silently uses the wrong XP amount,
timeout and level curve, so members can get the wrong level or reward role, until the day passes or someone changes a level
setting. Fix: delete the `try`/`catch` so the exception reaches `GetLevelingSettings`, which isn't cached and already returns a
failure. `GetGuildSettings` has one caller, so it can return a list instead of an `IAsyncEnumerable`. Removes code.

**S-3 (med): threads locked at deploy time become broken channel locks.** `main` keeps thread and channel locks in one `Locks`
table, and `Initial` copies every row to `ChannelLocks`. For a thread locked during the deploy:

- `IsThreadLocked` is false, so `LockedThreadEvents` stops deleting new messages and the lock isn't enforced.
- `/Unlock` looks in `ThreadLocks` and replies that it isn't locked.
- At expiry, `ProcessExpiredChannelLockAsync` calls `ChannelLockReverter` on the thread. Threads have no permission
  overwrites, so Discord should refuse. Not tested, but if so the row is never resolved and retries with backoff forever.

Only live locks are affected, so this is small and ends at expiry apart from any stuck rows. Fix: in the expiry task, skip the
revert when `channel.IsThread`, which is what `main` did. That's about two lines and clears the rows. SQL can't tell threads
from channels, so enforcement for those locks stays lost, and the check below sizes it.

**S-4 (med): eight public methods throw although they return `Result`.** `AddMute`, `RemoveMute`, `GetActiveMute`,
`IsMemberMuted`, `GetAllExpiredMutes`, `GetAllMutes`, `GetAllIgnoredItems` and `AppendIgnoredItemsEvent` have no error handling,
while the rest of the module does. Two effects are concrete. `MuteUser` catches only `DbUpdateException` around `AddMute`, so a
connection failure skips the code that takes the sin back off, leaving a sin with no mute. And the `.OrElse(...)` in
`IsMessageIgnored` and `IsMemberIgnored` can never run, because `GetAllIgnoredItems` always returns `Ok` or throws. Fix: wrap
them in `ExecuteSafelyAsync` and delete `MuteUser`'s `catch`. `GetGuildSetting`, `SetGuildSetting`, `GetLevelingSettings` and
`SetRewardAsync` can use it too in place of their own `try`/`catch`, which should make this net smaller. Once `.OrElse` works,
a failed lookup treats everyone as not ignored and awards them XP; that matches the code's intent, but check it's what you want.

**S-5 (low): the "latest event" filter is written out 13 times.** The `!dbContext.X.Any(y => ... y.SetAt > x.SetAt)` block
appears in every lock and mute query, with its ReSharper comment. Fix: one private query each for current channel locks, thread
locks and mutes. Saves about 40 lines.

**S-6 (low): the setting writers repeat each other.** `SetLogChannelSetting` and `SetUserCommandChannelSetting` are the same
method with a different setting type. `SetModuleState`, `SetMuteRole` and `DisableMuteRole` inline what `ApplyGuildSetting`
already does. Fix: one private `SetChannelSetting`, and use `ApplyGuildSetting` throughout. Saves about 30 lines.

**S-7 (low): the four level value types carry code nothing needs.** After S-1, `Create(string?)` has no production caller in
any of them. `Create(int?)` is only ever passed an `int`. Each has its own one-method `ToDatabaseString` extension class, one
of them named `LevelingSettingsExtensions` and hidden in `LevelScalingBase.cs`, and three are now just `ToString()`. Fix:
delete the string overloads, take `int`, and drop the extension classes. Saves about 70 lines plus the tests for the string
overloads.

**S-8 (low): seven indexes repeat the primary key.** Same reasoning as P-5: `ChannelLocks`, `ThreadLocks`, `Mutes`, `Rewards`,
`XpTrackedItems`, `MessageLogChannelOverrides` and `SpamFilterOverrides` each have an `IX_..._SetAt` on the key's own columns,
descending on the last one, and Postgres can scan the key backwards. Writes to these tables are rare, so the gain is small and
is mostly less code. `IX_GuildSettings_GuildId_Type_SetAt` has a different column order and can stay. Do it in the P-5 migration.

**S-9 (low): unused code.** `GuildSettingDefault` is never created by the bot, the tests or `Initial`, so its `Create` and its
`IsRedundantWrite` arm can go, along with the type if `has-pending-model-changes` stays clean. `IsMemberMuted`, `DisableMuteRole`
and `ResetAutoPardonDuration` are called only from tests.

**S-10 (low): the lock API uses a runtime switch where the compiler already knows the type.** All six callers of
`ApplyChannelLockAction` and `ApplyThreadLockAction` pass a concrete `ChannelLocked`, `ChannelUnlocked`, `ThreadLocked` or
`ThreadUnlocked`, and the method switches on it with an `UnreachableException` arm. Fix: make the private add and remove methods
public and delete the two dispatchers. Principle 10.

**S-11 (low): stale comments and dead branches.**

- "Stream rows as they arrive and filter in memory" appears twice above code that calls `ToListAsync`.
- `Initial`'s comment says `ModelRedesign` drops the source tables. It doesn't (see P-7).
- `GetGuildSetting`'s `NotFound` arm can't run, because the cache factory never returns null.
- `ToSettingType`'s "unrecognized" arm can't run.
- `GetGuildSetting` returns a `ValueTask`, and all six callers immediately call `.AsTask()`.

**S-12 (optional): 60 `.Value == 0` checks.** Every Settings factory checks each id for zero, and almost all of the ids come
straight from Discord. This is most of the length of `XpIgnoredItem.cs`, and it's why these factories return `Validation`
(see D-5). Removing them is a principle 2 trade-off, so it's your call. Separately, `RewardAdded`'s positional constructor is
public, so `Create` can be bypassed. Nothing does so today.

**S-13 (optional): the expiry polls grow with history.** On `main` finished locks and mutes were deleted, so the 5-second polls
saw only live rows. These tables are event logs now, so `EndTime <= now` matches every lock and mute ever made and the `EndTime`
index narrows nothing. Three such queries run every 5 seconds. The rows filtered out by the anti-join are cheap and the tables
are small, so leave this until it shows up. No simple lower bound is safe, because a row kept for later must still be found.

**Checked, no finding:** The cache is FusionCache in memory only, so the private `CachedSetting` records are never serialised.
Every write path clears or replaces its cache entry, and the bot is a single instance, so the one-day expiry is safe. `Initial`'s
sentinel `SetAt` can't collide on `Locks` or `Mutes`, because `main` keyed those by channel and by user and guild. Module flags,
`AntiSpamEnabled` and both override enums map correctly. Reason and reward message limits match `main`. `AutoPardon 0` means
"sins expire at once" on `main` but "use the default" here. That is recorded as expected in SIN-53, so it isn't reported.

**Run on production before deploying.** The first sizes S-1. The second is an upper bound for S-3, because a channel can also
have both values at zero:

```sql
SELECT count(*) FROM "GuildLevelSettings" WHERE "Base" NOT BETWEEN 1 AND 500 OR "Modifier" NOT BETWEEN 1 AND 200
    OR "Amount" NOT BETWEEN 1 AND 100 OR "TextTime" < INTERVAL '1 minute' OR "TextTime" > INTERVAL '60 minutes';
SELECT count(*) FROM "Locks" WHERE "PreviouslyAllowed" = 0 AND "PreviouslyDenied" = 0;
```

### Section 4: Shared infrastructure

Reviewed 2026-11-01 by reading the code against `main` and the DSharpPlus source in `../DSharpPlus`. "Unused" comes from
building a copy with the extension members marked `[Obsolete]`. The host behaviour in I-1 was tested in a small scratch
app, and the Polly exception types in I-3 were checked by reflection. Nothing here was run against real Discord, and the
Discord limits in I-6 come from its documentation.

**I-1 (high): one timed-out Discord request can stop the bot or its log queue.** `main` caught every exception in these
loops. The branch treats every `OperationCanceledException` as a shutdown, but `HttpClient` throws `TaskCanceledException`
when a request times out, and that type derives from it. DSharpPlus sets a 100-second timeout and its `RestClient` doesn't
wrap the exception. The effect depends on the loop:

- `GenericBackgroundService.cs:54,80`: the exception leaves `ExecuteAsync` and the default `StopHost` behaviour shuts the
  whole bot down. The scratch app reproduced this. Docker's `unless-stopped` restarts it, but any other host stays down.
- `GuildLogPublisher.cs:63`: `break` ends the read loop with no error. The queue fills to 1024, then every
  `SendLogMessageAsync` waits forever, including inside commands. This is KI-18's symptom coming back.
- `AlertService.cs:45`: the same `break`, so alerts stop with no error.

Fix: only treat it as a shutdown when the service's own token is cancelled, for example `catch (OperationCanceledException)
when (cancellationToken.IsCancellationRequested)`. `ProcessItemAsync` and `WarningFlushService.cs:33` need the same filter,
and `ProcessItemAsync` will need the token passed in. No net code.

**I-2 (high): one guild without Manage Server breaks invite tracking and join logs for every guild.** `main` checked
`ManageGuild` before `GetInvitesAsync` in both `UpdateAllGuilds` and `GuildAdded`, and the branch dropped both checks. So at
startup one such guild throws `UnauthorizedException` inside the loop:

- `UpdateAllInvites` never runs, so `InviteService` knows no guilds.
- `SaveChangesAsync` never runs, so the username, nickname and avatar backfill is lost for all guilds.
- `CalculateInviteUsed` and `UpdateInvite` then throw "Could not find guild" in every guild, so every join log with the User
  Log module on fails. Restarting doesn't help, because startup fails the same way.

`GuildAdded` loses the same backfill for the new guild, and later invite events in that guild throw. `GuildMemberAddedEvent`
also calls `GetInvitesAsync` unguarded, which `main` did too. Fix: one helper that returns an empty list without the
permission, used in all three places with the mapping from I-13. In `GuildMemberAddedEvent`, log the join with no invite.
Removes code.

**I-3 (med): a PluralKit failure loses the delete log.** `DeleteMessageEvent` calls `GetProxiedMessageInformation` before
anything else, unguarded. The service catches `TimeoutException`, 404 and 502 only. The resilience pipeline actually throws
`TimeoutRejectedException`, `RateLimiterRejectedException` and `BrokenCircuitException`, none of which derive from
`TimeoutException`, and retries that run out on a 429, 500 or 503 throw `HttpRequestException`. When PluralKit is configured,
the limiter allows 2 calls a second with no queue, and every message under 60 seconds old makes a call, so a third delete in
the same second loses its log. PluralKit proxying deletes each original, so busy servers will hit this. Also present on
`main`. Fix: catch
`ExecutionRejectedException` and every `HttpRequestException`, and return null, because the lookup is optional.

**I-4 (med): 12 setting commands report success after the save failed.** `SetGuildSetting` and `ExecuteSafelyAsync` turn a
database failure into `Result.Fail`, and these callers drop the result, then reply "Updated" and post to the mod log:
`ModuleCommands.Set`, both `GeneralSettingsCommands` setters, `SetMuteRole` (2), `SetAutoPardonDuration`, `PublicBanLog`,
`SetLevelSettings`, `SetChannelLogOverride`, `SetSpamFilterOverrideAsync`, and `Message.Set` and `User.Set` in
`LogSettingsCommands`. On `main` the failure threw and the command handler reported it. Fix: one `ctx.ReplyResultAsync` helper
that sends the error, treating `NotModified` as success so an unchanged value still replies normally. Principle 6. S-4 covers
the other five dropped results, which throw today.

**I-5 (med): `GetOrElse(() => null!)` turns a handled failure into a crash.** There are nine of these. `/UserInfo` uses it for
level settings and rewards and `/Modules View` for module state, so any settings failure becomes a
`NullReferenceException` and the whole command fails. That corrects S-1, which says `/UserInfo` only loses its level data.
`SendErrorAlert` also rates that exception as Warning, so it never reaches the urgent channel. Fix here: `Match` to
`SendErrorResponseAsync`, and for `/UserInfo` skip the level fields. The other six are for sections 5 and 7.

**I-6 (med): deleted messages with large or many images can lose the whole log.** `DiscordImageEmbedService` has two causes,
both also on `main`, and the publisher has no fallback when Discord refuses:

- Each image gets a full copy of the embed, including the message text, and Discord caps a message at 6000 characters across
  all embeds. Four images with a message of about 1500 characters exceed it. Fix: only the first embed of each group of four
  needs the text. The gallery only needs the matching `WithUrl`.
- An upload over the guild's limit gets 413 `RequestSizeException`, and nothing checks the size. Fix: read `Content-Length`
  in `GetImage` and treat too-large files as failed downloads, so the existing "Failed to download" field shows them.

**I-7 (low): log attachments can't survive a retry and aren't always disposed.** `GetStreamAsync` returns a non-seekable
stream, but `GuildLogPublisher` retries `SendMessageAsync` on a 5xx with the same builder, so a retry has nothing to upload.
The builder is also never disposed, so a message skipped by `continue` holds its connection until garbage collection. The body
is also read when the queue reaches it, not when the event fires. Fix: only retry messages that carry no files, and dispose
the builder in a `finally`.

**I-8 (low): a failed error reply loses the alert.** `CommandHandler.cs:140` replies before `SendErrorAlert`. `RespondAsync`
throws `InvalidOperationException` if the interaction was already replied to, and `EditResponseAsync` throws if the response
was deleted, so the alert never goes out. Fix: swap the two statements.

**I-9 (low): three `OrDefault` helpers hide outages.** `GetRoleOrDefaultAsync`, `guild.GetChannelOrDefaultAsync` and
`GetMessageOrDefaultAsync` catch `Exception`, so a 5xx reads as "doesn't exist". This is what KI-19 fixed for members. `/Mute`
then says the role is missing, and log cleanup treats the message as failed. Fix: catch `NotFoundException`, as the
`DiscordClient` helpers do. Their `ct` and `token` parameters are never used and can go too.

**I-10 (low): `InviteEvents` raises urgent alerts for harmless states.** Both `throw new Exception` lines fire when a deleted
invite was never tracked, and a missing View Audit Log permission escapes as `UnauthorizedException`. Each is an urgent alert
for something with nothing to do. Fix: ignore `DeleteInvite`'s result and catch the permission failure as `BanAddedEvent`
does. Removes code.

**I-11 (low): a log channel the bot can't post in fails silently.** `GuildLogPublisher` only logs the exception, so nobody in
the guild finds out, although `GuildIssueCatalog.CannotSendInChannel` exists and `TextCustomCommandProcessor` already uses it
(corrected in section 8). Fix: report it on `UnauthorizedException`. The daily throttle stops this looping when the broken channel is the mod log itself.

**I-12 (low): the daily report can exceed the embed limit.** `WarningReportFormatter` can reach about 4700 characters with 15
long entries, and DSharpPlus throws above 4096. The rows stay open, so it fails every minute and raises urgent alerts until
the rows change. Fix: stop adding entries when the next one wouldn't fit.

**I-13 (low): small simplifications.**

- The `Invite` mapping is written three times, in `GuildAdded`, `UpdateAllGuilds` and `InviteEvents`, and a fourth in
  `GuildMemberAddedEvent`. One `ToInvite()` extension replaces them. Discord documents `inviter` as optional, so handle null
  there.
- `ShouldPurgeMessageAfterInterval`'s 17 lines become one `is ... or ... or ...` and lose a runtime throw.
- `PurgeMessagesAsync`: the `switch` repeats what the chunk loop already handles, and `await foreach` over `ToAsyncEnumerable`
  should be `foreach`.
- `GeneralSettingsCommands.ModLogChannel` and `UserCommands` are nearly the same method.
- `RequireUserGuildPermissionsCheck` is 20 lines for one expression. `DiscordMember.Permissions` already returns everything
  for the owner and for Administrator, so both of its special cases can go.
- The `Off` log entry in `ModLogChannel` can never post: the setting is already off when the publisher reads it.

**I-14 (low): unused code.**

- `TimespanExtensions.cs`, the whole file (51 lines).
- `DiscordRegex`: only `GetUserMentions(string)` and `GetRoleMentions(string)` are called, so it drops from 118 lines to about 25.
- `PermissionsExtensions`: `GetPreviouslyAllowedPermissions` and `GetPreviouslyDeniedPermissions`.
- `DurationType.GetTimeSpan`, `client.GetUserAsync(UserId)`, and `MessageCreatedEventArgs.GetGuildId`.
- `CommandOutcome.Exception`: failed commands never log an outcome. Either log it or delete the value.
- `GrimoireServiceRegistration.cs:160`: the empty `if (ulong.TryParse(...)) { }` and its comment.

**I-15 (low): the PluralKit models are far bigger than their use.** Only `Id`, `OriginalId`, `PluralKitSystem.Id` and `Member.Id`
are read, out of 141 lines across four files. Every other `required` or typed field is a way for a PluralKit API change to fail
deserialisation, and that exception isn't caught either (see I-3). Fix: one small record. Separately, `PluralkitService` is scoped
and DSharpPlus makes a scope per event, so an unconfigured bot logs its warning on every delete. Make it a singleton.

**I-16 (low): small bugs in `DiscordImageEmbedService`.**

- `AddNonImageEmbed` compares extensions case-sensitively and `GetImageUrls` doesn't, so `.PNG` appears as both.
- `Path.GetFileName` on a non-image URL keeps the signed query string.
- `index` counts successful downloads but `AddAttachmentFileNames` indexes all images, so a failed download shifts the names.
- `Path.GetExtension` includes the dot, so files are named `attachment0..png`.

**I-17 (low): the embed splitter can throw and has an unreachable branch.** `AddMessageTextToFields` trims chunks, so a run of more
than 1024 whitespace characters can become an empty value, and `AddField` throws, which loses the log. That needs a Nitro message
over 2000 characters. After the trim, no chunk can exceed 1024, so the inner fallback can't run. Fix: drop blank chunks and the
unreachable block.

**I-18 (low): the docs are out of date.** `CLAUDE.md` says to copy `appsettings.example.json`, which doesn't exist, and names
`pluralkitEndpoint`, while the code reads `pluralkitApiEndpoint` and also needs `pluralkitUserAgent`. The comment on
`SlowCommandThresholdMs` cites Discord's 3-second limit, but the check measures the whole command, so any deferred command over 2
seconds is counted as slow.

**I-19 (optional): repeated patterns.** 141 fields only copy a primary constructor parameter, while newer classes use the parameter
directly. `ctx.Guild!` appears 53 times, while `GetRequiredGuild` exists to avoid exactly that and is used in 10 files. The
ephemeral defer branch appears 9 times and could be one `ctx.DeferPrivateAsync()`.

**Checked, no finding:** Unknown `!` text commands raise nothing, because `EnableCommandNotFoundException` is off by default.
`GuildLogType.Moderation` maps to `Module.General`, so disabling Moderation doesn't drop setup problem posts. `/Diagnostics Check`,
named in the setup problem footer, exists. `RetryBackoff` and `AlertCooldown` aren't thread-safe but each has one reader, or a lock
in `GuildIssueThrottle`. `GatewayHealth`'s nested lock is safe because `Lock` is reentrant. KI-39 and KI-48 aren't re-reported.

### Section 5: Moderation

Reviewed 2026-11-01 by reading the code against `main`, the settings layer and `../DSharpPlus`. Where a finding says
"reproduced", a scratch test ran it through the fake gateway and real database, in a copy of the repo. Nothing here was run
against real Discord.

**Correction to S-2:** `RetryLimitExceededException` derives directly from `Exception`, checked by reflection, so it reaches
`GetLevelingSettings`' general catch, not the "settings are invalid" one. S-2 has been corrected, and its fix no longer depends
on S-1.

**M-1 (high): `/Ban` and `/Kick` can't remove a bot account.** Reproduced for both: the moderator gets an error and no ban or
kick request is sent. DSharpPlus throws `ArgumentException` ("Bots cannot DM each other.") before it makes any request, and
`DirectMessageAsync` only catches `DiscordException`. Both commands send the DM first, so the action never happens. `main`'s
`/Ban` caught every exception, so this regressed with KI-16. The other callers fail later:

- `/Mute`, `/Unmute` and `/Warn` have already acted and replied, but the mod log entry is lost.
- `MuteBackgroundTasks.cs:104` has the same narrow catch, so a bot's mute expiry loses its log and raises a Warning alert.

Fix: return early from `DirectMessageAsync` when `member.IsBot`, and do the same in the expiry task. Two lines.

**M-2 (med): sin id options now send `min_value` on a string option.** On `main`, `sin_id` was an `int`. Now it's a `SinId`, and
`SinIdArgumentConverter` declares `String`, so `/Forget`, `/Pardon`, `/Reason` and the three `/Publish` commands register as
`"type":3` with `"min_value":0` (read from the registered payload). Discord documents `min_value` for integer and number options
only. What it does with this wasn't tested, and the fake accepts anything. If it refuses, the bulk registration fails, and that
takes every command with it. Moderators have also lost the number input. Fix: declare `Integer` in the converter, which is
one line and matches `main`, and raise the minimum to 1, which is what the converter enforces. The tests that pass
`SlashOption.String("sin_id", ...)` change with it. The converter's `ReadableName` also says "Command Name", and its file is
`CommandNameArgumentConverter.cs`.

**M-3 (med): `/Forget` loses its mod log for users with no username history.** Reproduced. These are the users from KI-33, which
fixed `/Pardon`, `/Reason` and `/Publish` but not `/Forget`. `result.UserName.ToString() ?? "Unknown"` can't reach `"Unknown"`,
because a null `Username?` gives `""`, and `AddField` throws on that. By then the sin is deleted and the reply reads
`**User:** ` with nothing after it. The moderator then gets an error, and the deletion is never logged, though that log is the
only trace of it. `main` fell back to the mention. Fix: the same fallback, ideally through the helper in M-11.

**M-4 (med): a channel moderator can lock or unlock any channel.** `/Lock` and `/Unlock` take a `channel` option, but permissions
are only checked where the command is run: Discord checks the member there, and `RequirePermissionsCheck` only tests the bot's
`AppPermissions` for slash commands. So someone who moderates one channel can lock `#general`, or a channel they can't see,
and can lift another moderator's lock. Also on `main`, but your main guild has channel moderators, so it's reachable there. Fix: refuse
unless `channel.PermissionsFor(ctx.Member!)` has Manage Messages. That adds about four lines to each command, or one shared check.
If channel moderators are meant to do this, drop this finding.

**M-5 (med): `/Mute`'s undo misses outages, leaving a mute nothing will lift.** `TryRecordMuteAsync` only catches
`DbUpdateException`, but `EnableRetryOnFailure` is on, so an outage arrives as `RetryLimitExceededException`, which isn't one.
That exception leaves before `RevokeRoleAsync`, so the role stays with no `Mutes` row, which is the case the comment at `MuteUser.cs:112`
warns about. S-4 only covers `AddMute`, not the first `SaveChangesAsync` or the one in the undo. Fix: one `try` around all of it,
catching `Exception`, with the role revoked whether or not removing the sin works. This replaces the two `try` blocks. `/Lock`
has the same gap: KI-06 saves after applying the overwrite, so a failed save leaves a locked channel that `/Unlock` says isn't
locked. Reverting there adds a little code, so treat that half as optional.

**M-6 (med): a spam mute can lose its log and DM.** In `SpamEvents`, `args.Message.DeleteAsync()` runs after the role is granted
and before the DM and mod log, with no guard. It throws if the bot lacks Manage Messages, or with a 404 if the message is already
gone, and PluralKit deletes each original, so proxied spam can race it. The member is muted, but neither they nor the mods are
told. Also on `main`. This same method has a private copy of the DM logic that KI-16 meant to replace. Fix: use
`DirectMessageAsync`, and delete the message last or catch `DiscordException`. Removes about 15 lines. KI-53 touches the same
method, so do them together.

**M-7 (low): blank reasons crash `/Reason` and half-finish `/Warn`.** Both reproduced with `"   "`. `/Reason` throws the
`UnreachableException`. `/Warn` saves the sin, then `AddField` throws, so there's a sin with no reason, no reply and no log.
`/Pardon` already refuses this. Text commands can send it, and it's unclear whether Discord's client can. Fix: the same check in
both, using the `Validation` that `ModerationReason.Create` returns. Removes one more of the throws from D-2.

**M-8 (low): a thread lock can't work without the bot's Manage Messages.** Thread locks are enforced by deleting messages, but the
`/Lock` gate only asks for Manage Channels and Manage Roles, and `/Diagnostics` doesn't check for it. The thread records as
locked, and every message then throws in `LockedThreadEvents`. Also on `main`. Not run. Fix: refuse in `ThreadLockAsync`.

**M-9 (low): a category or forum lock's expiry always raises an alert.** `SendLockExpiredLogAsync` ends with
`channel.SendMessageAsync`, which DSharpPlus refuses for those types with `ArgumentException`. Reproduced in part: the unlock is
recorded and nothing is posted to the channel. The lock is lifted and the mod log posted, so the only cost is a Warning alert each
time. Also on `main`. Fix: skip the channel message for those types.

**M-10 (low): small behaviour bugs.**

- `BanAddedEvent`: when the audit log has no matching entry, the reason is dropped, although `GetBanAsync` could supply it. Falling
  through to the existing fallback removes a branch.
- `/Mute` and `/Lock` accept any positive `int`, so a huge number of days overflows `AddDays`, with a generic error and an alert.
  Nothing has changed by then. Not run. Fix: a maximum on the attribute. KI-35 is the same problem for `/ModSettings AutoPardon`.
- `/SinLog` has no `OrderBy`, so sins come back in whatever order Postgres picks. Also on `main`.
- `/SpamFilter Override` checks `guild.Channels`, which holds no threads, so it refuses a thread while its reply promises to cover
  "sub channels/threads". `SpamTrackerModule` would honour one. Also on `main`.
- `/ModSettings AutoPardon`'s description says "minutes hours or days", but the choices are Days, Months and Years.
- `$"Unmuted by {ctx.User.Mention}"` puts a raw `<@id>` in the audit log. Also on `main`.

**M-11 (low): duplication that can go.**

- `/Publish Ban` and `/Publish Kick` are the same 83 lines. One private method taking `SinType` and `PublishType` saves about 70.
  `SendPublicLogMessage` also builds its embed twice.
- "Find this guild's sin and the user's latest name" is written six times, across `/Publish`, `/Forget`, `/Pardon` and `/Reason`.
  One helper returning the user and a display name saves about 50 lines, and it's why M-3 could happen.
- `OverwriteChannelAsync` and `OverwriteVoiceChannelAsync` differ only in the permission set. Saves about 15.

**M-12 (low): dead or roundabout code.**

- The `member.Guild` checks in `/Mute` and `/Unmute` can't fail, because `DiscordMemberConverter` only resolves from this guild.
- `UnlockChannel` wraps a value in `Validation.Succeed(...).ToResult()` just to enter a pipeline.
- `LockBackgroundTasks.cs:69`: the `everyoneRole is null` check can't be true for a guild channel, and returning there would keep
  the lock forever.
- `LockChannel` reads `guild.Channels[channel.Id]` when it already has `channel`, and rebuilds `ForReason` by hand.
- `SpamFilterOverrideCommands` switches on the same enum twice, each with a `NotImplementedException` arm. One switch returning
  both values does it.
- `TimeSpan.FromDays(10950)` is copied in `SinLog`, `ModSettings.View` and `UserInfoCommands`, as a fallback for the Settings
  default.

**Checked, no finding:** Locking twice keeps the original permissions, because `AddChannelLock` reuses the active lock's values, and
a test pins it. `BanHandoff` can't record a ban twice, and a late event within 2 minutes still attributes correctly. The expiry
task's `is Result<RoleId?>.Invalid` check catches every read failure, because `Result.Fail` builds `Invalid`. `EffectivePermissions`
follows Discord's order, and the diagnostics make no API calls. The `Dispatch threw` error seen on forum locks comes from the
fake gateway's forum payload, which has no tag list. Spam recording a mute before granting the role is already in KI-53.

**Check against real Discord.** `/Ban`, `/Kick` and `/Mute` accept a 1000-character reason and send it as the audit log header, which
Discord documents as 512 at most. Whether it truncates or refuses wasn't tested. `main` allowed the same length. Banning someone
already banned may produce no gateway event, in which case `/Ban` waits 5 seconds and says "Not recorded yet".

### Section 6: Logging

Reviewed 2026-11-01 by reading the code against `main`, the settings layer and `../DSharpPlus`. Where a finding says
"reproduced", a scratch test ran it through the fake gateway and real database, in a copy of the repo. Nothing here was run
against real Discord. The branch isn't deployed, so none of these need a data repair.

**L-1 (high): deleting a message with an image loses the delete log.** Reproduced with `cat.png`: no log is posted, and the
delete is still recorded. `main` stored the attachment's `Url` in `Attachment.FileName`. `AddMessageEvent.cs:52` now stores
`x.FileName`, so `DeleteMessageEvent` passes `cat.png` to `BuildImageEmbedAsync`, and `new Uri("cat.png")` in `GetImageUrls`
throws. Non-image files don't reach that line, so they log, but as a bare name. The deleted image can no longer be re-uploaded
either, which is the point of that service, and the bulk dump shows names instead of links. The fake gateway always sends an empty
attachment list, so no test covers this. Fix: store `x.Url` again and drop the `Where` on the file name, as `main` did. Add an
attachment to one test. The column holds URLs, so `AttachmentFileName` is misnamed; rename it with D-6.

**L-2 (high): the bot's old log posts are logged as deleted, and that repeats forever.** Reproduced with a seeded row: a post by
the bot that isn't in the message cache produces a "Message deleted" log, and that log gets its own `OldLogMessages` row. The
whole 30-day cycle wasn't run. `AddMessageEvent` saves the bot's messages like anyone else's unless the channel is set to Never
Log. Cleanup deletes log posts at 30 days and message rows at 31, so the row still exists when the delete event arrives. The
message is far too old to be cached, so `args.Message.Author` is null and the check at the top passes. `main` had a second check
on the stored `UserId` after the query, and the branch dropped it. So every purged post creates an empty post, which is purged
30 days later and creates another. Fix: restore that check, or skip the bot's own messages in `AddMessageEvent`, which also saves
the rows. The second is simpler, since nothing reads them.

**L-3 (med): a deleted message that isn't cached has no author.** Reproduced: the log has only Channel, Message Id and Content.
Any message from before a restart or past the cache size is affected, so that's most older deletes. `main` built the mention from
the stored `UserId`; the branch reads `args.Message.Author`, which is null there. "Original Author" has a similar gap in both
`DeleteMessageEvent` and `UpdateMessageEvent`: it's skipped if a REST lookup fails, although a mention needs only the id. Fix:
build both mentions from the stored ids. That removes a branch and a REST call in each.

**L-4 (med): guild avatar changes aren't logged any more.** Reproduced. `main` compared `GetGuildAvatarUrl`, which falls back to the
global avatar. The branch compares `AvatarUrl`, and DSharpPlus builds that from the global hash only. `Avatars` is per guild, so
this is the case the table exists for. Fix: use `DisplayAvatarUrl` in `GetAvatarFileName` and in `UpdatedAvatarEvent`, keeping KI-49's
rule that one form is stored everywhere. `MemberAdded` and the backfill both go through `GetAvatarFileName`, so they follow.

**L-5 (med): a long edit's second embed exceeds Discord's limit.** Reproduced: two 3,000-character messages give one embed of 3,631
characters and a second of 7,174, which has both Before and After. Discord documents a 6,000-character limit, so the After text
should be lost, though the refusal itself wasn't tested. `main` copied the embed before adding Before. The branch's collection
expression copies it after. Needs Nitro, because the sum must reach 5,000. Fix: take the copy first, as `main` did.

**L-6 (med): a new member's first update logs a false username change.** Reproduced: a member with no earlier history joins and gets
a member update within 2 seconds, which an auto-role bot would cause. The result is "Username Updated" from `Unknown`, and a second
history row. `MemberAdded`'s row is hidden by the `Timestamp < now - 2s` filter, and the default struct from D-7 reads as a change.
`main`'s username query probably does the same, because its `First` becomes a subquery that returns null, but that wasn't run.
`UpdatedAvatarEvent` has the same default-struct path without the filter, so it fires if the event overtakes `MemberAdded`; there,
`main` returned on no row, so that half is a regression. Fix: cast to the nullable type in both `Select`s and return on null. That
also removes both `Unknown` branches, and this closes D-7.

**L-7 (med): log content is built even when that log is off.** Reproduced for avatars: with the log channel off, the image is still
downloaded, and the stream is never disposed (see I-7). `main` built the message only after finding a channel. Here `GuildLog` takes
a finished message and the publisher drops it later. So with the module on and a single log turned off, each delete still looks up the
avatar and downloads every image, and each bulk delete still makes its member lookups. Fix: check `GetLogChannelSetting` before
building, in `DeleteMessageEvent`, `BulkMessageDeletedEvent` and `UpdatedAvatarEvent`. Keep saving the history row.

**L-8 (low): `DeleteMessageEvent` calls out before it knows the message was logged.** The PluralKit lookup and the audit log parser
both run before the message query, so a delete in a Never Log channel, or of a message past the 31-day purge, still costs them. The
parser also runs its own query for the same row. Fix: query first and return on null, then pass `UserId` to the parser. A proxy link
needs the row anyway, because of its foreign key. Removes a query.

**L-9 (low): the bulk delete dump regressed.** `main` printed the username. The branch prints `author?.Mention`, which is a raw `<@id>`
in a text file, next to the same id. It also makes a member lookup per message, up to 100 with one author, and a 5xx there loses the dump
after the history is saved. There's no `OrderBy`, so messages are in no particular order. Fix: take the latest name from
`UsernameHistory` in the query, which removes the REST calls and the `async`, and add `OrderBy(m => m.Id)`. The stream can then be
`new MemoryStream(Encoding.UTF8.GetBytes(...))`, which removes about five lines.

**L-10 (low): the history trim runs over every member every minute.** `DeleteOldMessageAndUserLogs` does `Distinct` and a lateral query
across `Avatars`, `NicknameHistory` and `UsernameHistory` on each 1-minute tick, though almost nothing changes between ticks. Also on
`main`. Not measured. Fix: run that part once a day, using a timestamp field on the service. About three lines.

**L-11 (low): small behaviour bugs.**

- `/Log Message Override`'s mod log puts `ctx.User.Mention` in `Title`, which `GuildLogMessage` sends as the embed author, and Discord
  doesn't render mentions there. `main` used the username.
- `UpdateMessageEvent` drops the log when the avatar lookup returns null, and uses the webhook's avatar for a proxied message.
  `DeleteMessageEvent` treats the avatar as optional and uses the original author. Also on `main`. Fix: the same as delete.
- `AddMessageEvent`'s `catch (Exception)` swallows the failure, where `main` rethrew, so a failed save never raises an alert. A
  duplicate message also logs at Error.
- `GuildMemberRemovedEvent`: about 45 roles exceed the 1,024-character field limit, `AddField` throws, and the leave log is lost. Also
  on `main`.
- `InviteService` throws "Was not able to delete invite." when an `InviteDelete` event removed the invite first, which loses the join
  log. Fix: ignore the result, since the invite is gone either way.
- `/Log Message Set` and `/Log User Set` only check Send Messages, though these logs need Embed Links and Attach Files, and the option
  accepts voice channels and categories. Also on `main`. A wrong choice then fails silently (see I-11).

**L-12 (low): duplication that can go.**

- `/Log Message View` and `/Log User View` repeat the same lookup eight times. One helper taking a `GuildLogType` saves about 40 lines.
- `/Log Message Set` and `/Log User Set` share their whole body, apart from the enum mapping and one colour. One private method saves
  about 45 lines, and fixes I-4 for both at once.
- `/Log Message Override` switches on the same enum twice, as `SpamFilterOverrideCommands` does in M-12.
- The System Id and Member Id fields are written twice, and the "latest content" subquery three times. It can use `m.MessageHistory`,
  which also drops the captured `dbContext` and its ReSharper comments.
- `InviteService`: `UpdateAllInvites` repeats `UpdateGuildInvites`, and both `AddOrUpdate` calls can be an indexer.

**L-13 (low): dead code.**

- **Module Enabled** in both `View` commands can only say True, because `[RequireModuleEnabled]` refuses the command otherwise.
- `ViewOverrides`' `Inherit/Default` arm can't run, because `GetAllOverriddenChannels` filters `Inherit` out.
- `GetAvatarFileName(MediaFormat, ushort)` has no callers.
- `GroupBy(x => new { x.ChannelId })` in the cleanup wraps one key in an anonymous type.

**Checked, no finding:** `ReactionEvents` holds up: the guild comes from the right place on each event, and a reaction on an unsaved
message is skipped. Message purges can't block on references, because `ReferencedMessageId` has no foreign key, and `Attachment`,
`MessageHistory`, `Reaction` and both proxy links cascade. `DeleteMessageEvent`'s proxy fields are nullable through the implicit
conversion, so KI-38 doesn't recur there. The startup backfill, `MemberAdded` and `UpdatedAvatarEvent` all store the same URL form, so
restarts cause no false avatar logs. The cleanup's retry and its `GroupBy` translation are covered by `LogCleanupTests`. KI-39 isn't
re-reported, nor are the `?size=128` rows behind KI-49's one last false log.

**Check against real Discord.** Whether a signed attachment URL still downloads once the message is deleted decides how much L-1's
fix recovers. `main` relied on it. Whether the event order for a used-up single-use invite reaches KI-50's fallback at all also
wasn't tested.

### Section 7: Leveling

Reviewed 2026-11-01 by reading the code against `main`, the settings layer and `../DSharpPlus`. Where a finding says
"reproduced", a scratch test ran it through the fake gateway and real database, in a copy of the repo. The fake was changed there
to report managed roles, which it can't do today. The level curve was compared to a plain loop for 63 `Base` and `Modifier` pairs
up to 2,000,000 XP. Nothing here was run against real Discord.

**X-1 (high): a member with a managed role can never earn a reward.** Reproduced: the role isn't granted, no request is sent, and
the handler throws `InvalidOperationException: Cannot assign managed roles`. `ApplyRewards` passes `member.Roles` to
`ReplaceRolesAsync`, and DSharpPlus refuses any managed role in that list, before it adds the member's managed roles back itself.
Roles from integrations such as Twitch or Patreon are managed, and so should be the Server Booster role. The XP is saved first, so
this repeats on every XP gain for as long as the member qualifies. `UnauthorizedException` is the only one caught. Also on `main`.
The same call rewrites the whole role list from the cache, so a change the cache hasn't seen yet is undone, and one role the bot
can't grant blocks the others. Fix: `GrantRoleAsync` per new role, as the mute code does. That replaces the `Join`, `Concat` and
`Distinct` and fixes all three.

**X-2 (high): rewards for a deleted role never go away, and "earned" repeats forever.** Reproduced: with a reward's role deleted,
three XP gains posted three "has earned" logs and sent three no-op member updates, and the reward was still stored. `main` removed
these through `RoleDeleted` and a cascade. The branch dropped both, so the reward stays. `newRewards` is never filtered to roles that
exist, so every active member at that level triggers this on every XP gain. The issue post tells admins to "remove those rewards
with the reward commands", but `/Rewards Remove` takes a `DiscordRole`, so a deleted role can't be chosen. Nothing in Discord can
clear it. `/Rewards View` shows `Level:2 Role: ` with no id. Fix: in `ApplyRewards`, which already finds these roles, append a
`RewardRemoved` with the bot as `SetBy`, and drop them from `newRewards`. That also covers roles deleted while the bot was offline,
which a `GuildRoleDeleted` handler wouldn't. `RewardRolesMissing` and its test go, or it stays as a one-off notice with new wording.

**X-3 (med): `/Ignore View` fails once about 175 items are ignored.** Reproduced with 200 members: `Description length cannot exceed
4096 characters`. `main` wrote one item per line, and the paginator splits by line. The branch joins each type with spaces, so the
whole list is about ten lines and fits on one page, or doesn't. Fix: join with `'\n'`, as `main` did.

**X-4 (med): a backslash in a reward message loses the DM.** Reproduced with `\o/`: the role is granted and no DM is sent, and
`C:\dir` fails the same way. `Regex.Unescape` throws on any escape it doesn't know. It also strips Markdown escapes, so `\*bold\*`
becomes `*bold*`, and the description says Discord Markdown applies. `/Rewards Add` accepts all of these, and the failure now logs at
Information as if the member's DMs were closed. `main` logged a Warning; otherwise it's the same. Slash options can't contain
newlines, so `\n` looks like the only reason for the call. Fix: `.Replace("\\n", "\n")`, which removes the `Regex` import.

**X-5 (low): the rest of I-5.** Five of the `GetOrElse(() => null!)` sites are here. `GetLevel.cs:65`,
`RewardCommandGroup.GetRewards.cs:27` and `LevelSettingsCommandGroup.GetLevelSettings.cs:31` turn a settings failure into a
`NullReferenceException` after the defer, so a handled failure shows a generic error. `GainUserXp.cs:134` does the same after the
XP is saved, so the reward waits for the next gain, and that's harmless once the crash is gone. `IgnoreCommandGroup.GetIgnoredItems.cs:48`
can't fail until S-4 is fixed, and then it would crash too. Fix: `Match` to an error reply in the commands, and return early in
`ApplyRewards`. The sixth site, `UnmuteUser.cs:43`, is safe, because it reads the value with `?.`, but `null!` there hides that.

**X-6 (low): `/Rewards` changes no longer reach the mod log.** `main` sent Add and Remove to `ModChannelLog`. The branch uses
`GuildLogType.Leveling`, while `/Ignore`, `/LevelSettings`, `/Award` and `/Reclaim` all use `Moderation`. If the level log is off, a
reward change leaves no record, and if it's a public channel, members see it. The tests can't tell, because they point the leveling
log at `#modlog`. `/Rewards Add`'s entry also doesn't say who did it. Fix: use `Moderation` in both, a one-word change each, unless
this was deliberate.

**X-7 (low): `/Rewards Remove` reports success for a role that isn't a reward.** Reproduced: the reply and the log both say it was
removed, and a `RewardRemoved` row is added. `main` refused with "Did not find a saved reward". Fix: check `GetLevelingRewardsAsync`
first, about three lines.

**X-8 (low): `/Leaderboard` shows fewer rows near the bottom.** Reproduced: last place of 21 shows 6 rows. `main` shifted the window
so 15 always showed. The query is fixed at rank -5 to +9. Fix: read the count first, then start at
`Math.Max(1, Math.Min(rank - 5, total - 14))`.

**X-9 (low): the level shortcut overestimates for small `Base` and `Modifier`.** Reproduced by brute force. Once XP passes 1000 the
estimate can start past the right answer: 24 levels too high at Base 1 and Modifier 1, 2 at Base 10 and Modifier 1, and 1 at Base 15
and Modifier 1. `/Level`'s Progress then goes negative. Only 8 of the 63 pairs were wrong, all with `Base × Modifier` of 15 or less.
The defaults were exact everywhere, and in no pair did a level drop as XP rose. Also on `main`. Fix: delete the shortcut, which removes
five lines and the comment. The loop then runs about 500 times at the defaults for 2,000,000 XP, and about 14,000 at the extreme.
Those guilds' levels would move down slightly, which is why this is low.

**X-10 (low): `/Rewards View` and `/Ignore View` have three gaps.**

- Reproduced: two 3,000-character messages exceed the 4,096 limit, and the command fails. Each message can be 4,096 alone. Also on
  `main`. Fix: cut each message to about 200 characters in this list.
- `main` said "This guild does not have any rewards" and "does not have any ignored channels, roles or users". Reproduced: the branch
  sends a blank embed and three empty headings.
- `GetRoleOrDefaultAsync` is the reason the method is async. `RoleExtensions.Mention(x.RoleId)` needs no lookup, removes the `WhenAll`,
  and shows a deleted role's id.

**X-11 (low): `/Rewards Add` accepts roles that can't be granted.** `Hierarchy < role.Position` passes a role level with the bot's top
role, and Discord needs it strictly higher. Nothing checks `role.IsManaged`, so a managed role as a reward fails on every gain. Also on
`main`. Not run. Fix: `<=`, and refuse `IsManaged`.

**X-12 (optional): small behaviour differences.**

- Ignoring a channel doesn't cover its threads, because `IsMessageIgnored` only sees the thread's id. Also on `main`. Fixing it adds a
  parameter, so it depends on whether you want it.
- `/Ignore Add` on an ignored item, or `/Ignore Remove` on one that isn't, adds a row and says it worked. `main` reported only real
  changes. Harmless, and filtering adds code.

**X-13 (low): duplication and dead code.**

- `/Ignore Add` and `/Ignore Remove` are the same 55 lines with different types, and both build items with object initialisers,
  bypassing the `Create` factories that exist. One private helper saves about 35 lines. `BuildIgnoreListAsync` isn't async, and its `_`
  arm can't run for these types. The parameter is `items` on Add and `item` on Remove.
- `GetTopLeaderboardAsync` and `GetUserCenteredLeaderboardAsync` differ only in the rank range, and format lines and count rows twice.
  One method taking the first rank saves about 25 lines and makes X-8 simple. Top can't fail, so its `Result` is the case in D-5. The
  file also mixes `LeaderboardView` and `Set<LeaderboardView>()`.
- `ApplyRewards` looks up the guild and member it already has. `member` and `member.Guild` come from the event, and passing them
  removes four lines and two exits.
- `/Reclaim`: `SumAsync` on `long?` and the two "no xp" checks reduce to one `xpToTake <= 0` check. Saves about eight lines. The
  command also opens two contexts.
- `LogSetAsync` is another copy of the flow in L-12, and should share that helper, which fixes its half of I-4.
- `GetLevel`'s and `/LevelSettings View`'s guild checks can't fail under `[RequireGuild]`. **Module Enabled** in `View` can only say
  True, as in L-13.
- `AsNoTracking()` does nothing on the four `Sum` and `Max` queries.

**Checked, no finding:** The cooldown runs before the `Sum`, and the primary key serves it, so a message in cooldown costs one indexed
query. That's cheaper than `main`. `/Award` and `/Reclaim` set `TimeOut` to now, so they can't extend or shorten a cooldown. XP keys
can't realistically collide. Two messages landing together can both earn XP, as on `main`, but they must arrive within milliseconds
and cost one gain. `XpHistory` has no foreign key, so XP for someone with no `Members` row works, where `main` silently gave none.
`GetXpNeededForLevel` matches `GetLevelFromXp` wherever the shortcut doesn't overshoot. Setting an unchanged value replies "already
set", which is correct. The view has the unique index `CONCURRENTLY` needs, and its rank is per guild. `/Leaderboard` lagging by up to
15 minutes is deliberate, and a test pins it. S-1 and S-2 aren't re-reported, and P-2 covers `/Reclaim` losing its moderator.

**For the roll-up.** `main`'s `ChannelDeleted`, `ChannelAdded` and `RoleAdded` were dropped along with `RoleDeleted`. X-2 shows what
that cost for rewards, so section 9 should check what depended on the others, starting with log channels pointing at a deleted channel.

**Check against real Discord.** Whether the Server Booster role reports `managed: true`, which decides how common X-1 is. Roles from
integrations are managed either way.

**Run on production before deploying.** The refresh uses the 30-second default from P-3, and `CONCURRENTLY` takes longer than the
query alone. If this takes more than a few seconds, the leaderboard could stop updating, with a failure every 15 minutes:

```sql
EXPLAIN ANALYZE SELECT "GuildId", "UserId", SUM("Xp"),
    ROW_NUMBER() OVER (PARTITION BY "GuildId" ORDER BY SUM("Xp") DESC)
FROM "XpHistory" GROUP BY "GuildId", "UserId";
```

### Section 8: Custom commands

Reviewed 2026-11-01 by reading the code against `main`, the role policy spec and `../DSharpPlus`. Where a finding says
"reproduced", a scratch test ran it through the fake gateway and real database, in a copy of the repo. Nothing here was run
against real Discord, and the fake accepts empty messages that Discord may refuse.

**Correction to I-11:** `CannotSendInChannel` does have a caller, `TextCustomCommandProcessor`, and a test pins it. I-11 has been
corrected. Its fix still stands, because `GuildLogPublisher` doesn't report the problem.

**C-1 (med): any member can raise an urgent alert with a command that renders empty.** Reproduced: with content `%Message`, a bare
`!say` throws `ArgumentException` from `SendMessageAsync`. That reaches `HandleEventHandlerError`, which sends an urgent alert, so
this repeats on every use, and the use is still counted. `/Command` sends `content: ""` or an embed with an empty description in the
same case. Discord should refuse both, which would be an error reply and another alert, but that wasn't tested. Also on `main`. Fix:
when the rendered text is blank, return from the text path and delete the response in `/Command`, as an unauthorized use already
does. About four lines.

**C-2 (med): roles given without `restricted_use` are dropped, and the reply says it worked.** Reproduced: `/Commands Learn` with
`permission_role_1` and no `restricted_use` replies "Added open custom command." and stores no roles, so everyone can use it. The
role policy spec defines deny-only commands, and `main`'s commented-out option says roles deny unless the command is restricted, but
`BuildRoles` only ever creates `CustomCommandAllowRole`. Nothing in the bot can create a deny role, so after P-1's fix a legacy deny
list survives until the next `Learn`, which silently removes it. Fix: map these roles to `CustomCommandDenyRole`, which is one
`switch` arm and completes the spec. Refusing them is the alternative, but then deny lists stay impossible. This is your call.

**C-3 (low): `%Mention` shows a mention but never pings, so the sanitiser protects nothing.** Reproduced: both `!hug` and `/Command`
send `"allowed_mentions":{"parse":[]}`, which Discord documents as suppressing every ping. The library's default has been this way
since 2021, so `main`'s text path behaves the same. `main`'s slash path wasn't checked. So `SanitizeUserMessageMentions`, its two
regexes and the `@everyone` arm in `ApplyMention` change the text without preventing anything. Its `\b` also misses `@everyonex`,
which doesn't matter for the same reason. Fix: if pings are wanted, pass `WithAllowedMentions` with only the resolved user or role,
and delete the sanitiser, about 20 lines. Then `%Message` can't ping anyone either way. If they aren't wanted, delete it anyway.
`SlashCommand_FillsMentionAndMessage_AndDefusesEveryone` changes with it.

**C-4 (low): `/Commands Learn` lost its specific errors.** Reproduced: a name with a space or over 24 characters replies "Failed to
parse name.", and a bad colour "Failed to parse embed_color." `main` said what was wrong. The converters discard the messages that
`Create` already has. Fix: in `Learn`, take `name` and `embed_color` as `string` and put `Create` in the pipeline. That shows those
messages, lets `[MinMaxLength(1, 24)]` come back so Discord's client enforces the length, and deletes
`CommandEmbedColorArgumentConverter`, because `Learn` is its only user. Other commands keep the name converter.

**C-5 (low): a backslash in command content can change it.** Reproduced: `C:\new\temp` is stored with a real newline and tab. Slash
options can't carry newlines, so `\n` has a purpose, but `\t` doesn't obviously. `main` stored the text as typed, so a legacy row is
also changed on every read until D-1 is fixed. Fix: drop the `\t` replacement. Keeping `\n` is the same trade-off as X-4.

**C-6 (low): the database `catch` blocks miss the failures they exist for.** `Forget` and `Stats` catch `DbException` and `Learn`
catches `DbUpdateException`, but with `EnableRetryOnFailure` an outage arrives as `RetryLimitExceededException`, as in M-5. That falls
through to `CommandHandler`, which already replies and alerts, so the catches only run for rare non-transient errors. Not run. Fix:
delete the three catches and `ReportDatabaseFailure`, which also removes the `alerts` and `logger` constructor parameters. About 50
lines. The three methods can then return plain values. This trades the friendly wording for less code, and an outage already shows
the generic error today.

**C-7 (low): usage is recorded before the reply.** Reproduced: the failed send in C-1 still counted a use, and so does a send the bot
isn't allowed to make. Not run: a failed usage insert loses the reply, so optional statistics can stop the command itself. `main`
recorded no usage, so this is new. Fix: swap the two `TapAsync` lines, and the same two statements in `CallCommand`.

**C-8 (low): small behaviour bugs.**

- `/Commands Leaderboard`'s footers count only the top 15 rows: "15 commands" with 40 commands, and "total uses" leaves out everyone
  past 15th. Fix: drop the footers or count separately. Dropping them removes `LeaderboardDisplay.Footer`, which is never null anyway.
- `/Commands Revert` numbers versions from the newest 25, while `/Commands History` numbers from all of them, so at 31 versions the
  same version is v25 in one and v31 in the other. Not run. Fix: read the total count for the numbering, about two lines.
- Reproduced: with both `%Mention` and `%Message`, `!hug hello world` loses `hello`, because the first word is always skipped. Also
  on `main`. Fix: skip it only when `ResolveSnowflake` found something.
- Reproduced: autocomplete throws `PostgresException` when the first word typed exceeds 255 characters, the `levenshtein` limit. Also
  on `main`. Fix: cut `cleanedText` to 24 characters, which is the longest possible name.
- `/Commands Forget` on an unknown name posts a mod log entry for nothing. `main` refused. Fix: reply, but skip the log, which removes
  a branch.
- `embed_color` is ignored without `embed`, and its description still names `OutputType`, an option that doesn't exist.
- `/Commands Stats` on an unknown name shows zeros, where `History` says it doesn't exist. Also, `Leaderboard` uses the same wording
  for a name that was never created and one that was never used.

**C-9 (low): duplication and dead code.**

- `/Command` and `BuildResponse` both pick the output format, truncate, and apply the mention and message.
  `CommandOutputFormat` is built from the command's type just to be read straight back, so it can go, along with one of
  `BuildWebhookResponse` and `BuildMessageResponse` if a single render step feeds both. Saves about 30 lines. Its file is also the
  wrong place for it, a query helper.
- `SaveRevertedCommandAsync` is 38 lines, including a type switch with an unreachable arm and a `Validation` that can't fail. A
  `CustomCommand.AsNewVersion(moderatorId, now)` using `with`, as D-8 describes, replaces it. Set each copied role's `CustomCommand` to
  null. Saves about 30 lines, and `CreatedAt = default` on roles goes too if the factories take role ids.
- `IsUserAuthorized`'s last ten lines reduce to one expression: `(allow.Count == 0 || memberRoles.Overlaps(allow)) &&
  !memberRoles.Overlaps(deny)`. It matches every row of the spec's table. Update the spec's copy.
- The `GetRequiredGuild` block is repeated seven times here, under `[RequireGuild]`. See M-12, X-13 and I-19. Section 9 should pick
  one answer for all of them.
- `FetchUsageStatsAsync` and `FetchTopUsersAsync` map an anonymous type to an identical record. Select the record directly. `GetVersionsAsync`
  in `GetCommandVersionOptions` returns a `Result` that can't fail, as in D-5.
- `AsNoTracking()` is applied by `GetCustomCommandQuery` and again by both callers. The "latest version" filter is also written twice,
  and `GetChoicesAsync`'s comments describe a raw-string projection that the code no longer does.

**Checked, no finding:** The eight findings in the older FP review are all fixed, including the inverted `IsUserAuthorized`, and that
review is closed. Revert's ticks round-trip, because they come from the stored value, and a test pins it. A deleted allow role locks a
command to nobody, and `main`'s `RestrictedUse` with no roles did the same. Forget leaves no orphans: role rows cascade and usage is
deleted, and the two deletes not sharing a transaction only risks lost statistics. `%Message` text can't inject a mention placeholder,
because the mention is replaced first. `!` messages cost one cached module check and one indexed query. `Learn` saving a new version
instead of editing is deliberate. The autocomplete query translates, shows only the latest version and is covered by
`AutocompleteQueryTests`. D-1, P-1, P-5 and D-5 aren't re-reported.

**Check against real Discord.** Whether an empty `content` or empty embed description is refused, which decides `/Command`'s half of
C-1, and that `parse: []` suppresses pings on an interaction response as documented, for C-3.

**For the roll-up.** The `DatabaseError` alert type is only used by `CustomCommandSettings`, so C-6 removes it entirely.

### Section 9: Roll-up

Reviewed 2026-11-01. The eight sections hold 96 findings, 8 of them high. The event and command lists were compared
mechanically against `main`, and the code behind the high findings and top-ranked fixes was rechecked in the current working
tree. None of them has been fixed. Nothing here was run against real Discord or production.

#### Owner decisions

These change what the fix is, so they come first. The first three must be settled before deploy, because they decide what the
migrations do to production data. Editing `ModelRedesign` is fine for these, since it hasn't been deployed.

| Finding | Decision | Recommendation |
|---------|----------|----------------|
| S-1 | Keep legacy level settings outside the new limits, or clamp them? | Keep them: `FromDatabase` trusts stored values |
| P-1, C-2 | Are deny lists supported? | Yes: the `CASE` in the migration, plus the deny arm in `BuildRoles` |
| P-2 | Keep who reclaimed XP? | Yes: add `ModeratorId?` to `ReclaimedXp` and skip type 2 in the cleanup |
| M-4 | May a channel moderator lock any channel? | No: check permissions on the target channel |
| C-3 | Should `%Mention` ping? | No: keep today's behaviour and delete the sanitiser |
| X-6 | Were reward changes moved out of the mod log on purpose? | Assume not: use `Moderation` |

#### Ranked fix list

Ranked by harm, then by how little the fix costs. "Small" is under about ten lines. Each row can be its own commit.

| Rank | Findings | What it fixes | Size |
|------|----------|---------------|------|
| 1 | L-1 | Deleting a message with an image loses its log | Small, plus an attachment in one test |
| 2 | I-1 | A timed-out request stops the bot or its log queue | No net code |
| 3 | I-2, I-13 (`ToInvite`) | One guild breaks invites and join logs for all | Removes code |
| 4 | M-1 | `/Ban` and `/Kick` can't remove bots | Two lines |
| 5 | M-2 | Registration may fail for every command | One line, plus test changes |
| 6 | P-3 | The first deploy can time out | Two lines |
| 7 | L-2 | The bot's log posts repeat forever | Small |
| 8 | X-1, X-11 | Managed roles block rewards | Removes code |
| 9 | X-2 | Rewards for deleted roles repeat and can't be cleared | Small |
| 10 | S-2 | An outage caches default level settings for a day | Removes code |
| 11 | I-4, I-13, L-12, X-13 (`LogSetAsync`) | Setting commands report success after a failed save | Removes code; see the rulings |
| 12 | I-5, X-5 | Handled failures become crashes | Small per site |
| 13 | L-3, L-4, L-5, L-6, D-7 | Wrong or missing log content | Small each; L-3 and L-6 remove code |
| 14 | S-4, M-5 | Failures skip the mute undo, and eight methods throw | Likely net smaller |
| 15 | I-3, I-15 | A PluralKit failure loses the delete log | Small |
| 16 | M-3, M-11 | `/Forget` loses its mod log | Removes code |
| 17 | X-3, X-4, C-1, C-7 | `/Ignore View`, reward DMs, empty commands | Small each |
| 18 | S-3, M-6, KI-53 | Deploy-time thread locks; spam mute logging | Small; do M-6 with KI-53 |
| 19 | I-6, I-7, I-16 | Image log limits and stream handling | Medium |
| 20 | L-7, L-8, L-9 | Wasted log work and the bulk dump | Removes code |
| 21 | Remaining low behaviour bugs | I-8 to I-12, I-17, M-7 to M-10, L-10, L-11, X-7 to X-10, C-4, C-5, C-8, R-1 | Small each |

After those, these remove code without changing behaviour:

| Rank | Findings | What it removes | Saves |
|------|----------|-----------------|-------|
| 22 | D-1, S-7, D-6 | Validation on trusted paths, and unused `Create` methods; needs S-1 | About 70 lines, plus D-1 and D-6 |
| 23 | D-2, M-7 | The copied sin-with-reason block | About 70 lines |
| 24 | P-4, P-6 | Repeated id conversions and the copied actor converter | About 80 lines, more in Settings |
| 25 | D-3, D-4, D-9, S-9, S-11, I-14, L-13, M-12 | Unused and unreachable code | At least 200 lines |
| 26 | S-5, S-6, S-10, X-13, I-13, the guild ruling | Repeated queries and command boilerplate | About 150 lines |
| 27 | C-6, C-9, D-5, D-8 | Custom command catches and factories | About 110 lines |
| 28 | P-5, S-8 | 12 redundant indexes, in one migration | Lighter XP writes |
| 29 | P-7 | The 18 legacy tables, in a release after `Initial` has run | About 450 lines; must ship later |

The findings that give a number add up to about 900 lines removed, plus about 450 more when P-7 ships. I-18 and I-19 can wait. P-8, S-12, S-13 and X-12 are
best left alone unless they cause a real problem.

#### Order that matters

- S-1 before S-7 and D-1, because all three depend on `FromDatabase` trusting stored values.
- S-4 before X-5's `IgnoreCommandGroup` site, which can't fail until then.
- I-4's helper before L-12 and `LogSetAsync`, so those are written once.
- `ToInvite` from I-13 before I-2, which uses it.
- D-2 before M-7, and D-8 before C-9's `AsNewVersion`.
- L-1's rename of `AttachmentFileName` goes with D-6, not with the L-1 fix.
- P-7 must stay after `Initial` has run on production, or the settings copy is skipped with no error.

#### Duplication across sections

Each of these appears in more than one section, and each has one answer.

- **Save, check, reply, log.** I-4, I-13, L-12 and `LogSetAsync` in X-13 are the same flow. One `ctx.ReplyResultAsync` covers it.
- **Stored values that get revalidated.** D-1, S-1 and S-7 share a cause. One rule covers them: `FromDatabase` trusts the database,
  and `Create` is for input.
- **Catching the wrong exception.** M-5, S-4 and C-6 catch `DbUpdateException` or `DbException`, which an outage never reaches
  because `EnableRetryOnFailure` raises `RetryLimitExceededException`. I-1 is the same mistake with `OperationCanceledException`,
  and I-9 is the reverse, catching everything. The rule is to catch what the layer below actually throws, and only where the catch
  can do something useful.
- **Factories and results that can't fail.** D-5, S-12 and X-13 and C-9 are one pattern, with the `UnreachableException` guards
  in D-2, D-8, M-7, S-10 and C-9 as its cost. There are 27 of those throws in production code, so they make a good progress measure.
- **The same enum switched twice.** M-12 and L-12.
- **Latest username.** M-11 and L-9 need the same query, so the M-11 helper should cover both.
- **DM handling.** `DirectMessageAsync` is the single path. `SpamEvents` and `MuteBackgroundTasks` still have their own, which M-1
  and M-6 fix. `GainUserXp`'s reward DM is separate and fine.

#### Rulings for the questions left open

**`GetRequiredGuild`.** The section 8 question is answered by one C# 14 extension property, `ctx.RequiredGuild`, which returns the
guild or throws `UnreachableException`. It replaces all 53 `ctx.Guild!` and all 10 `GetRequiredGuild` uses. `GetRequiredGuild` goes
too, because `[RequireGuild]` has already refused those contexts and its error branch can't run. It's the one place the impossible
case is stated, which suits principle 10 better than `!` scattered around. This resolves M-12, X-13, C-9 and the guild half of I-19.
The 12 `Guild is null` checks in `Features` are in event handlers and attributes, where a null guild is real, so they stay.

**A shared `AddField` wrapper: no.** M-3, M-7, I-17 and L-11 all lose a log to `AddField`, but there are 102 call sites and only
these few can receive blank or oversized values. Fix those sites.

#### Dropped event handlers

`main`'s `ChannelDeleted`, `ChannelAdded`, `RoleDeleted` and `RoleAdded` were the only behaviour dropped from the event list, apart
from the voice event that served `/Track`. `ChannelAdded` and `RoleAdded` only added `Channels` and `Roles` rows, which nothing reads
now, so nothing depended on them. Dropping the two delete handlers ended the cascades. Rewards are the one real cost, already in
X-2, plus R-1 below.

**R-1 (low): a deleted log channel costs a failed request per log, and nobody is told.** `main`'s cascade cleared the setting. The
branch keeps it, and `GetChannelOrDefaultAsync` misses the cache, so each log posted for that channel makes one REST call that returns
404. The publisher works through one queue for all guilds, so a busy guild with a deleted log channel slows everyone's logs a little.
The log is lost either way, as on `main`. Not run. Fix: in `GuildLogPublisher`, report `CannotSendInChannel` when the channel is null,
alongside I-11. Clearing the setting would also stop the REST calls, but it adds more code.

**Checked, no finding:** A deleted channel or thread with a lock resolves through `ExpiredItemTriage`. A deleted mute role is handled by
`MuteRoleResolver`, with an issue reported. Log cleanup drops a deleted channel's posts after 3 tries, and the 31-day purge removes its
`Messages` rows, which `main` removed at once. Stale override rows are skipped by both `View` commands and never match a channel, so
they cost one lookup per `View`. Stale ignored items do nothing and show as `<#id>` or `<@&id>`, unlike X-10's blank reward rows. A
deleted `UserCommandChannel` makes `/Level` ephemeral everywhere, the same as `main`'s null. Nothing maps a foreign key to `Channels`
or `Roles` any more. `/Track` and `/Untrack` are gone deliberately, per `spec-migration-plan.md`. The only other command changes are
additions.

#### Regressions from `main`

Everything marked as a regression, or new to the branch, in sections 1 to 8:

| Severity | Findings |
|----------|----------|
| high | S-1, I-1, I-2, M-1, L-1, L-2, X-2 |
| med | D-1, P-1, P-2, S-2, S-3, S-4, I-4, I-5, M-2, M-3, M-5, L-3, L-4, L-5, L-6, L-7, X-3, C-2 |
| low | L-9, part of L-11, X-6, X-7, X-8, part of X-10, C-4, C-5, C-7, part of C-8, R-1 |

These are also on `main`, so they aren't new: X-1, I-3, I-6, M-4, M-6, M-8, M-9, C-1, L-10, X-9, X-11. X-4 is too, apart from its
log level. Fixing them is still worthwhile, but none has to hold up the branch. X-1 is the exception worth fixing regardless.

#### Test gaps behind the regressions

The fake accepted what real Discord wouldn't, which is why these got through. Three small changes would catch most of them:

- Send attachments in `FakeGuild` payloads, and let roles be `managed` (L-1, X-1).
- Make `FakeDiscordRest` enforce the embed limits of 6,000, 4,096 and 1,024, and refuse empty messages (L-5, X-3, X-10, I-6, I-12, C-1).
- Give the leveling log its own channel in the tests, which exposes X-6.

#### Before deploying

**Production queries.** Run the blocks in sections 2, 3 and 7. The last two in section 2 abort a migration if they aren't zero. Those
sizing P-1, P-2 and S-1 inform the decisions above, the S-3 one sizes that fix, and section 7's checks the `leaderboard_view` refresh
against the 30-second timeout.

**Real Discord.** Each of these was left open by a section:

- Does a string option with `min_value` register? (M-2: this one can stop every command, so test it first.)
- Does a signed attachment URL still download after the message is deleted? (L-1)
- Does a follow-up after deleting a deferred response work? (KI-21)
- Are empty `content` and empty embed descriptions refused, and does `parse: []` suppress pings on interactions? (C-1, C-3)
- Does Discord reject a logged edit's second embed over 6,000 characters? (L-5)
- Does the Server Booster role report `managed: true`? (X-1)
- What happens to a 1,000-character reason in the audit log header, and to banning someone already banned? (section 5)
- Does the event order for a used-up single-use invite reach KI-50's fallback? (section 6)
