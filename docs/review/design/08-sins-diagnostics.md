# Section 8: Moderation, sins and diagnostics

Reviewed 2026-11-09, against the code as it will be once every fix in `review-decisions.md` is in (D-2, M-7, M-11, S-12,
X-9 and the `RequiredGuild` ruling included).

The biggest win is one sin projection: SinLog, `/Publish` and `/Publish Unban` each write their own "current reason" and
"pardon" subqueries, and the pardon reaches the code as a `bool`, a nullable moderator and a date that is
`default` when the sin isn't pardoned (SIN-2). Next comes the T-1 shape over the sin commands. The same "Could not find
a sin with that ID." shows yellow, red-public and red-ephemeral in three commands, and the shape plus one `FindSinnerAsync`
saves about 400 lines across the section (SIN-1). After that: SinLog's `Mod` mode should stop being a case of the sin
filter (SIN-3), and the spam tracker should stop depending on FusionCache handing back the same mutable instance
(SIN-11). There are 15 findings: 4 med and 11 low.

## Line deltas against T-1 (F-5)

Measured with the target shape from section 1, plus this section's findings where the row says so. "Today" is the
current file length with header and usings.

| Feature | Files | Today | After | Δ | Also uses |
|---|---|---|---|---|---|
| `/Pardon`, `/Reason`, `/Forget` | `SinAdmin/Commands/{PardonSin,UpdateSinReason,ForgetSin}.cs` | 227 | ~150 | −75 | F-6 (`ModerationReason` parameter), SIN-1's `FindSinnerAsync` |
| `/SinLog` | `SinAdmin/Commands/SinLog.cs` | 157 | ~95 | −60 | SIN-2, SIN-3, SIN-5 |
| `/ModSettings` | `SinAdmin/Commands/ModSettings*.cs` | 160 | ~130 | −30 | F-1 (3 `GetOrElse`) |
| `/Publish` | `PublishSins/*.cs` | 280 | ~200 | −80 | SIN-2, SIN-4 |
| `/Diagnostics` | `Diagnostics/DiagnosticsCommands.cs` | 173 | ~135 | −38 | SIN-9, S-12 |
| `/SpamFilter` | `SpamFilter/Commands/SpamFilterOverrideCommands.cs` | 87 | ~72 | −15 | none |
| `/GeneralSettings`, `/Modules` | `Shared/Commands/{GeneralSettings,Module}Commands*.cs` | 247 | ~195 | −52 | SIN-13, F-1 |
| Channel setters (shared) | `Shared/Commands/ChannelSettingCommand.cs` | 100 | ~80 | −20 | SIN-14; `GetChannelOption` becomes the `RunAsync` input |
| `/Purge` | `Shared/Commands/PurgeCommands.cs` | 80 | ~58 | −22 | `Reply.Logged` replaces `LogPurgeAsync` |
| `/UserInfo` | `Shared/Commands/UserInfoCommands.cs` | 191 | ~160 | −30 | SIN-5, SIN-15, F-1 |
| **Total** | | **1,702** | **~1,275** | **~−420** | |

Counts in scope that the shape and F-1 remove: 8 copies of
`if (ctx is SlashCommandContext s) await s.DeferResponseAsync(true); else await ctx.DeferResponseAsync();`
(`SinLog`, `ModSettings.PublicBanLog`, `ModSettings.View`, `DiagnosticsCommands.CheckAsync`, `GeneralSettingsCommands.View`,
`PurgeCommands` ×2, and the variant in `SinLog` that returns silently for text commands). There are also 17
`GetOrElse(...)` tails (F-1), 9 hand-written reply-then-log pairs, and 5 `SendWarningResponseAsync` calls.

## Findings

#### SIN-1 The sin admin commands, measured against T-1  `pipe` · **med**
- **Where:** `PardonSin.PardonAsync` (`PardonSin.cs:26-82`), `UpdateSinReason.ReasonAsync` (`UpdateSinReason.cs:26-81`),
  `ForgetSin.ForgetAsync` (`ForgetSin.cs:26-61`).
- **Now:** all three look up "this guild's sin and its user" with the same query (`PardonSin.cs:43-46`,
  `UpdateSinReason.cs:44-47`; `ForgetSin.cs:35` loads the whole tracked entity). They report the same miss three ways:
  `/Forget` uses a yellow public `ReplyAsync`, `/Pardon` a red public `ReplyAsync`, and `/Reason`
  `SendErrorResponseAsync`, which is red and ephemeral. Each writes its reply and mod-log pair by hand. `/Pardon`
  opens its `DbContext` before it checks the reason.
- **Proposed:** T-1 with F-6's `ModerationReason` parameter, and one helper beside `GetDisplayNameAsync`:
  ```csharp
  // DatabaseQueryHelpers/SinQueryHelpers.cs
  public static async Task<Result<Sinner>> FindSinnerAsync(this GrimoireDbContext db, SinId sinId, GuildId guildId)
      => await db.Sins.Where(sin => sin.Id == sinId && sin.GuildId == guildId)
              .Select(sin => (UserId?)sin.UserId).FirstOrDefaultAsync() is { } userId
          ? new Sinner(userId, await db.UsernameHistory.GetDisplayNameAsync(userId))
          : new Error.NotFound("Could not find a sin with that ID.");

  public sealed record Sinner(UserId UserId, string DisplayName);
  ```
  Then `/Pardon` is `ctx.RunAsync(() => this.PardonAsync(guild, ctx.User, sinId, reason))`, where the act is
  `FindSinnerAsync(...).Map(sinner => { add pardon; return Reply.Logged(...); })`. `/Forget` becomes `FindSinnerAsync`
  plus `ExecuteDeleteAsync`, so the entity is never tracked. The miss is always `Error.NotFound` (yellow, ephemeral).
- **Size:** about −75 lines over 3 files, +15 for the helper.
- **Needs:** F-3, F-4, F-5. F-6 for the reason.
- **Interacts with:** M-11 (rank 16 shared the display-name half; this is the lookup half of the same bullet), M-7, M-3.
  `SinAdminTests` pins `NotFound` text and the reply's author line, but not the colour.

#### SIN-2 One sin projection, with the pardon as one nullable value  `state` `perf` · **med**
- **Where:** `SinLog.SinLogAsync` (`SinLog.cs:101-127`), `PublishCommands.PublishSinAsync` (`PublishCommands.cs:46-60`),
  `PublishCommands.PublishUnbanAsync` (`PublishCommands.Unban.cs:31-53`).
- **Now:** the "current reason" subquery (`SinReasonHistory … OrderByDescending(SetAt) … FirstOrDefault() ?? string.Empty`)
  is written twice. The pardon is read three times per row in SinLog: `Pardon = Pardons.Any(...)`, `PardonModeratorId`
  (latest row) and `PardonDate` (*earliest* row). That gives three correlated subqueries against `Pardons` for each sin.
  The result has three fields that are only valid together: `PardonDate` is `default(DateTimeOffset)` and
  `PardonModeratorId` is null when `Pardon` is false, and the code relies on checking `x.Pardon` first. Unban writes a
  fourth variant: the latest row's reason, with a nested subquery for the earliest date. Both queries use
  `dbContext.SinReasonHistory`/`dbContext.Pardons` through a captured context (hence the `AccessToDisposedClosure`
  suppressions), not the `Sin.ReasonHistory`/`Sin.Pardons` navigations.
- **Proposed:** one translatable projection, used by all three:
  ```csharp
  internal sealed record PardonSummary(ModeratorId ModeratorId, ModerationReason Reason, DateTimeOffset FirstPardonedAt);
  internal sealed record SinSummary(SinId Id, SinType SinType, DateTimeOffset SinOn, UserId UserId,
      ModerationActor Actor, string Reason, PardonSummary? Pardon);

  internal static readonly Expression<Func<Sin, SinSummary>> ToSummary = sin => new SinSummary(
      sin.Id, sin.SinType, sin.SinOn, sin.UserId, sin.Actor,
      sin.ReasonHistory.OrderByDescending(r => r.SetAt).Select(r => r.Reason.Value).FirstOrDefault() ?? "",
      sin.Pardons.OrderByDescending(p => p.SetAt)
          .Select(p => new PardonSummary(p.ModeratorId, p.Reason,
              sin.Pardons.Min(first => first.SetAt)))
          .FirstOrDefault());
  ```
  SinLog formats `x.Pardon is { } pardon ? … : …`. Unban refuses on `Pardon: null`. Publish reads `Reason`.
  `PublishedMessageId` stays a per-command extra, `.Select(ToSummary)` followed by a second projection or a join on
  `PublishMessages`. The scalar subqueries go from 4 to 2 per SinLog row, and the suppressions go. Whether EF
  translates a positional record constructor in a nested projection needs a quick check. If it doesn't, use
  member-init (`new PardonSummary { … }`) with `required init` properties.
- **Size:** about +20 / −45 lines over 3 files.
- **Needs:** nothing. Answer question 1 first, because it fixes which pardon row each field comes from.
- **Interacts with:** D-2 (the reason history this reads), M-10 (SinLog `OrderBy`, kept), section 3 (query shapes).

#### SIN-3 `/SinLog`: `Mod` is a second command inside the sin filter, and pages are hand-rolled  `pipe` `remove` · **med**
- **Where:** `SinQueryType` (`SinLog.cs:15-23`), `SinLog.SinLogAsync` (`SinLog.cs:61-93`, `:128-155`).
- **Now:** `SinQueryType.Mod` returns moderator statistics, a different query and a different embed, so the method
  handles it first and returns. The filter switch then needs `Mod => throw new InvariantViolationException("Handled
  above")` (T-4's one restructuring) and four copies of `queryable.Where(x => x.SinType == …)`. The list is split into
  2,048-character chunks by a `StringBuilder`/`List<string>`/`ForEach` loop. The first chunk is a reply, the rest are
  follow-ups, and an empty list also falls through the `foreach`. That is a second paginator next to
  `PagedReplyExtensions.Paginate`, which X-9 standardised on. For a text command from a non-moderator, `isPrivate`
  makes the method `return` before anything is sent (`SinLog.cs:47-48`), so the user gets no reply.
- **Proposed:**
  ```csharp
  public Task SinLogAsync(CommandContext ctx, SinQueryType type, DiscordUser? user = null)
      => ctx.RunAsync(
          CanView(ctx.RequiredMember, user ?? ctx.User),
          target => type is SinQueryType.Mod
              ? this.ModeratorStatsAsync(ctx.RequiredGuild, target)
              : this.SinListAsync(ctx.RequiredGuild, target, ToSinType(type)),
          IsModerator(ctx.RequiredMember) ? Visibility.Public : Visibility.Private);

  private static SinType? ToSinType(SinQueryType type) => type switch
  {
      SinQueryType.Warn => SinType.Warn, SinQueryType.Mute => SinType.Mute,
      SinQueryType.Ban => SinType.Ban, SinQueryType.Kick => SinType.Kick,
      SinQueryType.All => null,
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, null), // Mod is dispatched above; enum guard kept
  };
  // query: .Where(sin => sinType == null || sin.SinType == sinType)
  ```
  The list step returns its lines, and the command ends with `ctx.ReplyPagedAsync(template, lines, ephemeral)`. T-1
  leaves paged replies out of `Reply`, so this command keeps one direct primitive call, after
  `RunAsync`'s validation and defer. If `Reply` gets `Pages` later (T-1 says to add it at three paged commands), this
  is one of them. The "Handled above" throw goes, and so do the four `Where` copies and the chunking loop. A
  non-moderator using the text command now gets the "can't view others" error, or their own log privately.
- **Size:** about −40 lines, 1 file (−60 with SIN-2 and SIN-5).
- **Needs:** F-5. SIN-2 for the row shape.
- **Interacts with:** X-9 (paging), M-10 (`OrderBy`), T-4 (removes the "Handled above" restructuring). `SinLogTests`
  reads the follow-up messages for long logs, so those assertions move to pages.

#### SIN-4 `/Publish`: one `null` for two failures, a try/catch the extensions already wrap, and a pair that can't disagree  `pipe` `state` · **low**
- **Where:** `PublishCommands.SendPublicLogMessage` (`PublishCommands.cs:102-142`), `PublishAsync` (`:74-98`),
  `PublishSinAsync(ctx, sinId, SinType, PublishType)` (`:36`), `LogPublishedMessageNotFound` (`:164-165`).
- **Now:** `SendPublicLogMessage` returns `null` both when no public ban log is set and when the channel can't be
  fetched. The reply then always says "Verify the bot has access to send messages", which is wrong for the first case.
  `SIN-74` pins that text on purpose. A missing earlier post is handled with `try { GetMessageAsync } catch
  (NotFoundException)` plus an injected `ILogger` and a `[LoggerMessage]`, although
  `DiscordChannelExtensions.GetMessageOrDefaultAsync(MessageId?)` (`DiscordChannelExtensions.cs:54`) already does exactly
  that. `PublishSinAsync` takes `SinType` and `PublishType` separately, so `(SinType.Warn, PublishType.Unban)`
  compiles. `CommandContext` is passed three levels down.
- **Proposed:** `ResolvePublicLogAsync(guild) : Task<Result<DiscordChannel>>` with two `Error.Invalid` messages ("No
  public ban log is set. Use /ModSettings PublicBanLog." and the access message). The post becomes
  `await channel.GetMessageOrDefaultAsync(storedId) is { } old ? await old.ModifyAsync(embed) : await channel.SendMessageAsync(embed)`.
  The sin type comes from the publish type (`publish is PublishType.Kick ? SinType.Kick : SinType.Ban`), so the
  Ban/Kick entry points pass one value. With T-1, the act step takes the guild and moderator, not `ctx`.
- **Size:** about −25 lines, 4 files. The `ILogger<PublishCommands>` dependency and the logger method go.
- **Needs:** F-5 for the error rendering.
- **Interacts with:** M-11 (rank 16 shared the path), I-11/R-1 (the access failure still reaches `CommandHandler`'s
  missing-permission help, as `PublishTests` SIN-75 expects). `PublishTests.PublishBan_WithNoBanLog_SaysItFailed` (SIN-74)
  pins the current text, so the owner decides (question 2).

#### SIN-5 "Count this person's sins by type" is written twice  `remove` · **low**
- **Where:** `SinLog.cs:64-76` (moderator stats: `GroupBy(SinType)`, four fields), `UserInfoCommands.GetAndAddModerationInfo`
  (`UserInfoCommands.cs:177-189`: `GroupBy(new { UserId, GuildId, SinType })`, three fields).
- **Now:** both run `GroupBy … ToDictionaryAsync` and then `GetValueOrDefault(type, 0).ToString()` for each field.
  UserInfo groups on two columns that its `Where` has already fixed. The two differ in ways nobody seems to have
  chosen: SinLog Mod counts all time and includes Kicks, while UserInfo uses the auto-pardon window and leaves Kicks out.
- **Proposed:** `IQueryable<Sin>.CountByTypeAsync()` returning `IReadOnlyDictionary<SinType, int>` and one
  `AddSinCountFields(embed, counts, params SinType[] shown)`. The filters stay at the call sites.
- **Size:** about −12 lines, 2 files, +10 for the helper.
- **Needs:** nothing.
- **Interacts with:** question 3.

#### SIN-6 Diagnostics findings: the kind decides what `SubjectIds` means  `state` `cs15` · **low**
- **Where:** `ChannelFinding(ChannelSnapshot, ChannelFindingKind, IReadOnlyList<ulong> SubjectIds)` and
  `GuildFinding(GuildFindingKind)` (`ModerationFindings.cs:40-46`), `DiagnosticsReportFormatter.DescribeChannelFinding`
  (`DiagnosticsReportFormatter.cs:112-139`), `AppendChannelSection`/`ChannelCount` (`:92-110`).
- **Now:** `SubjectIds` holds role ids, member ids or nothing, depending on the kind. The formatter builds both a
  `roles` string (one `guild.Roles.FirstOrDefault` per id) and a `members` string for every finding, then uses one of
  them. Which section a finding belongs to is written out twice as lists of three kinds (`:36-41`, `:61-64`). That's
  why `BuildText`, `BuildEmbed` and `DescribeChannelFinding` all take the `GuildSnapshot`. `GuildFinding` is a record
  that only wraps the enum. Both formatter switches end in `_ => throw new ArgumentOutOfRangeException`.
- **Proposed:**
  ```csharp
  public enum Action { Mute, Lock }
  public abstract record ChannelFinding(ChannelSnapshot Channel, Action Action)   // cs15: closed
  {
      public sealed record NotDenied(ChannelSnapshot Channel) : ChannelFinding(Channel, Action.Mute);
      public sealed record BotCannotLock(ChannelSnapshot Channel) : ChannelFinding(Channel, Action.Lock);
      public sealed record RolesLeak(ChannelSnapshot Channel, Action Action, IReadOnlyList<RoleSnapshot> Roles)
          : ChannelFinding(Channel, Action);
      public sealed record MembersLeak(ChannelSnapshot Channel, Action Action, IReadOnlyList<ulong> MemberIds)
          : ChannelFinding(Channel, Action);
  }
  ```
  `CheckMutes`/`CheckLocks` already have the `RoleSnapshot`s, so they pass them instead of `.Select(role => role.Id)`.
  The sections filter on `f.Action`. `DescribeChannelFinding` switches on the case and on `Action` for the wording.
  The formatter no longer needs the snapshot or the id lookups. `GuildFinding` becomes the bare `GuildFindingKind`.
  On net10 the `_` arm stays on the hierarchy switch. With cs15 `closed`, it goes.
- **Size:** about −15 lines, 3 files, plus `ModerationDiagnosticsTests`, which builds findings by kind.
- **Needs:** `net11.0` for the last step only.
- **Interacts with:** KI-57, KI-58, KI-59 (each changes which findings appear, not their shape).

#### SIN-7 The mute role in `GuildSnapshot` is two nullables with three valid combinations  `state` · **low**
- **Where:** `GuildSnapshot.MuteRoleId`/`MuteRolePosition` (`PermissionSnapshots.cs:44-51`),
  `DiagnosticsSnapshotBuilder.Build` (`DiagnosticsSnapshotBuilder.cs:16-22`), `ModerationDiagnostics.Analyze`/`CheckGuild`/`CheckMutes`
  (`ModerationDiagnostics.cs:29`, `:45-53`, `:62`).
- **Now:** the builder already knows whether the configured role exists. It throws that away, and `Analyze` works it
  out again with `guild.Roles.Any(role => role.Id == id)`. `CheckMutes` then checks `!muteRoleExists ||
  guild.MuteRoleId is not { } muteRoleId`. The position set without an id is representable but meaningless.
- **Proposed:** `MuteRole` with `NotConfigured`, `Missing(ulong Id)` and `Present(ulong Id, int Position)`. `CheckGuild`'s
  first `if`/`else if` and the `MuteRoleAboveBot` test become one switch, and `CheckMutes` starts with
  `if (guild.MuteRole is not MuteRole.Present(var muteRoleId, _)) yield break;`. `muteRoleExists` goes.
- **Size:** about ±0 lines, 3 files plus the test builders. It removes one recomputation and one invalid combination.
- **Needs:** nothing. cs15 `closed` drops a `_` arm if one is added.
- **Interacts with:** none.

#### SIN-8 Effective permissions recomputed for every channel and role pair  `perf` · **low**
- **Where:** `EffectivePermissions.For` (`EffectivePermissions.cs:14-42`), called from `ModerationDiagnostics.CheckMutes`
  (`:75`) and `CheckLocks` (`:100`). Runs once per `/Diagnostics Check`.
- **Now:** for each channel and each candidate role, `For` scans every role to rebuild the base permissions
  (`@everyone | role`), scans the overwrites twice, and the caller allocates a new `HashSet<ulong>`. That is
  O(channels × roles × (roles + overwrites)), done twice. A guild with 250 roles and 500 channels does about 70 million
  role visits and allocates 250,000 sets. `ApplyLock` also allocates a new channel record per channel, which is fine.
  `administrators.Contains(role)` (`:21`) is a list scan with record equality.
- **Proposed:** compute `everyonePermissions` once and pass `basePermissions = everyone | role.Permissions`. Turn
  `For` into `For(DiscordPermissions basePermissions, ulong guildId, ChannelSnapshot channel, ulong? heldRoleId,
  ulong? muteRoleId)`, so it takes at most two held roles and needs no set. Make `administrators` a set of ids.
- **Size:** about ±0 lines, 2 files. Each `/Diagnostics Check` does O(channels × roles × overwrites) work.
- **Needs:** nothing. `ModerationDiagnosticsTests` covers the resolution order, so it guards the rewrite.
- **Interacts with:** none.

#### SIN-9 The diagnostics ignore list has two shapes, and Remove builds rows it discards  `remove` · **low**
- **Where:** `DiagnosticsIgnores` (`DiagnosticsIgnores.cs:15-37`), `IgnoreCommands.ToIgnoredItems`/`ReportAsync`/`ViewAsync`
  (`DiagnosticsCommands.cs:99-171`), `SettingsModule.RemoveDiagnosticsIgnoredItemsAsync`
  (`SettingsModule.Moderation.DiagnosticsIgnores.cs`).
- **Now:** Check uses `DiagnosticsIgnores` (two id sets, rebuilt from the cached rows on each call), while View and the
  `ReportAsync` mentions switch over `DiagnosticsIgnoredItem` again (with `_ => string.Empty`). `/Diagnostics Ignore
  Remove` builds full `DiagnosticsIgnoredRole`/`Member` rows, moderator and timestamp included, only so Settings can
  compare their `(Kind, Id)` keys. After S-12 the factories no longer fail, so `ToIgnoredItems`'s `Validation` branches
  and their `skipped` adds can't happen.
- **Proposed:** after S-12, `ToIgnoredItems` becomes one `switch` expression with `OfType`. Remove takes
  `DiagnosticsIgnores` (the id sets), which `ToIgnores(items)` builds without a moderator or a time. Give
  `DiagnosticsIgnoredItem` one `Mention` member (`<@&id>` or `<@id>`) and use it in View and `ReportAsync`. That drops
  the `_` arm, which T-3 would otherwise make `closed`.
- **Size:** about −25 lines, 3 files (one in Settings).
- **Needs:** S-12. Section 4 owns the Settings signature.
- **Interacts with:** S-12, X-12 (no-op reporting, kept), KI-62 (View's 1000-character cut, still open; `ReplyPagedAsync`
  would fix it as X-9 did for the other Views).

#### SIN-10 `CheckSpamResult` is a `bool` and a constant  `state` `perf` · **low**
- **Where:** `SpamTrackerModule.CheckSpam` and `CheckSpamResult` (`SpamTrackerModule.cs:39-105`, `:115-119`),
  `SpamEvents.HandleEventAsync` (`SpamEvents.cs:38-44`, `:59-60`, `:75`, `:85`).
- **Now:** every message in a guild with AntiSpam on allocates a `CheckSpamResult { IsSpam = false }`, and seven
  `return new CheckSpamResult …` sites build it. `Reason` is only meaningful when `IsSpam` is true, and it is always the
  literal "Auto mod: Spam detected.". `SpamEvents` then turns that string back into a reason with
  `ModerationReason.CreateIfNotBlank(…).Match(r => r, _ => null)`. `CheckSpam` already refuses non-members, yet
  `SpamEvents` checks `args.Author is not DiscordMember` again after it (`:43`).
- **Proposed:** `public static readonly ModerationReason SpamReason` in `SpamEvents`, built with D-6's trusted
  `Create`. `CheckSpam(DiscordMember member, DiscordMessage message) : ValueTask<bool>`, and `SpamEvents` does the
  `DiscordMember` pattern once, before the call. This is F-7's "producers return the validated type", taken to the end:
  the producer returns nothing but the verdict. It also follows F-10's rule, with plain `return false` for "not for me".
- **Size:** about −12 lines, 2 files.
- **Needs:** nothing.
- **Interacts with:** F-7, F-10, D-2 (the `CreateIfNotBlank` call it removes), section 5 (the per-message order).

#### SIN-11 The spam tracker is a mutable record that works only while FusionCache returns the same instance  `state` · **med**
- **Where:** `SpamTrackerModule` (`SpamTrackerModule.cs:27-33`, `:64-104`, `SpamTracker` at `:107-113`),
  `GrimoireServiceRegistration.cs:238-239` (`AddFusionCache().AsHybridCache()`).
- **Now:** `SpamTracker` is a `sealed record` with four `set` properties. It is fetched through `HybridCache.GetOrCreateAsync`,
  mutated, and used as a `lock` object. That is only correct because FusionCache's memory level returns the cached
  reference (no auto-clone, no L2). `HybridCache`'s own contract allows a fresh deserialised copy per call for a mutable
  type that isn't `[ImmutableObject(true)]`. That is the Microsoft implementation's default, and FusionCache does the
  same if auto-clone or a distributed level is turned on. Under either, every message would see a new tracker, nothing
  would be detected, and nothing would fail. Value equality on a mutable lock object is unused but misleading. The
  scoring (`:77-95`) is inline in the locked block, so it can only be tested through the event.
- **Proposed:** state that lives in this process and is never serialised doesn't belong behind `HybridCache`. Use
  `IMemoryCache` (reference semantics are its contract) with a 30-minute sliding expiry, or a
  `ConcurrentDictionary<(GuildId, UserId), SpamState>` with `AddOrUpdate` and a prune of idle entries. Make the state
  an immutable record and the scoring pure:
  ```csharp
  internal sealed record SpamState(double Points, string LastContent, ulong LastMessageId, DateTimeOffset At)
  {
      public static readonly SpamState Empty = new(0, "", 0, DateTimeOffset.MinValue);
      public (SpamState Next, bool IsSpam) Add(DiscordMessage message) { /* today's arithmetic, no mutation */ }
  }
  ```
  With `ConcurrentDictionary`, `AddOrUpdate` retries on a race, so no `lock` is needed. KI-53's reset becomes
  `Next with { Points = 0 }`.
- **Size:** about ±0 lines, 1 file, plus a unit test for `Add`. It removes the dependency on the cache adapter's
  behaviour.
- **Needs:** nothing.
- **Interacts with:** KI-53 (keep its reset), section 4 (owns FusionCache uses; this moves one use out of it). See
  question 4.

#### SIN-12 The spam mute is a second copy of `/Mute`'s act step and embeds  `remove` · **low**
- **Where:** `SpamEvents.HandleEventAsync` (`SpamEvents.cs:51-97`), `MuteUser.cs:100-117` (section 7).
- **Now:** after detection, `SpamEvents` records the sin, calls `AddMute`, resolves and grants the role, then builds a
  "Mute" log embed (User, Sin Id, Moderator, Length, Reason) and a "Mute Id" DM. These are the same fields and wording
  as `/Mute`. It also reads `DateTimeOffset.UtcNow` three times (`:57`, `:58`, `:84`), so the sin, the end time and the
  embed timestamp can differ.
- **Proposed:** when section 7 extracts `/Mute`'s act step (`MuteAsync(guild, actor, member, duration, reason)
  : Task<Result<MuteOutcome>>`, after M-6 and KI-55), `SpamEvents` calls it with `ModerationActor.System` and the
  doubled duration, and keeps only the delete-last. The two embeds are built in one place for both callers. Take one
  `now`.
- **Size:** about −35 lines in `SpamEvents`, 1 file, if section 7's step exists. Otherwise about −15 for shared
  embed builders alone.
- **Needs:** section 7's act step.
- **Interacts with:** M-6, KI-53, KI-55, KI-63, KI-107, F-7.

#### SIN-13 `ModuleArguments` mirrors `Module` so the command can't pick General, and Settings refuses it again  `state` · **low**
- **Where:** `ModuleCommands.ModuleArguments` and `MapToModule` (`ModuleCommands.Set.cs:16-28`, `:56-64`),
  `SettingsModule.ToSettingType` and `SetModuleState` (`SettingsModule.ModuleEnabled.cs:15-34`).
- **Now:** the command enum leaves out `General`, the mapping switch ends in `_ => throw`, and `SetModuleState(Module)`
  still checks at run time and returns `Error("module.general.immutable")` for General, which no caller can send.
- **Proposed:** a `ToggleableModule` enum in `Grimoire.Settings.Enums` (the six members), used by
  `SetModuleState`. The command takes it directly, since its member names are already the display names.
  `ModuleArguments`, `MapToModule`, `ToSettingType`'s `Result` and the General error go. `Module` keeps General for the
  log-type mapping.
- **Size:** about −20 lines, 3 files.
- **Needs:** nothing.
- **Interacts with:** section 4 (Settings API). `SetModuleStateTests` line 60 tests the General refusal and goes with it.

#### SIN-14 `ChannelSettingCommand.SetAsync` picks modes with `null` and a `bool`  `state` `remove` · **low**
- **Where:** `ChannelSettingCommand.SetAsync` (`ChannelSettingCommand.cs:30-68`). It has nine parameters:
  `DiscordPermission[]? botNeeds` (null means log permissions, `[]` means any channel), `bool isModerationLog` and
  `DiscordColor? color`.
- **Now:** `isModerationLog` stops "disabled the moderation log" from being queued to the moderation log. The
  publisher reads the setting when it sends, and it is already off by then, so the entry was dropped anyway
  (branch-review's ModLogChannel note). `null` and `[]` meaning two different things is easy to swap.
- **Proposed:** an enum `ChannelNeeds { Any, Log, LogWithFiles }` mapped to the arrays in one switch, and drop
  `isModerationLog`. With T-1, `SetAsync` becomes the act step: `GetChannelOption(...).Bind(CheckCanPost)` is the
  input, and the save returns `Reply.Logged(...)`.
- **Size:** about −20 lines, 1 file, plus 1–2 lines at each of the 7 callers.
- **Needs:** F-5 for the T-1 half.
- **Interacts with:** I-4, I-13 (the "not logged to itself" bullet), L-11.

#### SIN-15 `/UserInfo`: a five-tuple built by assignment, and work done before it is known to be needed  `perf` · **low**
- **Where:** `UserInfoCommands.GetUserInfo(DiscordUser, DiscordMember?)` (`UserInfoCommands.cs:56-86`),
  `GetAndAddLevelInfo` (`:137-146`).
- **Now:** five locals are declared, assigned in an `if`/`else` and returned as an unnamed
  `(DiscordColor, string, string, string, ulong[])`. The member's role ids are copied to an array and then to a
  `HashSet<RoleId>`. `XpHistory.SumAsync` runs before the level settings and rewards lookups that may make the command
  skip the fields. The three sections (usernames, level, moderation) run one after another, each with its own module
  check.
- **Proposed:** `sealed record Profile(DiscordColor Color, string Name, string AvatarUrl, string Joined, IReadOnlySet<RoleId> Roles)`
  with `Profile.Of(user, member) => member is { } m ? new(...) : new(...)`. Move the sum after the settings check.
  Optionally, give each section its own context and run them with `Task.WhenAll` (principle 8). It's a moderator
  command, so this is only latency.
- **Size:** about −12 lines, 1 file.
- **Needs:** F-1 for the `GetOrElse`/`Match(..., _ => "Unknown")` tails.
- **Interacts with:** I-5 (the level fields skip on failure, kept), SIN-5.

## Checked, no finding

- **Streaming the sin list.** One user's sins in one guild number in the tens, and pages need the total count, so
  `ToListAsync` is right. `AsAsyncEnumerable` would save nothing.
- **Streaming the purge.** `PurgeMessagesAsync` (section 6's file) is already lazy up to `Take(count)` and caps at
  1,000. Deleting each chunk of 100 as it arrives instead of after the fetch would not shorten a run that is limited
  by sequential REST calls, so the `ToArrayAsync` stays.
- **`SpamFilterOverrideSetting` mirroring `SpamFilterOverrideOption`.** Taking the Settings enum directly would need
  `[ChoiceDisplayName]` in `Grimoire.Settings`, which doesn't reference DSharpPlus (principle 9). The one switch that
  M-12 left is the right size. The `_ => throw` arm is an enum guard (T-4).
- **`/ModSettings AutoPardon`'s 100-year check** can't be an attribute, because it depends on `duration_type`. Under T-1
  it moves before the defer and becomes `Error.Invalid` (red), which T-2 already notes.
- **`DiagnosticsSnapshotBuilder` and `EffectivePermissions` as pure code over snapshots.** This is the shape the other
  sections are moving towards. Only SIN-6 to SIN-8 change it.
- **`PublishCommands.SavePublishedMessageAsync`'s three branches.** The key is `(SinId, PublishType)`, and each branch is
  one statement. An upsert wouldn't be shorter in EF Core 10.

## Questions for the owner

1. **Which pardon does a sin show?** A sin can have several `Pardons` rows (key `SinId, SetAt`). SinLog shows the
   latest moderator with the *earliest* date. `/Publish Unban` shows the latest reason with the earliest date. Is
   "first pardoned on, latest pardon's moderator and reason" what you want? SIN-2 encodes whatever you choose.
2. **`/Publish` with no ban log configured (SIN-4).** Today it says to check channel access, and SIN-74 pins that.
   Should it say no public ban log is set, and name `/ModSettings PublicBanLog`?
3. **Sin counts (SIN-5).** SinLog Mod counts all time and includes Kicks. UserInfo uses the auto-pardon window, leaves
   Kicks out and counts pardoned sins. Should both use one rule? If so, which?
4. **Spam tracker store (SIN-11).** Should it be `IMemoryCache` with a sliding expiry (closest to today), or a
   `ConcurrentDictionary` with a lock-free immutable state and an idle prune?
5. **Spam mutes through `/Mute`'s act step (SIN-12).** Spam mutes would then follow whatever order section 7 settles
   for `/Mute`, including KI-55's "save before it works". Is one path for both what you want, or should spam mutes stay
   separate?
6. **`/SinLog` as a text command for non-moderators (SIN-3).** Today it sends nothing. After SIN-3 they get their own
   log, as slash users do. Is that intended, or should text `/SinLog` require Manage Messages?
