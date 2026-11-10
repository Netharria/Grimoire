# Section 6: Shared infrastructure

Reviewed 2026-11-09, against the code as it will be once every fix in `review-decisions.md` is in.

The biggest win is SH-1. `Program.cs` adds `appsettings.json` a second time, after the environment variables, so the
file overrides every environment variable. Because the tracked file has an empty `token`, the docker-compose setup as
documented can't log in. Deleting the block (−9 lines) fixes it. Next is SH-2: the PluralKit limiter refuses a third
call in a second instead of queueing it, so on busy servers PluralKit's own deletes are logged as ordinary deletes.
Third is SH-3: one `OrNullIfNotFound` helper replaces six copies of the same try/catch, and nine overloads that nothing
calls go with it (about −55 lines). The rest are local cleanups: interfaces with one implementation, wall-clock reads
that skip the injected `TimeProvider`, and duplicated sync and alert code. That's 17 findings: 1 high, 1 med, 14 low,
1 optional. The net change is about −190 lines.

Already covered elsewhere, so not repeated here: `CommandContextExtension`, `GenericBackgroundService` and
`CommandHandler.SendOrEditMessageAsync` (F-5, F-8, F-12, F-13). The typed PluralKit result is LOG-4. The `GuildLog` gate
is LOG-2. The `MemberAdded` diff is LOG-1, and the invite DTO and service are LOG-8. The audit-log cache race and the
FusionCache defaults are SET-8 and SET-10. `DurationType` is MOD-5, `OverwriteChannelResult` is MOD-13, and the lock
permission parse is MOD-14. KI-89, KI-91 to KI-101 and KI-106 are open known issues, and none of them is re-reported.

## Findings

#### SH-1 `Program.cs` re-adds `appsettings.json` above the environment variables  `remove` · **high**
- **Where:** `Grimoire/Program.cs:15-22` (`ConfigureAppConfiguration`). Also `Grimoire/appsettings.json`, which is
  tracked and copied to the output (`Grimoire.csproj:45-47`), and `docker-compose.yml:10-20`.
- **Now:** `Host.CreateDefaultBuilder` already loads `appsettings.json` (optional, reload on change), then the
  environment variables, then the command line. The extra `ConfigureAppConfiguration` builds a second configuration
  from the same file and chains it last, so it has the highest precedence. Every key in the file therefore beats
  `TOKEN`, `CONNECTIONSTRINGS__GRIMOIRE`, `CHANNELID` and the rest. The tracked file has `"token": ""`,
  `"channelId": ""` and a non-empty local connection string. So a container configured the way `docker-compose.yml`
  shows gets an empty token and a localhost database, and the `ConnectionProperties` fallback in `AddGrimoire` never
  runs. CLAUDE.md's "Environment variables use `__`" is only true for keys the file leaves out. The block also makes the
  file required and opens a second file watcher.
- **Proposed:** delete the `ConfigureAppConfiguration` call. That leaves
  `Host.CreateDefaultBuilder(args).ConfigureServices(...).RunConsoleAsync()`. If a missing file should still stop
  startup, make that check explicit in `AddGrimoire`, where `token` is already read. Replace the tracked file with
  `appsettings.example.json`, which also settles half of I-18 (see Questions).
- **Size:** about −9 lines, 1 file. Optionally move the file: +1 −1 in the csproj and `.gitignore`.
- **Needs:** nothing. Check first how production supplies its configuration, because this changes which value wins
  when both a variable and a file key are set.
- **Interacts with:** I-18 (the missing `appsettings.example.json`), KI-101.

#### SH-2 The PluralKit limiter refuses calls instead of queueing them  `perf` `state` · **med**
- **Where:** `GrimoireServiceRegistration.AddGrimoire` (`GrimoireServiceRegistration.cs:72-81`, `QueueLimit = 0`, and the
  pipeline at `:252-258`). The caller is `DeleteMessageEvent.cs:74-75`, which runs on every delete of a logged message
  under 60 seconds old.
- **Now:** a sliding window with `SegmentsPerWindow = 1`, which is a fixed window, of 2 permits a second and no queue.
  After I-3, a rejected call returns null, and the delete is logged as an ordinary one. PluralKit deletes every original
  it proxies, so in a server with more than two proxied messages a second, the extra originals show up in the delete
  log as if a member deleted them. A rejected lookup also skips the `ProxiedMessageLink` row, so the edit and delete
  logs for the proxy show the webhook instead of the member. The work saved is one HTTP call, and the user-visible
  cost is a false log entry.
- **Proposed:** let a burst wait a little instead of failing:
  ```csharp
  var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
  {
      TokenLimit = 2, TokensPerPeriod = 2, ReplenishmentPeriod = TimeSpan.FromSeconds(1),
      QueueLimit = 20, QueueProcessingOrder = QueueProcessingOrder.OldestFirst, AutoReplenishment = true
  });
  ```
  With 20 queued calls, a call waits at most about 10 seconds, which is well inside the 60-second window the service
  allows. The standard handler's 30-second total timeout still bounds it. Also, `Name = $"{nameof(RateLimiter)}"` can
  just be `"RateLimiter"`. Check PluralKit's published limit before choosing the numbers.
- **Size:** about ±0 lines, 1 file.
- **Needs:** nothing. This is an owner decision about how long a delete handler may wait (see Questions).
- **Interacts with:** I-3 (keeps its catch, which now runs only on real outages), L-8, LOG-4.

#### SH-3 One `OrNullIfNotFound` instead of six try/catch copies, and the unused overloads go  `remove` · **low**
- **Where:** `DiscordClientExtensions.cs:21-85`, `DiscordGuildExtensions.cs:41-92` and `DiscordChannelExtensions.cs:54-70`.
- **Now:** `GetChannelOrDefaultAsync` (client and guild), `GetUserOrDefaultAsync`, `GetGuildOrDefaultAsync`,
  `GetRoleOrDefaultAsync`, `GetMemberOrDefaultAsync` and `GetMessageOrDefaultAsync` each repeat the same
  `try { return await …; } catch (NotFoundException) { return null; }`, and each has a twin that takes a nullable id.
  Only two of the nullable twins have a caller: `client.GetChannelOrDefaultAsync(ChannelId?)` from
  `AlertChannels.GetAsync`, and `guild.GetRoleOrDefaultAsync(RoleId?)` from `MuteAdminCommands.View`. These have no
  callers at all: both `GetGuildOrDefaultAsync` overloads, `GetUserOrDefaultAsync(UserId?)`, `GetUserAvatar(UserId?)`,
  `guild.GetChannelOrDefaultAsync(ChannelId?)`, `GetMemberOrDefaultAsync(UserId?)` and
  `GetMessageOrDefaultAsync(MessageId?)`.
- **Proposed:**
  ```csharp
  public static class DiscordNotFound
  {
      extension<T>(Task<T> call) where T : class
      {
          /// <summary>Null only when Discord says it doesn't exist. Any other failure, an outage included, throws.</summary>
          public async Task<T?> OrNullIfNotFound()
          {
              try { return await call; }
              catch (NotFoundException) { return null; }
          }
      }
  }

  public Task<DiscordRole?> GetRoleOrDefaultAsync(RoleId roleId) => guild.GetRoleAsync(roleId.Value).OrNullIfNotFound();
  ```
  Keep the two nullable twins that have callers, or write the `id is { } x ? … : null` at those two sites. Each lookup
  then has one doc comment, in one place.
- **Size:** about +12 −65 lines, 4 files.
- **Needs:** nothing.
- **Interacts with:** KI-19 (the "NotFound only" rule, which now lives in one place).

#### SH-4 `GetUserAvatar` makes a second lookup when the member has no guild avatar  `perf` · **low**
- **Where:** `DiscordClientExtensions.GetUserAvatar` (`DiscordClientExtensions.cs:72-85`). It runs on every delete and edit
  log (`DeleteMessageEvent.cs:160`, `UpdateMessageEvent.cs:79`).
- **Now:** the member is fetched, and if they have no guild avatar, the user is fetched too, just for the global avatar.
  `DiscordMember.DisplayAvatarUrl` already falls back to it, and `GetAvatarFileName` relies on that.
- **Proposed:** `(await guild.GetMemberOrDefaultAsync(id))?.DisplayAvatarUrl ?? (await client.GetUserOrDefaultAsync(id))?.AvatarUrl`.
  The user fetch is then only for authors who left. Either form can be a REST call on a cache miss.
- **Size:** about −6 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** LOG-2 and LOG-5, and LOG's question 5 (taking the avatar from history instead, which would remove
  the method).

#### SH-5 The audit-log parser: a name that says it claims, and a moderator instead of an entry  `pipe` `state` · **low**
- **Where:** `DiscordAuditLogParserService.ParseAuditLogForDeletedMessageAsync` (`Utilities/DiscordAuditLogParserService.cs:31-80`).
  The only caller is `DeleteMessageEvent.cs:115-120, 166-167`.
- **Now:** it's called "Parse", but it does REST calls, reports guild issues and writes the cache. A second call for the
  same delete returns null, because the first one used up the count. It returns DSharpPlus's
  `DiscordAuditLogMessageEntry`, but the caller reads only `UserResponsible`. `catch (Exception) { return null; }`
  (`:59`) hides every outage with no log at all, which is the swallowing F-1 warns about. The cutoff reads
  `DateTime.UtcNow` (`:65`).
- **Proposed:** `Task<DiscordUser?> ClaimDeletingModeratorAsync(GuildId, ChannelId, UserId author)`, with a doc comment
  saying it can be called once per delete. Replace the blanket catch with a logged `catch (DiscordException)`. The
  attribution is optional, so it still returns null. Read the time from `TimeProvider` (SH-7). The cache half is SET-8.
- **Size:** about ±0 lines, 2 files.
- **Needs:** nothing.
- **Interacts with:** SET-8 (atomic claim), KI-64, F-1, SH-6 (the interface).

#### SH-6 Interfaces with one implementation, lifetimes, and a redundant registration  `remove` · **low**
- **Where:** `IPluralkitService` (`PluralkitService.cs:18`), `IDiscordImageEmbedService` (`DiscordImageEmbedService.cs:14`),
  `IDiscordAuditLogParserService` (`DiscordAuditLogParserService.cs:15`) and `IAlertSender` (`Alerts/IAlertSender.cs`),
  with its forwarding registration at `GrimoireServiceRegistration.cs:118`. Also the scoped registrations at `:107-108`,
  and `services.AddHttpClient()` at `:233`.
- **Now:** each interface has one implementation, and no test substitutes it. The integration tests fake the wire
  (HttpClients and the gateway), and `HostBuilder.cs:43-45` only mentions them in a commented-out block.
  `AlertTests` resolves `IAlertSender`, but it could resolve `AlertService`. The two scoped services hold no state.
  DSharpPlus makes a scope per event, so `DiscordImageEmbedService` re-reads and splits `validImageExtensions`, and
  calls `CreateClient`, on every delete-log and avatar event. This is the same reason `PluralkitService` was made a
  singleton (I-15). `AddHttpClient("Default")` already registers the factory, so the bare `AddHttpClient()` does nothing.
- **Proposed:** delete the four interfaces and register the classes as singletons. `DiscordImageEmbedService` then calls
  `CreateClient("Default")` per download instead of keeping the client in a field, so handler rotation still works.
  The validated extension set becomes a field (SH-17). Delete `services.AddHttpClient()`. `IInviteService` is LOG-8's.
  `AlertService` keeps its two registrations, as a singleton and as a hosted service.
- **Size:** about −30 lines, 9 files (5 sources, the registration, 3 consumers), plus one test line.
- **Needs:** nothing.
- **Interacts with:** I-15, LOG-8.

#### SH-7 Shared helpers read the wall clock, though a `TimeProvider` is registered  `state` · **low**
- **Where:** `GetRecentAuditLogAsync` (`DiscordGuildExtensions.cs:33-39`, with `int allowedTimeSpan` in milliseconds),
  `DiscordAuditLogParserService.cs:65`, `PluralkitService.cs:51`, `PurgeMessagesAsync` (`DiscordChannelExtensions.cs:26`),
  `GuildLogMessage.GetMessageBuilder` (`GuildLogMessage.cs:33`), `GuildLogPublisher.ScheduleMessagePurge` (`:138`),
  `InviteEvents.cs:38`, `CommandHandler.GetElapsedMilliseconds` (`:198`) and `CommandContextExtension.BuildReplyEmbed`
  (`:147`). `TimeProvider.System` is registered at `GrimoireServiceRegistration.cs:112`.
- **Now:** the alert, issue, gateway and backoff code takes `TimeProvider`, and the integration harness can swap it
  (`configureHost`). The helpers every feature uses read `DateTime.UtcNow` or `DateTimeOffset.UtcNow` instead, so a test
  with a manual clock gets two clocks. `GetRecentAuditLogAsync`'s window is a bare `int` of milliseconds.
- **Proposed:** pure helpers take the time as an argument, and services take `TimeProvider`:
  `GetRecentAuditLogAsync<T>(DateTimeOffset now, TimeSpan maxAge, …)`, `PurgeMessagesAsync(…, DateTimeOffset now)`, and
  `IPluralkitService`/the audit-log parser inject `TimeProvider`. `GuildLogMessage` takes its timestamp when it's built
  (SH-10). `DurationType` is MOD-5.
- **Size:** about ±0 lines, 8 files. It changes signatures only.
- **Needs:** nothing.
- **Interacts with:** MOD-5, F-12 (`GenericBackgroundService` builds its `RetryBackoff` with `TimeProvider.System`, which
  is the same gap), LOG-6's retention dates.

#### SH-8 Mentions as C# 14 extension properties on the id types  `remove` · **optional**
- **Where:** `ChannelExtensions.Mention(ChannelId?)` and `Mention(ulong?)` (`ChannelExtensions.cs:13-22`),
  `UserExtensions.Mention(UserId?)`/`(ModeratorId?)`/`(ModerationActor)` (`UserExtensions.cs:12-28`) and
  `RoleExtensions.Mention(RoleId)`. There are about 20 hand-written `$"<@{…}>"`/`<#…>`/`<@&…>` strings, in
  `IgnoreCommandGroup`, `DiagnosticsCommands`, `UserInfoCommands` and `GuildIssueCatalog`.
- **Now:** the helpers are static methods called as `UserExtensions.Mention(id)`. Every `ChannelExtensions.Mention`
  caller checks for null first and shows "None", so the "Unknown Channel" arm never runs, and the `ulong?` overload
  exists only for a `.Value` caller. Half the code doesn't use the helpers at all.
- **Proposed:** `extension(UserId id) { public string Mention => $"<@{id}>"; }`, and likewise for `ModeratorId`, `RoleId`,
  `ChannelId` and `ModerationActor`. Callers write `x.UserId.Mention`, or `id?.Mention ?? "None"`. Delete the nullable
  and `ulong` overloads and the three static classes. With cs15 `closed`, `ModerationActor.Mention` loses its `_` arm
  (T-4).
- **Size:** about −12 lines in `Extensions`. The ~35 call sites keep their line count and get shorter. About 15 files.
- **Needs:** nothing (C# 14).
- **Interacts with:** T-4 (the `ModerationActor` arm), LVL's `RoleExtensions.Mention` use.

#### SH-9 `AddMessageTextToFields`: a pure splitter with no regex and no static logger  `remove` `pipe` · **low**
- **Where:** `DiscordEmbedBuilderExtensions.AddMessageTextToFields` (`DiscordEmbedBuilderExtensions.cs:20-52`). It has 9
  callers, which build the delete, edit, bulk-delete and custom-command logs.
- **Now:** a 1-second-timeout regex splits the text at whitespace. When it skips characters, as happens on a run of more
  than 1,024 characters with no space, such as a long URL, it logs through Serilog's static `Log.Logger`, which is a
  side effect that bypasses DI and the test sinks. It then splits again with `Chunk`. A regex timeout would throw and
  lose the log. `bool addBlankField` picks a mode.
- **Proposed:** a pure `static IEnumerable<string> SplitForFields(string text, int max = 1024)` that cuts at the last
  whitespace at or before `max`, or at `max` if there is none, and skips blank pieces. The extension adds the fields
  from it. Callers that want the `` `blank` `` field pass `content ?? ""` through
  `AddMessageTextToFields(type, content)`, and the one caller that doesn't checks `string.IsNullOrWhiteSpace` itself.
  Unit-test the splitter directly.
- **Size:** about +14 −30 lines, 1 file plus the callers that pass `false`.
- **Needs:** nothing.
- **Interacts with:** I-17 (keeps its blank-chunk rule), LOG-5.

#### SH-10 `GuildLog`: three message kinds where one record would do, and a hand-rolled fast path  `remove` `state` · **low**
- **Where:** `GuildLogMessageBase` and its three subclasses (`Channels/GuildLog/GuildLogMessage.cs:12-53`, with 25
  `new GuildLogMessage`, 16 `new GuildLogMessageCustomEmbed` and 3 `new GuildLogMessageCustomMessage`), and
  `GuildLog.SendLogMessageAsync` (`GuildLog.cs:32-41`).
- **Now:** `GuildLogMessage` is an embed with defaults, built lazily in the publisher, so `Timestamp ?? UtcNow` is the
  time it was *posted*, not the time it happened. That can be seconds late when the queue is backed up.
  `Description` is `required` and also has a `= string.Empty` initializer. `SendLogMessageAsync` checks for null on a
  non-nullable parameter (nullable errors are on), and calls `TryWrite` before `WriteAsync`, which `WriteAsync` already
  does internally.
- **Proposed:** one record that carries the finished message, with factories:
  ```csharp
  public sealed record GuildLogMessage(GuildId GuildId, GuildLogType GuildLogType, DiscordMessageBuilder Message)
  {
      public static GuildLogMessage Embed(GuildId guild, GuildLogType type, DiscordEmbed embed) => new(guild, type,
          new DiscordMessageBuilder().AddEmbed(embed));

      public static GuildLogMessage Text(GuildId guild, GuildLogType type, string description, DateTimeOffset at,
          DiscordColor? color = null, string title = "", string footer = "") => Embed(guild, type, new DiscordEmbedBuilder()
          .WithAuthor(title).WithDescription(description).WithFooter(footer).WithTimestamp(at)
          .WithColor(color ?? GrimoireColor.Purple).Build());
  }

  public ValueTask SendLogMessageAsync(GuildLogMessage message, CancellationToken ct = default)
      => this._channel.Writer.WriteAsync(message, ct);
  ```
  T-1's `Reply.Logged` uses `GuildLogMessage.Embed`. If the owner prefers to keep the cases, mark the base `closed`
  under cs15, as T-3 says.
- **Size:** about −25 lines in `Shared`. The 44 call sites keep their line count. About 30 files, mechanically.
- **Needs:** nothing. Do it together with LOG-2, which adds an overload here.
- **Interacts with:** LOG-2, T-1 (`Reply.Logged`), T-3.

#### SH-11 Warnings go through the urgent queue and can crowd it out  `state` `perf` · **low**
- **Where:** `AlertService.Send` and `ProcessAsync` (`Alerts/AlertService.cs:32-36, 57-64`) and `WarningBuffer.Add`
  (`WarningBuffer.cs:19`).
- **Now:** every alert, warning or urgent, goes into one 256-slot queue that drops writes when full. A warning only
  takes the queue to reach `WarningBuffer.Add`, which is already thread-safe and bounded. While the reader is waiting
  on Discord to post an urgent alert, a burst of warnings can fill the queue, for example `CannotReadAuditLog`, which is
  raised on every delete in a guild without View Audit Log. Then the next urgent alert is dropped.
- **Proposed:** `Send` adds warnings to the buffer directly, and only urgent alerts are queued:
  ```csharp
  public void Send(Alert alert)
  {
      var kept = alert.Severity is AlertSeverity.Warning ? warningBuffer.Add(alert) : this._channel.Writer.TryWrite(alert);
      if (!kept) LogAlertDropped(logger, alert.Type);
  }
  ```
  `ProcessAsync` loses its warning arm. A `Warning` that reaches it is a bug, which the enum guard already reports.
- **Size:** about −4 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** KI-91, KI-92 (unchanged), KI-94 (the buffer is now the only place a warning waits).

#### SH-12 Alerts: a one-caller wrapper and the merge rule written twice  `remove` · **low**
- **Where:** `GuildIssueThrottle` (`GuildIssues/GuildIssueThrottle.cs`, whose only user is `GuildIssueReporter`),
  `AlertCooldown` (`Alerts/AlertCooldown.cs`), `PendingWarning.Merge` (`PendingWarning.cs:33-39`) and
  `AlertOccurrenceStore.UpsertAsync` (`AlertOccurrenceStore.cs:23-29`).
- **Now:** `GuildIssueThrottle` adds a lock and a `"{guild}:{code}"` key around `AlertCooldown`, in its own file. The
  rule for combining two occurrences (add the counts, take the min of the first-seen times and the max of the last-seen
  times, union the guilds and cap them at 20) is written once for the in-memory buffer and once for the database row.
- **Proposed:** give `AlertCooldown` its own `Lock`, which costs nothing uncontended in `AlertService`'s single reader,
  and have `GuildIssueReporter` hold an `AlertCooldown` directly. Delete `GuildIssueThrottle`. `UpsertAsync` merges
  through `PendingWarning`: `existing.ToPending().Merge(warning)` and then copies the result back, or `AlertOccurrence`
  gets a `With(PendingWarning)` that calls the same rule.
- **Size:** about −20 lines, 4 files.
- **Needs:** nothing.
- **Interacts with:** KI-96 (unchanged).

#### SH-13 `GuildAdded` and `UpdateAllGuilds` are one sync written twice  `remove` · **low**
- **Where:** `GuildAdded.HandleEventAsync` (`Events/GuildAdded.cs:21-41`) and `UpdateAllGuilds.HandleEventAsync`
  (`Events/UpdateAllGuilds.cs:20-53`). These run at startup and when the bot joins a guild.
- **Now:** both set the status, run the three `AddMissing…HistoryAsync` backfills, fetch the invites with the same
  `?? new GuildInviteDto` fallback, and save if anything changed. One does it for one guild, the other for all guilds.
- **Proposed:** one `internal sealed class GuildSync` handling both events. It has a private
  `Task<bool> BackfillAsync(GrimoireDbContext, DiscordGuild)`, called once or in a loop, and one status line. The invite
  half becomes LOG-8's `Upsert`. Add the `GuildDeleted` arm for KI-100 here if the owner wants it, because it's the same
  status line. `MemberAdded`'s diff is LOG-1's.
- **Size:** about −25 lines, 2 files become 1, and one registration goes.
- **Needs:** nothing. Fix KI-89's ordering (invites can't stop the backfill) in the merged method, rather than twice.
- **Interacts with:** LOG-1, LOG-8, KI-89, KI-97, KI-100.

#### SH-14 `CommandHandler`: a zero guild id, one alert path, and a pure argument formatter  `state` · **low**
- **Where:** `CommandHandler.HandleEventAsync(CommandErroredEventArgs)` (`Features/Shared/CommandHandler.cs:100-101`,
  `new GuildId(args.Context.Guild?.Id ?? 0)`), `BuildCommandLogAsync` (`:58-64`), `SendErrorAlert`/`SendWarning`/
  `HandleGatewayError` (`:39-92`, which find `IAlertSender` three ways), and the `//todo` at `:25`.
- **Now:** outside a guild, the "missing permissions" issue is built with guild id 0, only to borrow its text for the
  reply. `BuildCommandLogAsync` isn't async. It mutates a `StringBuilder` it's handed, and runs on every command even
  when nothing logs at Information.
- **Proposed:** `GuildIssueCatalog.BotMissingPermissions` takes a real `GuildId`. The reply text comes from a static
  `MissingPermissionText(command)` that the catalog also uses. Replace `BuildCommandLogAsync` with
  `static string FormatArguments(IReadOnlyDictionary<CommandParameter, object?> args) => string.Concat(args.Select(a => $" {a.Key.Name} '{a.Value}'"))`.
  Make one private `Alert(IServiceProvider, Alert)` the only way to resolve the sender. Delete the `//todo`, because the
  lazy resolve is the answer to it.
- **Size:** about −12 lines, 2 files.
- **Needs:** nothing.
- **Interacts with:** F-8 (same file; the reply path is F-8's), S-12/D-5 (no zero ids).

#### SH-15 One retry policy for every Discord call, idempotent or not  `state` · **low**
- **Where:** `DiscordRetryPolicy` (`Utilities/DiscordRetryPolicy.cs:16-24`: 4 retries, 3 s exponential base, jitter).
  It's used by `CommandContextExtension.ReplyAsync`/`FollowUpAsync` (`:34-46`), `GuildLogPublisher.cs:62` and
  `MuteAdminCommands.cs:74`.
- **Now:** a 5xx is retried for up to about 45 seconds wherever the policy is used. That's right for reads and edits.
  It isn't right in two places. An initial `RespondAsync` must happen within 3 seconds of the interaction, and the first
  retry waits about 3 seconds, so it almost always fails with Unknown Interaction. A `SendMessageAsync` that returns a
  502 after Discord already created the message is sent again, which posts a duplicate log or follow-up. The single
  reader in `GuildLogPublisher` also waits out the whole retry, so one bad send holds up every guild's logs for that
  long.
- **Proposed:** two named pipelines. `RetryDiscordRead` keeps today's policy, for edits, deletes, fetches and overwrites.
  `RetryDiscordSend` makes 1 retry after about 1 second, for new messages and follow-ups. Initial responses get no
  retry. That's about 6 lines. The default-value overload stays.
- **Size:** about +6 lines, 3 files.
- **Needs:** an owner decision: is a rare duplicate log worse than a rare lost one?
- **Interacts with:** I-6 and I-7 (the publisher's retry re-uploads files, which this keeps), KI-54.

#### SH-16 Helpers left with a single caller or an unused parameter  `remove` · **low**
- **Where:** `DiscordChannelExtensions.GetMessageAsync(MessageId, bool skipCache)` (`:79-81`, where nothing passes
  `skipCache`; its one caller is `PublishCommands.cs:133`), `BuildChannelTree` (`:42-50`, one caller, which immediately
  calls `.ToDictionary()` at `AddMessageEvent.cs:37`), `DiscordClientExtensions.GetGrimoireModeratorId` (`:87`, one
  file), `DiscordInviteExtensions.GetInviteUrl` (only used inside `ToInvite`), and `DiscordRoleExtensions`, which is the
  last old-style `this` extension next to `extension` blocks.
- **Now:** each one adds a public member for a single use. `BuildChannelTree` returns `KeyValuePair<ChannelId,
  ChannelId?>` pairs that only make sense once they're in a dictionary.
- **Proposed:** drop `skipCache`. Make `BuildChannelTree` return the `IReadOnlyDictionary<ChannelId, ChannelId?>` the
  settings call takes, or let section 4's ignore lookup take the `DiscordChannel` chain. Make `GetInviteUrl` private.
  Move `GetRoleId` into an `extension(DiscordRole role)` block.
- **Size:** about −8 lines, 4 files.
- **Needs:** nothing.
- **Interacts with:** X-12 (the channel tree feeds the thread ignore), MOD-14 (`RevertLockPermissions(long)`).

#### SH-17 `DiscordImageEmbedService`: a set for the extensions, a pooled buffer, and no mode flag  `remove` `cs15` · **low**
- **Where:** the constructor (`Utilities/DiscordImageEmbedService.cs:34-43`), `IsImage` (`:79-80`), `GetImage`'s copy
  loop (`:101-108`), and `bool displayFileNames` (`:45-46`; only `UpdatedAvatarEvent.cs:74` passes `false`).
- **Now:** `IsImage` does a linear `Contains` with a comparer over a list, and runs twice per file (images, then
  non-images). Each download allocates an 80 KB buffer. The flag exists only so the avatar log can hide a meaningless
  file name.
- **Proposed:** net10: `FrozenSet<string>` built once, with `StringComparer.OrdinalIgnoreCase` (singleton, SH-6), and
  `files.ToLookup(IsImage)` to split them in one pass. Copy with `body.CopyToAsync(capped)` into a `MemoryStream` that
  refuses to grow past `MaxFileSize`, or rent the buffer from `ArrayPool<byte>.Shared`. **cs15**: if a `HashSet` is
  preferred over the frozen set, write it as `HashSet<string> set = [with(StringComparer.OrdinalIgnoreCase), .. split];`.
  Replace the flag with an avatar-specific overload,
  `BuildImageEmbedAsync(string url, UserId, DiscordEmbed)`, which can also skip the name lists. LOG-5 wants the files
  returned separately from the embed, so do both in one pass.
- **Size:** about −6 lines, 3 files.
- **Needs:** `net11.0` only for the `[with(...)]` form.
- **Interacts with:** LOG-5, I-6, I-16.

## Line deltas

| Finding | Approx. change |
|---|---|
| SH-1 | −9 |
| SH-2 | ±0 |
| SH-3 | −53 |
| SH-4 | −6 |
| SH-5 | ±0 |
| SH-6 | −30 |
| SH-7 | ±0 |
| SH-8 | −12 |
| SH-9 | −16 |
| SH-10 | −25 |
| SH-11 | −4 |
| SH-12 | −20 |
| SH-13 | −25 |
| SH-14 | −12 |
| SH-15 | +6 |
| SH-16 | −8 |
| SH-17 | −6 |
| **Total** | **about −220 in Shared/Extensions/Utilities, about −190 net once call-site churn is counted** |

## Questions for the owner

1. **SH-1, production configuration.** How does production get its token and connection string: a deployed
   `appsettings.json`, or environment variables? Right now the file wins over any variable. Removing the block makes
   variables win, as CLAUDE.md and docker-compose assume. Is that the intended behaviour?
2. **The tracked `appsettings.json`.** It is tracked in git and holds a local connection string that includes a
   password. Should it become `appsettings.example.json`, with the real file ignored (closing I-18's missing-file half)?
   If that password is used anywhere besides a local dev database, rotate it.
3. **SH-2, how long a delete may wait for PluralKit.** Is up to about 10 seconds acceptable in a burst, in exchange for
   not logging PluralKit's own deletes as ordinary ones? Or would you rather keep failing fast?
4. **SH-15, duplicates against losses.** For new messages (logs, follow-ups), would you rather risk a rare duplicate
   post (today's behaviour) or a rare lost one (a single short retry)?
5. **SH-10, shape of the log message.** One sealed record with factories, or keep the three cases and mark them
   `closed` under cs15 as T-3 suggests? The record removes more code, but it touches about 30 files.
6. **`GuildLogPublisher` has a single sequential reader.** Every guild's logs wait behind one slow send. That's fine for
   one main guild. If more guilds are planned, partitioning by channel would be the next step. It isn't a finding,
   because it adds code. Should it be tracked?
