# Section 5: Message hot path

Reviewed 2026-11-09, against the code as it will be once every fix in `review-decisions.md` is in (F-1 is still an open
question, so the counts below are given for today's settings layer).

An ordinary message costs **3 database round trips and 9 cache reads**, and no Discord REST or PluralKit calls. A message
that earns XP costs **5 round trips and 11 cache reads**. Two changes bring both cases down by 2 round trips: an
in-memory XP cooldown that also fixes KI-74 (HOT-1), and saving the logged message without a separate `COMMIT`
(HOT-2). One `MessageCreated` pipeline with a shared `MessageContext` (HOT-3) cuts the cache reads from 9 to 5. It also
lets a locked thread stop the other handlers (HOT-4). Outside `MessageCreated`, a new reaction costs 3 round trips
where 1 would do (HOT-6). Link embeds cause `MessageUpdated` events, and each costs a query that a field check can skip
(HOT-7). Findings HOT-1 to HOT-10 follow.

## Assumptions behind the counts

- **Common case:** a regular member with no Manage Channels permission posts in a text channel that sits in a category.
  The message isn't a command, isn't spam and isn't in a thread. Every module is on, the guild has no overrides and no
  ignores, and the caches are warm.
- **Cache** means a FusionCache read through `HybridCache` (`GrimoireServiceRegistration.cs:238`). There's no L2 and no
  backplane, so a cache hit is in-process. Each read still goes through `ExecuteSafelyAsync` (`SettingsModule.cs:102`)
  and its `Task`/`Result` wrappers.
- **DB round trip:** a `SaveChangesAsync` that holds more than one statement is counted as the batch plus a `COMMIT`.
  EF wraps a multi-statement save in a transaction. Npgsql sends `BEGIN` together with the first command, but `COMMIT`
  is a round trip of its own. I haven't measured this. HOT-2 includes a spike to confirm it.
- P-8 (pooled context factories) is assumed in. Creating a context then costs no round trip, but each handler still
  rents its own context.
- `GuildLog.SendLogMessageAsync` (`GuildLog.cs:32`) only queues the log. The log-channel lookup and the REST post happen
  on the consumer, so they're listed as "queued" and not counted on the event.
- DSharpPlus runs the handlers for one event at the same time (see `project_reaction_tracking` in memory). Each event
  resolves a new instance of every handler, because `AddEventHandlers` is called without a lifetime
  (`GrimoireServiceRegistration.cs:120-150`).

## Per-event cost table

### `MessageCreated`, common case

| Handler | Gate before any I/O | DB round trips | Cache reads | Discord REST | PluralKit |
|---|---|---|---|---|---|
| `GainUserXp`, member in cooldown | Default/Reply, `DiscordMember`, not bot | 1: the compiled `Max(TimeOut)` (`GainUserXp.cs:59`) | 2: `IsModuleEnabled(Leveling)`, `GetAllIgnoredItems` | 0 | 0 |
| `GainUserXp`, earns XP | same | 3: `Max`, `Sum(RawXp)` (`:64`), insert (`:86`) | 4: also `GetLevelingSettings`, `GetLevelingRewardsAsync` | 0, or one `GrantRoleAsync` per missing reward | 0 |
| `AddMessageEvent` | guild, not the bot itself, Default/Reply | 2: one batch (`Message`, `MessageCreatedEntry`, attachments) plus `COMMIT` (`AddMessageEvent.cs:77`) | 3: `IsModuleEnabled(MessageLog)`, then `GetChannelLogOverride` for the channel and for the category (`ShouldLogMessage`, `SettingsModule.MessageLog.ChannelOverrides.cs:18`) | 0 | 0 |
| `LockedThreadEvents` | guild, is a thread | 0 (not a thread) | 0. In a thread: 2 (`IsModuleEnabled(Moderation)`, `IsThreadLocked`) | 0. In a locked thread: 1 delete | 0 |
| `TextCustomCommandProcessor` | none: every message goes through the `Validation` pipeline (F-10) | 0 | 0 | 0 | 0 |
| `SpamEvents` | not bot, guild | 0 | 4: `IsModuleEnabled(AntiSpam)`, `GetSpamFilterOverrideAsync` for the channel and the category, the member's `SpamTracker` (`SpamTrackerModule.cs:50`, `:64`) | 0 | 0 |
| **Total, in cooldown** | five different gates | **3** (2 statements and a `COMMIT`), 2 contexts | **9** | **0** | **0** |
| **Total, earns XP** | | **5**, 2 contexts | **11** | 0, or one grant per missing reward. A level-up log is queued | **0** |

A message in a thread adds 2 cache reads in `LockedThreadEvents` and one more override read in each of `AddMessageEvent`
and `SpamEvents`, for 13 in all. Once X-12 is in (an ignored channel covers its threads), `GainUserXp` walks the same
ancestors a third time. When a text command is found, `TextCustomCommandProcessor` adds 1 cache read, 2 DB round trips
(the lookup and `RecordCommandUsageAsync`) and 1 REST send.

### The other message events

| Event, case | DB round trips | Cache reads | Discord REST | PluralKit |
|---|---|---|---|---|
| `UpdateMessageEvent`, real edit of a logged message | 2: select (`UpdateMessageEvent.cs:39`), insert (`:71`) | 1 | 0 to 2: `GetUserAvatar` does a member lookup, then a user lookup (both usually cached). Log queued | 0 |
| `UpdateMessageEvent`, link embed added, content unchanged | 1: the select, then a return at `:57` | 1 | 0 | 0 |
| `DeleteMessageEvent`, member deletes their own logged message, older than 60 s | 2: select (`DeleteMessageEvent.cs:48`), insert (`:133`) | 2 (`IsModuleEnabled`, `IsLogChannelSet`), +2 if an audit entry is found | 1 `GetAuditLogsAsync` (`DiscordAuditLogParserService.cs:48`), 0 to 2 avatar lookups, one CDN download per image. Log queued | 0 |
| same, younger than 60 s | 2 | 2 | same | 1 (`PluralkitService.cs:47`) |
| `DeleteMessageEvent`, PluralKit removing an original it proxied | 2: select, link insert (`:96`) | 1 | 0 | 1 |
| `DeleteMessageEvent`, Grimoire deleting spam or a locked-thread message | 2 | 2 | 1 audit-log read, plus avatar lookups. Log queued | 1, because the message is always under 60 s old |
| `ReactionEvents`, new reaction on a logged message | 3: `ReviveAsync` (`ReactionEvents.cs:107`, 0 rows), `Messages.AnyAsync` (`:108`), insert (`:114`) | 1 | 0 | 0 |
| `ReactionEvents`, reaction on a message that wasn't logged | 2 | 1 | 0 | 0 |
| `ReactionEvents`, re-add / remove / clear | 1 | 1 | 0 | 0 |
| `LockedThreadEvents`, reaction in a thread | 0 | 2 | 1 delete if the thread is locked | 0 |

### Now and proposed, per message

| Case | Now: DB / cache / REST / PK | Proposed: DB / cache / REST / PK | Findings |
|---|---|---|---|
| Ordinary message, member in cooldown | 3 / 9 / 0 / 0 | **1** / **5** / 0 / 0 | HOT-1, HOT-2, HOT-3, HOT-5 |
| Ordinary message, earns XP | 5 / 11 / 0 / 0 | **3** / **7** / 0 / 0 | HOT-1, HOT-2, HOT-3, HOT-5 |
| Message in a locked thread (deleted) | 3 to 5 for the message, then 2 DB, 1 audit read and 1 PK call on the delete | 0 for the message, then 1 DB and no audit read or PK call on the delete | HOT-4 |
| New reaction on a logged message | 3 / 1 / 0 / 0 | **1** / 1 / 0 / 0 | HOT-6 |
| Link embed added (update, content unchanged) | 1 / 1 / 0 / 0 | **0** / 0 / 0 / 0 | HOT-7 |
| Delete of a bot or webhook message under 60 s | 2 / 2 / 1+ / 1 | 2 / 2 / 1+ / **0** | HOT-8 |

The cache-read savings are in-process and cheap on their own: a FusionCache hit costs well under a microsecond plus
about ten allocations (HOT-9). The round trips are where the time goes. On a busy guild, HOT-1 and HOT-2 together
remove about two thirds of the database traffic `MessageCreated` causes.

## Findings

#### HOT-1 Keep the XP cooldown in memory, and claim it atomically  `perf` `state` · **high**
- **Where:** `GainUserXp.HandleEventAsync` (`Grimoire/Features/Leveling/Events/GainUserXp.cs:58-66`), the compiled
  `_getUserXpInfoQuery` (`:27-31`), on every `MessageCreated` in a guild with Leveling on.
- **Now:** every message from a member asks the database for `Max(TimeOut)`, which is one indexed query. With the
  default 3-minute timeout (`XpTimeoutPeriod.Default`), almost every message from an active member pays for it only to
  learn it's in cooldown. A message that earns XP then runs a second query for `Sum(RawXp)` over the same rows, and then
  the insert. The check and the insert aren't atomic, so two messages arriving together can both earn (KI-74).
- **Proposed:** a singleton `XpCooldowns`, keyed by guild and user, that holds when each member's cooldown ends. The
  database is asked only on the first message after start-up. The claim is a compare-and-swap, so only one of two
  concurrent messages wins. On a miss, one query reads `Max` and `Sum` together. On a hit, the earn path needs only the
  `Sum`.
  ```csharp
  internal sealed class XpCooldowns
  {
      private readonly ConcurrentDictionary<(GuildId, UserId), DateTimeOffset> _until = new();

      public bool TryGet(GuildId guild, UserId user, out DateTimeOffset until) => this._until.TryGetValue((guild, user), out until);

      /// <summary>Wins the next gain if <paramref name="seen" /> is still current and has passed.</summary>
      public bool TryClaim(GuildId guild, UserId user, DateTimeOffset seen, DateTimeOffset now, DateTimeOffset next)
          => seen <= now && (this._until.TryAdd((guild, user), next) || this._until.TryUpdate((guild, user), next, seen));
  }

  // GainUserXp: cooldown first, then the ignore list, then one query only for a gain
  var seen = cooldowns.TryGet(guildId, userId, out var until) ? until : await LoadStoredCooldownAsync(...);
  if (!cooldowns.TryClaim(guildId, userId, seen, now, now + settings.XpTimeoutPeriod.Value)) return;
  ```
  `/Award` and `/Reclaim` set `TimeOut` to now (branch review, Leveling "Checked, no finding"), so they can never move a
  cooldown, and nothing else needs to invalidate the map. Each guild belongs to one shard, so the map stays correct if
  the bot is ever sharded. Expired entries are a few dozen bytes each. They can be pruned on the existing
  `LeaderboardRefreshBackgroundTask` tick, or left alone. If the insert fails after a claim, the member loses one gain,
  which is the same outcome as today's failure path.
- **Saves:** 1 round trip on every message in cooldown, and 1 on every gain (the `Max` drops out). The race in KI-74
  goes away.
- **Size:** about +35 lines (new file and one registration line) and −5 in `GainUserXp.cs`. 3 files.
- **Needs:** nothing.
- **Interacts with:** KI-74 (fixed by this), F-7 (`BuildEarnedXp`), X-12. Section 10 shouldn't re-report it.

#### HOT-2 Save the logged message without a separate `COMMIT`  `perf` · **high**
- **Where:** `AddMessageEvent.HandleEventAsync` (`Grimoire/Features/Logging/MessageLogging/AddMessageEvent.cs:74-89`),
  on every message in a logged channel.
- **Now:** one `SaveChangesAsync` inserts the `Message`, its `MessageCreatedEntry` and any attachments. That's more
  than one statement, so EF opens a transaction. Npgsql sends the batch with `BEGIN` in front of it, and then sends
  `COMMIT` as a second round trip.
- **Proposed:** `dbContext.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;` before the save. EF still
  sends one batch. With no explicit transaction, Npgsql ends the batch with a single `Sync` (unless
  `EnableErrorBarriers` is set, and it isn't), so PostgreSQL runs every statement before that `Sync` as one implicit
  transaction. A replayed message that hits `UniqueConstraintException` still writes nothing. Do this only for this
  save. Every other hot-path save is a single statement, and EF already skips the transaction for those.
- **Saves:** 1 round trip on every logged message, if the spike confirms it.
- **Size:** +2 lines, 1 file.
- **Needs:** a spike. Turn on Npgsql command logging in an integration test and check two things: that the save sends
  one batch and no separate `COMMIT`, and that a replayed message leaves no orphan `MessageCreatedEntry`
  (`MessageLogEventsTests` covers the replay). If either check fails, drop this finding.
- **Interacts with:** P-8. Section 3 may count the same round trip. Keep one of the two.

#### HOT-3 One `MessageCreated` pipeline with a `MessageContext` computed once  `perf` `remove` `state` · **med**
- **Where:** the five `HandleEventAsync(DiscordClient, MessageCreatedEventArgs)` methods: `GainUserXp.cs:38-56`,
  `AddMessageEvent.cs:24-38`, `LockedThreadEvents.cs:19-33`, `TextCustomCommandProcessor.cs:27-48` and
  `SpamEvents.cs:28-41`, plus `SettingsModule.IsModuleEnabled` (`SettingsModule.ModuleEnabled.cs:41`) and the two
  channel-ancestor walks: `ShouldLogMessage` with `BuildChannelTree().ToDictionary()` (`AddMessageEvent.cs:37`), and
  `CheckSpam`'s `Parent` loop (`SpamTrackerModule.cs:48-62`).
- **Now:** each handler writes its own gate, and no two are the same. `GainUserXp` and `TextCustomCommandProcessor`
  check the message type, member and bot flag. `AddMessageEvent` checks the guild, self and the type, but not the bot
  flag, because PluralKit proxies are webhook messages. `SpamEvents` checks only the bot flag and the guild, so it scores
  system messages such as "X joined" and "X pinned a message", which carry the member as author. Three handlers each
  read their module's enabled flag, `GainUserXp` builds a `HashSet` of role ids on every message (`:53`), and the
  channel's ancestors are walked twice (three times after X-12). None of the handlers knows what the others decided, so
  a message the lock is about to delete still earns XP, gets logged and can trigger a custom command (HOT-4).
- **Proposed:** register one handler, `MessageCreatedPipeline`. It builds the context once and runs the feature steps,
  which become ordinary methods with no DSharpPlus event interface:
  ```csharp
  internal sealed record MessageContext(
      DiscordMessage Message, DiscordMember Member, GuildId GuildId, ChannelId ChannelId,
      ImmutableArray<ChannelId> Ancestors,  // the channel, its parent, then the category
      GuildModuleState Modules)
  {
      public static MessageContext? From(MessageCreatedEventArgs args, GuildModuleState modules)
          => args is { Guild: { } guild, Author: DiscordMember member, Message.MessageType: Default or Reply }
              ? new(args.Message, member, guild.GetGuildId(), args.GetChannelId(), [.. args.Channel.Ancestors()], modules)
              : null;
  }

  public async Task HandleEventAsync(DiscordClient client, MessageCreatedEventArgs args)
  {
      if (args.Author.Id == client.CurrentUser.Id) return;
      var modules = await settings.GetModuleStateAsync(args.Guild.GetGuildId());   // one cache read
      if (MessageContext.From(args, modules) is not { } ctx) { await log.SaveAsync(args, modules); return; } // webhooks
      if (await locks.EnforceAsync(ctx)) return;                                   // HOT-4
      await Task.WhenAll(Run(log.SaveAsync(ctx)), Run(xp.GainAsync(ctx)), Run(spam.CheckAsync(ctx)),
          Run(commands.RunAsync(ctx)));
  }
  ```
  `Run` catches each step's exception and passes it to `IClientErrorHandler.HandleEventHandlerError` under the step's
  name, so one failing step still doesn't hide another. Today the alert names only the event, `MessageCreated`, so this
  is an improvement. `GetModuleStateAsync` reads one per-guild cache entry. That entry is built with the existing
  `GetGuildSettings` query (`SettingsModule.cs:55`), which takes six types, and `SetModuleState` invalidates it.
  Today's `GetAllModuleState` does six reads (`SettingsModule.ModuleEnabled.cs:52`). Each step keeps its own gate only
  where the gate differs from the shared one: the bot flag for XP, commands and spam, and Manage Messages for the lock.
- **Saves:** cache reads go from 9 to 5 per message (module state read once, and one override read per feature after
  HOT-5), and about 40 allocations per message. There are no round trips on its own. The main gain is one gate, a fixed
  order (lock first), and one place to add a step.
- **Size:** about +70 lines (pipeline, context, `Run`, `GetModuleStateAsync`) and −60 lines (five gates, three module
  reads, two ancestor walks, the `ToDictionary`). The five handler files plus `GrimoireServiceRegistration.cs` and
  `SettingsModule.ModuleEnabled.cs`. No test builds a handler directly, so the integration tests keep working.
- **Needs:** F-10 for the custom-command step (its early return becomes the shared gate). F-1 makes
  `GetModuleStateAsync` a plain `ValueTask<GuildModuleState>`.
- **Interacts with:** F-10, F-1, X-12 (the ignore check takes `ctx.Ancestors`), L-2 (the self check stays first),
  KI-107. Section 4 owns the per-guild module-state cache entry. Sections 9 to 11 shouldn't re-report the gates.

#### HOT-4 A locked thread or a spam verdict still pays the full cost of the message and of its delete  `perf` · **med**
- **Where:** `LockedThreadEvents.HandleEventAsync` (`LockedThreadEvents.cs:31-32`) and `SpamEvents` (`SpamEvents.cs:102`),
  running alongside the other handlers. Then `DeleteMessageEvent.HandleEventAsync` (`DeleteMessageEvent.cs:74-116`).
- **Now:** thread locks work by deleting each new message (KI-20). While that happens, the other handlers run in
  parallel: the message is saved, XP may be earned, and a `!command` gets its reply. The delete then comes back as a
  `MessageDeleted` event. The message is always under 60 seconds old, so the handler calls PluralKit, reads the audit
  log, saves a history row and posts a delete log, all to find out that Grimoire deleted it.
- **Proposed:** two parts.
  1. In HOT-3's pipeline, run `locks.EnforceAsync(ctx)` before the other steps, and return if it deleted the message.
     Whether the message should still be logged is a question for the owner (see Questions).
  2. A `BotDeletions` handoff like `BanHandoff` (`Grimoire/Features/Moderation/Ban/BanHandoff.cs:39`). The lock and spam
     paths call `Expect(messageId, reason)` before `DeleteAsync`. `DeleteMessageEvent` calls `Take(messageId)` right
     after its row lookup. When the take succeeds, it skips PluralKit and the audit log, and records
     `MessageDeletedByModeratorEntry` with the bot as moderator. Entries expire after about a minute, as `BanHandoff`'s
     do.
- **Saves:** for each message in a locked thread, 3 to 5 round trips on the create, plus 1 PluralKit call and 1 audit-log
  read on the delete. For each spam mute, 1 PluralKit call and 1 audit-log read.
- **Size:** about +30 lines (handoff and registration), about −2. 4 files.
- **Needs:** HOT-3 for part 1. Part 2 needs nothing.
- **Interacts with:** KI-20, KI-55, KI-64 (the audit-log parser's cached count no longer moves for bot deletes, which
  makes it simpler to reason about), I-3.

#### HOT-5 Load channel overrides once per guild, not once per channel  `perf` · **med**
- **Where:** `SettingsModule.GetChannelLogOverride` (`SettingsModule.MessageLog.ChannelOverrides.cs:48-69`, key
  `LogOverrides_{channelId}`) and `GetSpamFilterOverrideAsync` (`SettingsModule.Moderation.SpamFilterOverrides.cs:17-42`,
  key `SpamFilterOverrides_{channelId}`). These are read for every ancestor of every message's channel.
- **Now:** each channel and each category has its own cache entry for each of the two kinds. A guild with no overrides
  still pays 2 reads per kind on each message, and 3 in a thread. Each new thread or forum post, and each channel once a
  day when its entry expires, misses both caches and runs 2 queries, one for each kind, before its first message is
  handled. `IsThreadLocked` (`SettingsModule.Moderation.Locks.cs:118`) already does this the cheap way, with one
  `FrozenSet` per guild.
- **Proposed:** one cache entry per guild for each kind, `FrozenDictionary<ChannelId, Option>` holding only the
  non-`Inherit` rows. The setter invalidates the guild's key. The walk becomes a pure function over HOT-3's ancestors:
  ```csharp
  static bool ShouldLog(FrozenDictionary<ChannelId, MessageLogOverrideOption> overrides, ImmutableArray<ChannelId> ancestors)
      => ancestors.Select(overrides.GetValueOrDefault).FirstOrDefault(o => o is not MessageLogOverrideOption.Inherit)
          is not MessageLogOverrideOption.NeverLog;
  ```
- **Saves:** 2 to 4 cache reads per message become 2. A guild has one miss for each kind per day, instead of one per
  new thread, so busy forum guilds lose most of their override queries.
- **Size:** about −20 lines across the two settings files, and `ShouldLogMessage` loses its loop. 4 files.
- **Needs:** nothing. HOT-3 supplies the ancestors, but `BuildChannelTree` works too.
- **Interacts with:** KI-71 (overrides on deleted channels never matching), KI-103. Section 4 owns the cache shape. If
  it proposes the same thing, keep section 4's version and cite HOT-5 for the per-message numbers.

#### HOT-6 One upsert per reaction  `perf` · **med**
- **Where:** `ReactionEvents.SaveReactionAsync` and `ReviveAsync` (`Grimoire/Features/Logging/MessageLogging/ReactionEvents.cs:103-130`),
  on every `MessageReactionAdded` in a guild with MessageLog on.
- **Now:** an `UPDATE` tries to revive the reaction. For a new reaction it matches nothing, so `AnyAsync` checks that the
  message was logged, and then an `INSERT` follows. That's 3 round trips for the most common case, 2 for a reaction on a
  message that wasn't logged, and 1 for a re-add. Two concurrent adds of the same reaction rely on the
  `UniqueConstraintException` catch.
- **Proposed:** one statement.
  ```csharp
  dbContext.Database.ExecuteSqlAsync($"""
      INSERT INTO "Reactions" ("MessageId", "UserId", "GuildId", "Emoji", "ReactedAt", "RemovedAt")
      SELECT {(decimal)r.MessageId.Value}, {(decimal)r.UserId.Value}, {(decimal)r.GuildId.Value}, {r.Emoji.ToString()}, {r.ReactedAt}, NULL
      WHERE EXISTS (SELECT 1 FROM "Messages" WHERE "Id" = {(decimal)r.MessageId.Value})
      ON CONFLICT ("MessageId", "UserId", "Emoji")
      DO UPDATE SET "ReactedAt" = EXCLUDED."ReactedAt", "RemovedAt" = NULL
      """);
  ```
  Keep the `ReferenceConstraintException` catch for a message purged between the `EXISTS` and the insert. The duplicate
  race goes away.
- **Saves:** 2 round trips on every new reaction to a logged message, and 1 on a reaction to a message that wasn't
  logged.
- **Size:** about +10 lines and −20. 1 file.
- **Needs:** the raw SQL ties the code to the table and column names and to `numeric(20,0)` ids, which the model
  snapshot shows. `ReactionTests` covers the behaviour. If the owner doesn't want raw SQL here, swap the order instead:
  insert first, revive on `UniqueConstraintException`, and skip on `ReferenceConstraintException`. That's 1 round trip
  for a new reaction, but every reaction on a message that wasn't logged raises a database error.
- **Interacts with:** `project_reaction_tracking`. Section 3 and section 9 may count the same query.

#### HOT-7 Skip `MessageUpdated` events that aren't edits before querying  `perf` · **med**
- **Where:** `UpdateMessageEvent.HandleEventAsync` (`Grimoire/Features/Logging/MessageLogging/UpdateMessageEvent.cs:27-60`
  and `:79`).
- **Now:** Discord sends `MESSAGE_UPDATE` when it adds a link's embed, and for pins and flag changes. For each one, the
  handler reads the module setting and queries the stored row with its latest content, and only then finds that the
  content didn't change (`:57`). For a real edit, it looks up the avatar (`:79`) and builds both embeds even when the
  guild has no edit log channel. `GuildLog` then drops the message.
- **Proposed:**
  ```csharp
  if (args is not { Guild: { } guild, Message: { EditedTimestamp: not null, Content: { Length: > 0 } content } }
      || args.MessageBefore?.Content == content)
      return;
  ```
  Discord sets `edited_timestamp` only when the user edits, and `MessageBefore` is non-null whenever the message was in
  DSharpPlus's cache. After the row is saved, check `IsLogChannelSet(GuildLogType.MessageEdited, …)` before the avatar
  and embed work, as `DeleteMessageEvent` already does (`DeleteMessageEvent.cs:136`).
- **Saves:** 1 query for every message that gets a link embed (any message with a URL or GIF link), and 0 to 2 member
  or user lookups on every edit in guilds without an edit log.
- **Size:** about +4 and −3 lines, 1 file.
- **Needs:** a check against real Discord that an embed-only update arrives with `EditedTimestamp` null when the message
  is uncached, and that a real edit always carries it. The test harness's fake gateway will need to send
  `edited_timestamp` on edits.
- **Interacts with:** KI-69 (an edit that removes all the text isn't logged). The `Content.Length > 0` arm keeps that
  behaviour, so fix KI-69 there if it gets fixed. KI-38.

#### HOT-8 Don't ask PluralKit about messages it can't have proxied  `perf` · **low**
- **Where:** `DeleteMessageEvent.HandleEventAsync` (`DeleteMessageEvent.cs:74-76`) and
  `PluralkitService.GetProxiedMessageInformation` (`Grimoire/Features/Shared/PluralKit/PluralkitService.cs:47-52`).
- **Now:** every delete of a logged message under 60 seconds old makes a PluralKit call, which is limited to 2 a second.
  That includes deletes of webhook and bot messages, and deletes of proxies. A proxy's own lookup returns
  `Id == args.Message.Id`, so the result is thrown away (`:81`). Only an original, which is always a human's message, can
  produce a link.
- **Proposed:** skip the call when the cached message says it can't be an original:
  call only when `args.Message is not { Author: { } author } || author is { IsBot: false } && args.Message.WebhookId is null`.
  An uncached message, with no author, keeps the call. Together with HOT-4 part 2, the rate limiter is left for the real originals.
- **Saves:** 1 PluralKit call for each deleted proxy or bot message under 60 seconds old. In PluralKit guilds this takes
  away a good part of what pushes the 2-a-second limit (I-3).
- **Size:** +2 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** I-3, KI-101.

#### HOT-9 Cached settings reads allocate on every hit  `perf` · **low**
- **Where:** `SettingsModule.GetGuildSetting` (`SettingsModule.cs:31-49`, the anonymous state `new { key, guildId,
  this._dbContextFactory }` at `:36`), `GetChannelLogOverride` (`new { channelId, guildId }`), `GetSpamFilterOverrideAsync`
  (`new { GuildId, ChannelId }`), the interpolated keys in `CacheKey`, and `IsModuleEnabled`'s `ToSettingType` →
  `BindAsync` → `Map` chain (`SettingsModule.ModuleEnabled.cs:41-49`). These run 9 times per message.
- **Now:** even on a hit, each read allocates its state object, its key string, the closures for `ExecuteSafelyAsync`,
  two or three async state machines, a `Task` and a `Result` or two. I estimate about ten allocations per read and about
  100 per message. This is from reading the code, not measured.
- **Proposed:** pass value tuples as state, since `GetOrCreateAsync<TState, T>` is generic and a tuple doesn't box. With
  F-1, return `ValueTask<T>` straight from `GetOrCreateAsync`, so a hit completes synchronously. HOT-3 and HOT-5 cut the
  reads from 9 to 5. The key strings can stay.
- **Saves:** most of the roughly 100 allocations per message. No I/O.
- **Size:** about ±0 lines. 4 files in `Grimoire.Settings/Services`.
- **Needs:** F-1 for the `ValueTask` half. The tuple half needs nothing.
- **Interacts with:** F-1, S-4. Section 4 may report the same thing. If so, keep one.

#### HOT-10 Write-behind batching for message rows  `perf` · **optional**
- **Where:** `AddMessageEvent.HandleEventAsync` (`AddMessageEvent.cs:40-89`), and the inserts in `GainUserXp`.
- **Now:** each message makes its own save, so the round trips grow with message volume.
- **Proposed:** an option, not a recommendation. Queue `Message` rows on a bounded `Channel`, as `GuildLog` does, and
  flush them every 100 ms or every 100 rows in one save. With HOT-1 and HOT-2 in, this would take a busy guild from 1
  round trip per message to 1 per flush. The cost: a delete, edit or reaction that arrives before the flush finds no row.
  PluralKit deletes its originals within about a second, so that window matters. A replayed message would fail the whole
  batch unless the flush used `ON CONFLICT DO NOTHING`. And a crash loses up to one flush of messages.
- **Size:** about +60 lines, 2 files.
- **Needs:** an owner decision (see Questions).
- **Interacts with:** HOT-2, `project_reaction_tracking` (it already accepts out-of-order events).

## Checked, no finding

- **PluralKit on `MessageCreated`:** none of the five handlers calls it. Only `DeleteMessageEvent` does, after its row
  lookup (L-8).
- **`DeleteMessageEvent` order:** it looks up the row first and returns when there isn't one, checks `IsLogChannelSet`
  before the avatar and image work, and reads the audit log only when the delete isn't a PluralKit original. Running the
  PluralKit and audit-log calls in parallel would cut latency, but each PluralKit original would then cost an extra
  audit-log read, and those are the busiest deletes. A log isn't latency-sensitive, so keep them in sequence.
- **`UpdateMessageEvent`, `DeleteMessageEvent` and `ReactionEvents` gate on the module, not on `ShouldLogMessage`:**
  the stored row is the gate, which is cheaper and correct, because a message that wasn't logged has no row.
- **`GuildLog`:** it only queues, so level-up, reward and spam logs add nothing to the event itself.
- **`SpamTracker` in FusionCache:** a mutable object in an L1-only cache, behind a `lock`, works. Whether it should be a
  `ConcurrentDictionary` is section 4's question.
- **`TextCustomCommandProcessor`:** F-10 covers the early return. Caching command definitions is section 4's or
  section 11's call. Only command messages reach the query.
- **`ApplyRewards` on every gain:** one cache read and in-memory work, with REST only when a member lacks a reward. That
  belongs to section 10 (KI-75).
- **cs15:** nothing on the hot path. The `_ =>` arm in `IsMessageIgnored` goes with section 2's or section 4's `closed`
  decision for `XpIgnoredItem`.

## Questions for the owner

1. **HOT-3: one pipeline or five handlers?** One registered handler gives one gate, one module-state read and a fixed
   order. It changes how failures are reported: by step name, through `IClientErrorHandler`. The feature folders keep
   their logic, but no longer register their own `MessageCreated` handler. Is that split acceptable?
2. **HOT-4: messages in a locked thread.** Once the lock has deleted a message, should it still be saved to the message
   log, so the delete log shows what was said? Should it still earn XP or run a custom command? I'd skip XP and commands,
   and keep the log row, with the bot recorded as deleter through the handoff.
3. **HOT-3: system messages and the spam filter.** With the shared Default/Reply gate, `SpamEvents` stops scoring
   member-join, pin and thread-created notices. I assume that's wanted. Confirm?
4. **HOT-6: raw SQL.** Is one `INSERT … ON CONFLICT` acceptable here, or do you want the EF-only fallback, with its
   database error for each reaction on a message that wasn't logged?
5. **HOT-10: batching.** Do you want write-behind for message rows at all? It only pays off on busy guilds, and it widens
   the window in which a fast delete or edit finds no row.
6. **HOT-1: the member's XP total.** Caching the running `Sum` too would make a gain cost 1 round trip (the insert)
   instead of 2. But `/Award`, `/Reclaim` and any future XP reset would then have to update it. I left this out. Want it?
