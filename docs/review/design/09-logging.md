# Section 9: Logging

Reviewed 2026-11-09, against the code as it will be once every fix in `review-decisions.md` and the KI decisions is in.

The biggest win is merging the three `GuildMemberUpdated` handlers into one, built on a pure diff (LOG-1). Today every
member update, a role change included, costs three module checks, three contexts and three queries. Next comes gating
log building in one place, `GuildLog`, rather than in each handler, which also covers the edit log that L-7 missed
(LOG-2). After that, the cleanup should stop swallowing outages, because three bad minutes leave log posts up forever
(LOG-6), and `InviteService` should replace its whole snapshot, which removes the "Could not find guild." throws
(LOG-8). Twelve findings: 1 high, 3 med, 7 low, 1 optional, about −230 lines in all.

**How this section measures against section 1.**
- **F-5 (T-1):** Logging's commands are `/Log Message Override`, `Set`, `View`, `ViewOverrides` and `/Log User Set`,
  `View`. The two Sets go through `ChannelSettingCommand`, which is shared, so section 8 counts them. Override and the
  two Views save about −12 lines on `ctx.RunAsync`: the two `SlashCommandContext` defer branches become
  `Visibility.Private` (I-19), and Override's defer and `ReplyResultAsync` become one call. LOG-9 saves more on top.
  `ViewOverrides` is paged, so it keeps the primitives.
- **F-1:** 11 `IsModuleEnabled(...).GetOrElse(() => false)` sites in this section become plain awaits. Lines stay the
  same, each one shorter.
- **T-4:** the three `_ => throw new InvariantViolationException` arms (Override, both Sets) are enum guards. Keep them.
- **F-9:** `CleanupLogsBackgroundTask` doesn't use `ProcessItemAsync`. LOG-6 uses it.
- **F-10:** the handlers here already skip with `is not`/`return`. `ReactionEvents.ToReactionEmoji` uses
  `Validation.Match` for an emoji it can't store, which is a real outcome (it logs a warning), so it stays.
- **Left to section 5:** the per-event round trips of `AddMessageEvent`, `UpdateMessageEvent`, `DeleteMessageEvent`
  and `ReactionEvents`. One example is `ReactionEvents.SaveReactionAsync`, which does an UPDATE, then a SELECT, then an
  INSERT, where one upsert would do. The findings below cover only the internal design of those files.

## Findings

#### LOG-1 One handler for member updates, with a pure diff  `remove` `perf` `state` · **high**
- **Where:** `UserLogging/UpdatedUsernameEvent.cs:23-63`, `UpdatedNicknameEvent.cs:23-71`, `UpdatedAvatarEvent.cs:26-76`,
  registered separately at `GrimoireServiceRegistration.cs:135-137`. `Shared/Events/MemberAdded.cs:22-78` repeats the
  same three "latest row" queries and the same comparisons.
- **Now:** three `IEventHandler<GuildMemberUpdatedEventArgs>`, each run on **every** member update (role changes,
  timeouts, boosts, pending flag). Each one does its own `IsModuleEnabled`, `CreateDbContextAsync`, one "latest row"
  query, its own compare and its own `SaveChangesAsync`. The case-insensitive nickname compare is written out twice
  (`UpdatedNicknameEvent.cs:38-42`, `MemberAdded.cs:49-52`), and so are the username and avatar compares. Each handler
  builds its embed inline, so none of the decisions can be tested without the harness.
- **Proposed:** one `UserLogEvents` handler. It makes one module check and one query that reads all three latest values
  as scalar subqueries, then calls a pure `Diff`, makes one `SaveChangesAsync` and sends 0 to 3 logs. `MemberAdded`
  reuses the same query and `Diff`, and keeps only the history rows.
  ```csharp
  internal sealed record LatestUserState(Username? Username, Optional<Nickname?> Nickname, AvatarFileName? Avatar);

  // cs15: closed. net10: abstract with a private constructor (T-3's interim shape)
  internal abstract record UserChange
  {
      public sealed record UsernameChanged(Username Before, Username After) : UserChange;
      public sealed record NicknameChanged(Nickname? Before, Nickname? After) : UserChange;
      public sealed record AvatarChanged(AvatarFileName Before, AvatarFileName After) : UserChange;
  }

  internal static IEnumerable<UserChange> Diff(LatestUserState stored, DiscordMember after) { ... } // pure
  private static object ToHistoryRow(UserChange change, UserId user, GuildId guild, DateTimeOffset now) => change switch { ... };
  private static GuildLogMessageBase? ToLog(UserChange change, DiscordMember member, DateTimeOffset now) => change switch { ... };
  ```
  "No row yet" (L-6, KI-40) stays a `null`/missing case in `LatestUserState`, so `Diff` returns nothing for it, as each
  handler does now. The avatar log still has to download the image, so it goes through LOG-2's gate.
- **Size:** three files of 213 lines become one of about 120 (−90). `MemberAdded` loses about 25 more. Two
  registrations go. `Diff` gets unit tests that need no harness.
- **Needs:** nothing. The `closed` modifier needs `net11.0`, and with it the `ToHistoryRow`/`ToLog` switches have no
  `_` arm.
- **Interacts with:** L-4, L-6, D-7, KI-40, KI-49 (one `GetAvatarFileName` form, unchanged), KI-68 (unchanged: the
  username row is still per guild event), LOG-2. Section 6 owns `MemberAdded`, so this finding counts its share.

#### LOG-2 Gate log building in `GuildLog`, not in each handler  `perf` `remove` · **med**
- **Where:** `SettingsModule.IsLogChannelSet` calls at `DeleteMessageEvent.cs:136`, `BulkMessageDeletedEvent.cs:74` and
  `UpdatedAvatarEvent.cs:54` (the L-7 fix). It is missing at `UpdateMessageEvent.cs:79`, where `GetUserAvatar` is a
  member lookup and possibly a REST user fetch on every logged edit, even with the edit log off.
  `GuildLog.SendLogMessageAsync` is at `Shared/Channels/GuildLog/GuildLog.cs:32`.
- **Now:** `GuildLog` takes a finished message. Each handler has to remember to ask first whether anyone will see it.
  Three do, and the edit log doesn't. Every future log has the same trap.
- **Proposed:** an overload that checks the setting (a cache hit) before running the builder on the caller's task:
  ```csharp
  public async ValueTask SendLogMessageAsync(GuildId guildId, GuildLogType type,
      Func<ValueTask<DiscordMessageBuilder>> build, CancellationToken ct = default)
  {
      if (await settingsModule.IsLogChannelSet(type, guildId, ct))
          await this.SendLogMessageAsync(new GuildLogMessageCustomMessage { GuildId = guildId, GuildLogType = type,
              Message = await build() }, ct);
  }
  ```
  Handlers write the history row first, as now, then call this overload. The publisher keeps its own check, because
  the setting can change while the message is queued. Don't build in the publisher itself: it has a single reader, so
  avatar and image downloads would hold up the whole queue.
- **Size:** about +12 lines in `GuildLog`, about −9 lines at the three sites, and the edit log gets the gate. `GuildLog`
  gains a `SettingsModule` dependency. 4 files.
- **Needs:** nothing.
- **Interacts with:** L-7, I-7, LOG-1, LOG-5. Section 6 owns `GuildLog`, so this is the consumer's side of that change.
  `GuildMemberAddedEvent` should **not** skip its invite fetch when the join log is off, because `InviteService` needs
  every join to keep its snapshot right (LOG-8).

#### LOG-3 One nullable `ProxyAuthor` instead of three nullable fields  `state` · **low**
- **Where:** `DeleteMessageEvent.Response` (`DeleteMessageEvent.cs:189-198`, plus the projection at `:63-65`), the
  anonymous projection at `UpdateMessageEvent.cs:51-53`, and
  `MessageLogEmbedExtensions.AddMessageAuthorFields`/`OrPrivate` (`MessageLogEmbedExtensions.cs:18-27`).
- **Now:** `OriginalUserId`, `SystemId` and `MemberId` are three nullables that are only valid together. They come from
  one `ProxiedMessageLink` row whose `SystemId` and `MemberId` are `required` and non-blank (`PluralKitSystemId.Create`
  refuses blanks), and whose FK guarantees the original message. So `OrPrivate`'s "Private" arm can't run.
  `AddMessageAuthorFields` takes four parameters, and nothing stops a caller from passing a system id with no original
  author.
- **Proposed:** `sealed record ProxyAuthor(UserId OriginalUserId, PluralKitSystemId SystemId, PluralKitMemberId MemberId)`.
  Project it as `Proxy = m.ProxiedMessageLink == null ? null : new ProxyAuthor(...)`, and change the signature to
  `AddMessageAuthorFields(UserId author, ProxyAuthor? proxy)`. Delete `OrPrivate`. `Response` becomes a positional
  private record with `required` members. Today `UserId` defaults to 0 if the projection misses it. Its `AttachmentDto`
  (`Id` is never read) becomes `AttachmentUrl[]`, as in `BulkMessageDeletedEvent`, and
  `Shared/SharedDtos/AttachmentDto.cs` goes.
- **Size:** about −20 lines, 4 files.
- **Needs:** nothing. See the questions if PluralKit can leave out the ids.
- **Interacts with:** L-3, L-12 (`AddMessageAuthorFields` was its fix), LOG-4.

#### LOG-4 PluralKit hands back the typed link  `state` `pipe` · **low**
- **Where:** `DeleteMessageEvent.cs:74-93` (an eight-clause `&&` chain of `ulong.TryParse`, null checks and
  `is Validation<…>.Valid(var …)`), `Shared/PluralKit/Models/PluralKitMessage.cs` (all `string?`), and
  `IPluralkitService.GetProxiedMessageInformation`, whose only caller is this one.
- **Now:** the delete handler parses PluralKit's wire format itself. It turns strings into ids and validates the system
  and member ids, then builds a `ProxiedMessageLink`. A response that fails any clause silently falls through to being
  logged as an ordinary delete.
- **Proposed:** the service returns `ProxiedMessageLink?`. A `PluralKitMessage.ToLink()` does the parsing once at the
  edge, with D-6's trusted factory, since PluralKit's ids are trusted input as Discord's are. The handler becomes
  `if (await pk.GetProxyLinkAsync(id, created) is { } link && link.ProxyMessageId != args.Message.GetMessageId())`.
- **Size:** about −8 lines in the handler, about ±0 in the service. 3 files.
- **Needs:** nothing. F-2 would rename the `Validation` patterns anyway.
- **Interacts with:** D-6, F-2, KI-101. Section 6 owns `PluralkitService`.

#### LOG-5 Delete and edit embeds as pure functions; fetch in parallel  `pipe` `perf` · **low**
- **Where:** `DeleteMessageEvent.BuildLogMessage` (`:147-181`) and `UpdateMessageEvent.cs:79-105`.
- **Now:** both mix I/O (avatar lookup, image download) into embed building. The edit log's Before/After split, which
  L-5 had to fix, can only be tested through the fake gateway. `message.Content.ToString()?.Length` (`:95`) calls
  `?.` on a `string` that is never null, and `Nullable<T>.ToString()` gives `""`. In the delete log, the avatar lookup
  and `BuildImageEmbedAsync`'s downloads are independent but run one after the other.
- **Proposed:** `static DiscordEmbedBuilder DeleteLogEmbed(StoredMessage, ChannelInfo, DiscordUser? deletedBy, string? avatar)`
  and `static IReadOnlyList<DiscordEmbed> EditLogEmbeds(MessageContent? before, string after, …, string? avatar)`. The
  handler fetches and then calls the pure builders. In the delete log, start the avatar lookup and the image downloads
  together. That needs `BuildImageEmbedAsync` to take the embed later, or to return the files separately (section 6).
  Unit-test the 5,000-character split directly.
- **Size:** about ±0 lines. Adds unit tests for the L-5 split. 2 files, or 3 with the image service.
- **Needs:** LOG-2, which handles the gate.
- **Interacts with:** L-5, L-11 (avatar optional, original author), I-6.

#### LOG-6 Cleanup: classify each delete, and stop swallowing outages  `pipe` `state` · **med**
- **Where:** `LogCleanup/CleanupLogsBackgroundTask.cs:68-85` (`DeleteMessageAsync`), `:87-117`
  (`UpdateOldGrimoireLogEntries`) and `:160-164` (`DeleteMessageResult`). It runs every minute.
- **Now:** each old post costs a `GetMessageOrDefaultAsync` and then a `DeleteAsync`. Every outcome, including a 5xx, a
  timeout or the shutdown token, is caught by `catch (Exception)` and becomes `WasSuccessful = false`. After three such
  ticks the row is deleted. So a three-minute Discord or network outage makes the bot give up on every post due in that
  window, and logs of deleted messages stay in the log channel for good. No alert is raised. An already-gone post, or a
  channel that no longer exists, takes three ticks to forget, although `GetMessageOrDefaultAsync` already says
  NotFound for certain. The `bool WasSuccessful` struct is a flag picking a mode.
- **Proposed:** three outcomes, chosen from the exception type:
  ```csharp
  private enum Outcome { Deleted, Gone, Refused } // Gone: NotFound (message or channel). Refused: UnauthorizedException.
  ```
  `Deleted` and `Gone` rows are deleted, and `Refused` rows get `TimesTried + 1` (existing behaviour). Anything else
  escapes to the tick, which logs it and alerts (T-5), so the rows stay for the next tick. Run each channel through
  `ProcessItemAsync($"channel {id}", …)` so one channel's outage doesn't stop the others. That keeps
  `ADiscordError_OnOnePost_DoesNotStopTheOthers` valid. If DSharpPlus can delete by id without the GET (spike:
  `DiscordChannel.DeleteMessageAsync` only reads `message.Id`, but `DiscordMessage` can't be built outside the library),
  the REST calls halve.
- **Size:** about −20 lines. `DeleteMessageResult` goes, and the two lists become one `ToLookup(outcome)`. 1 file.
- **Needs:** F-9's `ProcessItemAsync` to take a result (optional). A DSharpPlus spike for the delete-by-id half.
- **Interacts with:** F-9, T-5, L-2 (the 30/31-day relationship). Two tests pin the retry-for-gone behaviour:
  `LogCleanupTests.APostThatCannotBeDeleted_IsRetried_ThenForgottenOnTheThirdTry` and
  `APostInADeletedChannel_CountsAsAFailedTry` (see Questions).

#### LOG-7 Cleanup: bound each tick and record as it goes  `perf` `state` · **low**
- **Where:** `CleanupLogsBackgroundTask.RunTask` (`:29-66`). The retention dates are at `:33` (`DateTime.UtcNow - 30d`)
  and `:122` (`DateTimeOffset.UtcNow - 31d`).
- **Now:** one tick buffers every due post in every channel (`ToArrayAsync`), deletes them one by one, and writes the
  results only at the end. After downtime or on first deploy, that can be thousands of sequential REST calls in one
  tick. A shutdown or exception partway through records none of the deletes already done, so the next tick re-fetches
  posts that are gone. The two retention periods are separate literals, although L-2 depends on the second being longer
  than the first. They read the wall clock, while the history trim uses the injected `TimeProvider`.
- **Proposed:** `.OrderBy(p => p.CreatedAt).Take(500)` per tick. Write each channel's outcomes as soon as that channel
  is done (this falls out of LOG-6's per-channel item). Run channels with `Task.WhenAll`, since Discord's delete limit is
  per channel. Add `LogPostRetention = 30d` and `MessageRetention = LogPostRetention + 1d` as named constants, and read
  `timeProvider` for both.
- **Size:** about ±0 lines. 1 file. Tests can then move the clock instead of back-dating rows.
- **Needs:** LOG-6.
- **Interacts with:** L-10, L-13 (the `GroupBy` key, already done).

#### LOG-8 `InviteService`: replace the snapshot, pure diff, no DTO, no interface  `state` `remove` · **med**
- **Where:** `UserLogging/InviteService.cs:12-80`, `Extensions/DiscordGuildExtensions.GetGuildInvitesAsync` (`:23-31`),
  and the `?? new GuildInviteDto { … }` fallbacks at `Shared/Events/GuildAdded.cs:36-37` and `UpdateAllGuilds.cs:41-42`.
  It runs on every join.
- **Now:** `CalculateInviteUsed` diffs Discord's fresh list against the stored one, then writes back **only** the one
  invite it picked. Every other drift stays (a missed InviteCreate, uses from a join that fell in the KI-52 window).
  An untracked guild throws "Could not find guild." on every join until a restart (KI-89's consequence). A guild synced
  without Manage Server is stored as tracked with no invites, which is what causes KI-90. `GuildInviteDto` is a mutable
  `ConcurrentDictionary` inside a record whose `GuildId` can default to 0. `IInviteService` has one implementation and
  no test double (the only reference in tests is a commented-out line in `HostBuilder.cs:49`).
- **Proposed:**
  ```csharp
  public sealed class InviteService
  {
      private readonly ConcurrentDictionary<GuildId, ImmutableDictionary<InviteCode, Invite>> _guilds = new();

      /// <summary>Stores the fresh list and returns the invite it shows was used, if any.</summary>
      public Invite? RecordJoin(GuildId guild, ImmutableDictionary<InviteCode, Invite> current)
      {
          var previous = this._guilds.GetValueOrDefault(guild);
          this._guilds[guild] = current;
          return previous is null ? null : FindUsed(previous, current);
      }

      internal static Invite? FindUsed(IReadOnlyDictionary<InviteCode, Invite> before,
          IReadOnlyDictionary<InviteCode, Invite> after) => /* today's two passes, pure */;

      public void Track(GuildId guild, ImmutableDictionary<InviteCode, Invite> invites) => this._guilds[guild] = invites;
      public void Upsert(GuildId guild, Invite invite) => this._guilds.AddOrUpdate(guild, ...);
      public void Forget(GuildId guild, InviteCode code) => ...;
  }
  ```
  A guild without Manage Server is left **untracked**, not stored empty, so its first join after the permission
  arrives starts tracking and returns null ("Unknown Invite") instead of picking an arbitrary invite. `FindUsed` is unit
  tested, including KI-50's `Uses + 1` fallback.
- **Size:** about −40 lines (80 → about 40), `GuildInviteDto` and the interface go, and the two `?? new GuildInviteDto`
  fallbacks go (−6). 5 files.
- **Needs:** nothing.
- **Interacts with:** KI-89 (joins stop throwing; the startup-sync half stays), KI-90 (the permission half is fixed;
  the missed-event half is narrowed, because drift heals at the next join), KI-98, KI-50, L-11 (an ignored delete
  result), L-12 (the `UpdateAllInvites` bullet). Section 6 owns `InviteEvents`, `GuildAdded` and `UpdateAllGuilds`.
  Their changes are one line each.

#### LOG-9 One log table per module drives Set, View and the reply names  `remove` · **low**
- **Where:** `Settings/LogSettingsCommands.Message.Set.cs:45-57`, `User.Set.cs:49-63`, `Message.View.cs:28-37`,
  `User.View.cs:28-39`.
- **Now:** each module states its logs three times: a `switch` from the command enum to `GuildLogType`, a
  `logType is … ? LogWithFilesPermissions : LogPermissions` choice, and hand-written View labels. Set passes
  `logSetting.ToString()` as the name, which is KI-66's code-name reply. The Views await 3 and 5 lookups one after
  another, each a settings read plus a channel lookup.
- **Proposed:**
  ```csharp
  private sealed record LogKind(GuildLogType Type, string Name, bool UploadsFiles);

  private static LogKind Kind(MessageLogSetting setting) => setting switch
  {
      MessageLogSetting.DeleteLog => new(GuildLogType.MessageDeleted, "the delete log", UploadsFiles: true),
      MessageLogSetting.BulkDeleteLog => new(GuildLogType.BulkMessageDeleted, "the bulk delete log", true),
      MessageLogSetting.EditLog => new(GuildLogType.MessageEdited, "the edit log", false),
      _ => throw new InvariantViolationException("Invalid message log setting."), // enum guard, kept (T-4)
  };
  ```
  Set reads `Type`, `Name` and `UploadsFiles` from it. View becomes one shared
  `DescribeLogsAsync(IEnumerable<LogKind>)` over `Enum.GetValues<…>().Select(Kind)` with `Task.WhenAll`.
- **Size:** about −30 lines across 4 files, on top of the F-5 delta. KI-66's replies change, and the tests that pin
  "Updated DeleteLog to …" change with them.
- **Needs:** nothing.
- **Interacts with:** KI-65, KI-66, L-12, I-19.

#### LOG-10 `MessageHistoryEntry.GuildId` is written and never read  `remove` `state` · **low**
- **Where:** `Grimoire.Domain/MessageHistory.cs:14` (`MessageHistoryEntry.GuildId`). It is written at
  `AddMessageEvent.cs:67`, `UpdateMessageEvent.cs:65`, `DeleteMessageEvent.cs:123,130` and
  `BulkMessageDeletedEvent.cs:70`. The model snapshot shows the column with no index.
- **Now:** every history row stores its message's guild again. No query filters or projects on it, because every read
  goes through `Message`. Nothing keeps the two copies equal.
- **Proposed:** drop the property and the column. The guild is `entry.Message.GuildId`.
- **Size:** −1 property, −5 assignments, a migration that drops one column.
- **Needs:** a migration (with the other new schema work, per project memory).
- **Interacts with:** section 2 or 3 may report it too, so the roll-up should de-duplicate. `Reaction.GuildId` is the
  same shape, but see Questions.

#### LOG-11 Join log: invite text as one expression  `pipe` · **optional**
- **Where:** `GuildMemberAddedEvent.cs:31-38` and `:45-53`.
- **Now:** the invite text uses a mutable `string?` and an `if/else if/else` chain. "Created in the last 7 days" is
  computed twice, once for the colour and once for the extra field.
- **Proposed:** `var inviteText = (inviteUsed, args.Guild.VanityUrlCode) switch { ({ } i, _) => …, (null, { Length: > 0 }) => "Vanity Invite", _ => "Unknown Invite" };`
  and one `var isNewAccount`.
- **Size:** about −6 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** KI-67 (the vanity wording stays as decided there).

#### LOG-12 Logging and rethrowing what the boundary already logs  `remove` · **low**
- **Where:** `DeleteMessageEvent.cs:94-110` (three catches) with `:183-187` (two log methods), and
  `AddMessageEvent.cs:85-89` with `:95-97` (`LogOriginalMessageForDebugging`).
- **Now:** the delete handler logs a duplicate or FK-violating proxy link at **Error**, although both are expected
  races (replay, or purge in between), and then logs any other exception before rethrowing it, so
  `IClientErrorHandler` logs and alerts it a second time. The add handler writes the whole message content to the log
  at Error before rethrowing, which puts deleted members' text into log files.
- **Proposed:** `catch (Exception ex) when (ex is UniqueConstraintException or ReferenceConstraintException)` logged at
  Debug, as `ReactionEvents.SaveReactionAsync` does. Drop the log-and-rethrow arms and their `LoggerMessage` methods.
  If the content really is needed for debugging, log its length, not its text.
- **Size:** about −14 lines, 2 files.
- **Needs:** nothing.
- **Interacts with:** L-11 (the AddMessage rethrow, kept).

## Questions for the owner

1. **Case-only changes.** `UpdatedNicknameEvent` (`:41`), `MemberAdded` (`:48-51`) and `UpdateMessageEvent` (`:58`)
   compare with `CurrentCultureIgnoreCase`. So a nickname changed from "bob" to "BOB", or an edit that only changes
   case, is neither recorded nor logged. Usernames are lowercase on Discord now, so the username compare is harmless.
   Is that intended for nicknames and edits? With ordinal compares, LOG-1's `Diff` becomes plain `!=`.
2. **History while User Log is off.** The three update handlers write history only while User Log is on, but
   `MemberAdded` and the startup backfill write it regardless. So `/UserInfo`, the bulk-delete dump's names and a
   re-enabled log can show stale "Before" values. With LOG-1, should history always be recorded, with only the logs
   gated?
3. **PluralKit private systems (LOG-3/4).** If PluralKit's message endpoint can leave out `system` or `member`, the link
   isn't saved today. PluralKit's own delete of the original is then logged as an ordinary delete, and `OrPrivate` was
   presumably meant for this case. Have you seen that? If it happens, the ids should become optional on
   `ProxiedMessageLink` (with a migration) rather than `OrPrivate` being deleted.
4. **Gone log posts (LOG-6).** OK to forget a post that Discord says is already gone, or whose channel is gone, straight
   away instead of after three ticks? Two `LogCleanupTests` pin the current behaviour.
5. **Avatar thumbnails from history.** The delete and edit logs look up the author's avatar through the cache or REST
   (`DiscordClientExtensions.GetUserAvatar`). The `Avatars` table already holds the latest URL per member, so the
   message query could project it, as L-9 did for usernames. It is only current while User Log is on (see question 2),
   and old CDN hashes may stop loading. Worth doing once question 2 is settled? Section 5 owns the call's cost.
6. **`Reaction.GuildId`.** It has the same write-only shape as LOG-10. Keep it for the reaction features you plan, or
   drop it along with LOG-10?
7. **Where LOG-2 lives.** The gate goes in `GuildLog`, which is section 6's file and adds a `SettingsModule` dependency
   to the queue. Fine, or would you rather have a small `LogGate` helper in `Features/Logging`?
