# Section 7: Moderation actions

Reviewed 2026-11-09, against the code as it will be once every fix in `review-decisions.md` is in (D-2, M-7, S-12 and the
`RequiredGuild` ruling in particular). Scope: `Moderation/{Ban,Kick,Mute,Warn,Lock}`, `ModerationDirectMessage`,
`ModerationRank`, the two expiry tasks, and the helpers only they use (`ExpiredItemTriage`, `OverwriteChannelResult`,
`DurationType`). That is 1,911 lines today.

The biggest win is moving the ten action commands onto T-1 (MOD-1, about −190 lines). Most of that comes from four
shared pieces: a pure rank check that also covers self-targeting (MOD-2), one embed and sin-save helper (MOD-3), one
optional-reason type from the parameter through to `BanAddedEvent` (MOD-4), and a duration value (MOD-5). After that
come the expiry polls, which re-scan the whole mute and lock history every 5 seconds (MOD-6), and the background-task
cleanups (MOD-7, MOD-8). That makes 14 findings: 4 med, 8 low, 2 optional. Together they come to about −300 lines once the
overlap between MOD-1 and MOD-2..4 is taken out.

## Findings

#### MOD-1 The action commands measured against T-1  `pipe` · **med**
- **Where:** `Kick/KickUser.cs:31-88`, `Ban/Commands/AddBanCommand.cs:28-92`, `Ban/Commands/RemoveBanCommand.cs:24-54`,
  `Mute/Commands/MuteUser.cs:35-123`, `Mute/Commands/UnmuteUser.cs:27-98`, `Lock/Commands/LockChannel.cs:27-79`,
  `Lock/Commands/UnlockChannel.cs:26-52`, `Mute/Commands/MuteAdminCommands.Set.cs:17-42`, `MuteAdminCommands.View.cs:14-36`.
- **Now:** every command defers, then handles each refusal with its own `if (...) { reply; return; }`. The refusals in
  Ban, Kick, Mute and Unmute's no-role case, Lock's "Channel not of valid type." and every Unlock and Unban failure use a
  **public** `ctx.ReplyAsync(GrimoireColor.Yellow, ...)` or a plain `ReplyAsync(message:)`. So KI-21's ephemeral rule
  only covers the few `SendErrorResponseAsync`/`SendWarningResponseAsync` calls. Each command also builds a reply and
  log pair by hand. `LockChannel` collects its outcome into a mutable `Error? failure` through
  `.Match<Error?>(_ => null, e => e)` (`:47-66`). `UnlockChannel` replies inside `TapAsync` and logs inside `Match`
  (`:37-51`). `MuteAdminCommands.View` hand-writes the ephemeral-defer branch (`:16-19`), which is I-19's case.
- **Proposed:** T-1 (F-5). The pure checks (self-target, bot hierarchy, channel type, the thread-lock bot permission)
  become the input, so they're answered ephemerally before the defer. The settings-dependent rank check runs in the act
  step (MOD-2). Kick as a sample, with MOD-2..4 in place:
  ```csharp
  public Task KickAsync(CommandContext ctx, DiscordMember member, ModerationReason? reason = null)
      => ctx.RunAsync(ModerationRank.NotSelf(ctx.User, member, "kick"),
          target => this.KickAsync(ctx.RequiredGuild, ctx.RequiredMember, target, reason));

  private async Task<Result<Reply>> KickAsync(DiscordGuild guild, DiscordMember moderator, DiscordMember target,
      ModerationReason? reason)
      => await ModerationRank.Check(guild, moderator, target, await this.MuteRoleAsync(guild), "kick")
          .BindAsync(async _ =>
          {
              await this._guildLog.DirectMessageAsync(target, ModerationEmbeds.Dm("Kicked",
                  $"You have been kicked from {guild.Name}{ForReason(reason)}", GrimoireColor.Yellow), "kick");
              await target.RemoveAsync(reason?.Value);
              var sinId = await this._sins.AddAsync(Sin.ForKick(moderator.ToActor(), target.GetUserId(),
                  guild.GetGuildId(), DateTimeOffset.UtcNow, reason));
              return Result.Ok(Reply.Logged(ModerationEmbeds.Action("Kick", target, sinId, moderator, reason,
                  GrimoireColor.Yellow), guild.GetGuildId(), GuildLogType.Moderation));
          });
  ```
  Lock's three-way branch becomes a pure input:
  ```csharp
  private static Result<DiscordChannel> Lockable(DiscordGuild guild, DiscordChannel channel) => channel switch
  {
      { IsThread: true } when !channel.PermissionsFor(guild.CurrentMember).HasPermission(DiscordPermission.ManageMessages)
          => new Error.Invalid($"{guild.CurrentMember.Mention} needs **Manage Messages** in {channel.Mention} to lock it."),
      { IsThread: true } or { Type: DiscordChannelType.Text or DiscordChannelType.Category or DiscordChannelType.GuildForum }
          => channel,
      _ => new Error.Invalid("Channel not of valid type."),
  };
  ```
  Lock and Unlock reply with different text from what they log, so they use `new Reply(embed, Log: new GuildLogMessage {...})`
  rather than `Reply.Logged`. `/Mutes Create` and `/Mutes Refresh` post progress edits and a diagnostics follow-up, so
  they're like the paged replies in T-1's "Where the shape doesn't fit". Leave them as they are, apart from MOD-13.
- **Size:** estimated per file, header and usings included:

  | Command | Today | Target | Delta |
  |---|---|---|---|
  | Kick | 89 | ~62 | −27 |
  | Ban | 93 | ~75 | −18 (the `CancelBan` try/catch stays) |
  | Unban | 55 | ~42 | −13 (with MOD-11) |
  | Mute | 156 | ~118 | −38 (`TryRecordMuteAsync` stays; its `bool` becomes `Result<Unit>`) |
  | Unmute | 104 | ~82 | −22 |
  | Lock | 131 | ~98 | −33 |
  | Unlock | 75 | ~52 | −23 |
  | Mutes Set | 53 | ~42 | −11 (`MuteRoleProblem` becomes the input) |
  | Mutes View | 42 | ~38 | −4 |
  | **Total** | 798 | ~609 | **about −190** |

  Warn (77 → 56) is F-5's own sample and isn't counted again here.
- **Needs:** F-3, F-4, F-5. MOD-2, MOD-3 and MOD-4 make up about a third of the cut.
- **Interacts with:** KI-21 (the refusals listed above become ephemeral, see Questions), I-19, M-7, F-8 (Unban's
  `ServerErrorException` arm). `KickTests.Kick_OfYourself_IsRefused` and the Lock/Unlock tests check `Description`
  only, so they keep passing provided `RunSlashCommandAndWaitAsync` returns the ephemeral response.

#### MOD-2 `ModerationRank` becomes pure and also does the self-check  `pipe` `state` · **low**
- **Where:** `ModerationRank.GetRefusalReasonAsync` (`ModerationRank.cs:19-37`). The self-checks are at
  `KickUser.cs:41-45`, `AddBanCommand.cs:48-52` and `MuteUser.cs:52-56`. The mute role is read at `ModerationRank.cs:30`
  and again at `MuteUser.cs:64-71`.
- **Now:** the method takes `SettingsModule` and `CommandContext` and returns `string?`, with null meaning "allowed". It
  exempts "acting on yourself" (`:27`), but all three callers have already refused that a few lines earlier, each with
  its own five-line block, so the exemption is dead. It reads `GetEffectiveMuteRole`, which checks the Moderation module
  again inside commands that are already `[RequireModuleEnabled(Module.Moderation)]`. `/Mute` then reads the same
  setting a second time to find the role it grants.
- **Proposed:** `static Result<DiscordMember> NotSelf(DiscordUser invoker, DiscordMember target, string action)` (the
  input, pure) and `static Result<DiscordMember> Check(DiscordGuild guild, DiscordMember invoker, DiscordMember target,
  RoleId? muteRole, string action)` (pure, `Error.Invalid`). The caller reads `GetConfiguredMuteRole` once, and `/Mute`
  passes the same value on to the role lookup. The message stays "You can't {action} yourself." for all three. Both
  functions can be unit-tested without a context.
- **Size:** about −15 lines across 4 files. One cached settings read and one module check less per `/Mute`.
- **Needs:** F-3, F-4. F-1 turns `GetOrElse(() => null)` into a plain await.
- **Interacts with:** KI-01, KI-02 (behaviour kept). Ban still checks rank only when the user is a member.

#### MOD-3 One moderation embed and one sin save  `remove` · **low**
- **Where:** the "Author, User, Sin Id, Moderator, [Length], Reason, colour, timestamp" embed in `Warn.cs:55-62`,
  `KickUser.cs:71-80`, `MuteUser.cs:99-109`, `AddBanCommand.cs:84-91` and `BanAddedEvent.cs:47-56`. The
  `CreateDbContextAsync → Sins.Add → SaveChangesAsync` block in `Warn.cs:48-54`, `KickUser.cs:53,68-69`,
  `MuteUser.cs:134-136` and `BanAddedEvent.RecordSinAsync` (`:64-75`), plus `SpamEvents` (section 8).
- **Now:** five hand-built copies of one embed, which have already drifted ("Mod" in `BanAddedEvent` against
  "Moderator" elsewhere, and "None" against leaving the Reason field out). Five copies of one save.
- **Proposed:** `ModerationEmbeds.Action(string title, DiscordUser user, SinId? sinId, DiscordUser? moderator,
  ModerationReason? reason, DiscordColor color)` returns a `DiscordEmbedBuilder`. Mute adds its Length field afterwards,
  and a null `sinId` shows "Not recorded yet" (Ban). Add `ModerationEmbeds.Dm(title, text, color)` for the four DMs,
  and an extension `IDbContextFactory<GrimoireDbContext>.AddSinAsync(Sin) : Task<SinId>`. Decide how the Reason field
  looks in one place (see MOD-4).
- **Size:** about +20 / −45 lines, 7 files.
- **Needs:** S-12, so that `Sin.ForX` returns `Sin` and the `.Match(s => s, _ => throw ...)` at `Warn.cs:52`,
  `KickUser.cs:57`, `MuteUser.cs:89` and `BanAddedEvent.cs:71` is gone.
- **Interacts with:** D-2, S-12, section 8 (`SpamEvents` uses the same helpers).

#### MOD-4 One optional-reason type from the parameter to the event  `state` · **med**
- **Where:** `KickUser.cs:35,56,62,66,79`, `MuteUser.cs:45,84,88,108,116`, `AddBanCommand.cs:35,65,68,74,89`,
  `BanHandoff.cs:13` (`PendingBan(ModeratorId, string Reason)`), `BanAddedEvent.cs:40-42,56,64-70`,
  `LockChannel.cs:40,91,117`, and `ModerationDirectMessage.ForReason(string?)` (`:44`).
- **Now:** the reason travels as two values. The raw `string?` goes to Discord's audit-log header, the embed and the
  DM, while a trimmed `ModerationReason?` goes to the sin. So a reason with leading spaces is stored differently from
  what was shown. Blank reasons are handled three different ways: Kick and Mute drop them
  (`CreateIfNotBlank(...).Match(r => r, _ => null)`, where the `_ => null` arm hides a failure that can't happen),
  Lock refuses them with "Reason must be 1–1000 non-whitespace characters" (`CreateIfNotNull`), and Ban defaults to `""`
  and lets `BanAddedEvent` drop it later. `string.IsNullOrWhiteSpace(reason)` is checked again at five sites to decide
  whether to show the field.
- **Proposed:** F-6's converter for an optional `ModerationReason? reason = null` parameter on Kick, Mute, Ban and
  Lock, with blank meaning none, as `CreateIfNotBlank` already does. `PendingBan.Reason` becomes `ModerationReason?`, so
  the handoff path in `BanAddedEvent` needs no `Create` at all. Only the audit-log fallback keeps F-7's
  `CreateIfNotNull`. `ForReason(ModerationReason?)`. Discord gets `reason?.Value`. The embed helper (MOD-3) decides
  whether to show the field.
- **Size:** about −15 lines and 3 `.Match(r => r, _ => null)` arms, across 6 files.
- **Needs:** F-6. Without its spike, the same type can be built in the T-1 input step.
- **Interacts with:** F-6, F-7, D-2, M-7. Lock's blank-reason behaviour changes (see Questions).

#### MOD-5 A duration value instead of `(DurationType, int)` and three end-time checks  `state` · **med**
- **Where:** `MuteUser.cs:40-43,81,104,116`, `LockChannel.cs:30-33,49,54,69,77,84,101,114,127`,
  `DurationTypeExtensions.GetDateTimeOffset` (`Shared/SharedDtos/DurationType.cs`). The end-time checks are in
  `MuteAdded.Create`, `ChannelLocked.Create` and `ThreadLocked.Create` (`endTime <= setAt`).
- **Now:** a duration is two loose parameters that are passed around separately (`int` in the commands, `long` in
  `LockChannel`'s helpers) and formatted by hand as `$"{durationAmount} {durationType}"` in four places. The end time is
  computed from `DateTime.UtcNow` in `GetDateTimeOffset`. The set time is a second clock read (`DateTimeOffset.UtcNow`
  in `SettingsModule.AddMute`, `:63`, and in `LockChannel.cs:98,126`). Each domain factory then re-checks
  `endTime > setAt`. After KI-07 and M-10 (`[MinMaxValue(1, 36_500)]`) that check can't fail, but if it did, `/Mute`
  would already have granted the role and saved the sin before `AddMute` refused, and the undo would have to run.
- **Proposed:** `readonly record struct ModerationDuration(TimeSpan Value)` with
  `From(DurationType type, int amount)` (the one switch, keeping its enum guard per T-4) and a `ToString()` that gives
  "5 Minutes". The domain factories take `setAt` and `ModerationDuration` and set `EndTime = setAt + duration`, so the
  end time can't come before the set time and the three checks go. There's one clock read per command. `/Mute` then
  only fails to record a mute during an outage.
- **Size:** about +15 / −30 lines, 6 files (3 in `Grimoire.Settings/Domain`).
- **Needs:** section 2's view on the lock and mute factories. No migration: `EndTime` is still stored.
- **Interacts with:** KI-07, M-10, S-12 (the same factories lose their id checks).

#### MOD-6 The expiry polls re-scan the whole mute and lock history every 5 seconds  `perf` · **med**
- **Where:** `SettingsModule.GetAllExpiredMutes` (`SettingsModule.Moderation.Mutes.cs:116-131`),
  `GetAllExpiredChannelLocks` and `GetAllExpiredThreadLocks` (`SettingsModule.Moderation.Locks.cs:96-116, 167-187`),
  through `ActiveMutes`/`ActiveChannelLocks`/`ActiveThreadLocks` (`:147-151`, `:190-201`). Called from
  `MuteBackgroundTasks.RunTask` and `LockBackgroundTasks.RunTask` on a 5-second timer. That's 3 queries per tick, about
  52,000 a day.
- **Now:** `end_time <= now` matches every `Added`/`Locked` row ever written, because history is append-only and a
  processed mute keeps its row. Each of those rows is then probed with `NOT EXISTS (later row for the same key)`
  against the primary key. Almost every tick returns nothing, but the work grows with all history, not with what is
  due. Rows for guilds the bot has left are returned on every tick and only then thrown away by `ExpiredItemTriage`
  (`KeepForLater`).
- **Proposed:** in this order:
  1. Run `EXPLAIN (ANALYZE, BUFFERS)` on the three queries against a copy of production, and add it to "Before
     deploying". If it's under a millisecond, stop here.
  2. Pass the bot's guild ids in, `Where(x => guildIds.Contains(x.GuildId))` from `discordClient.Guilds.Keys`. That
     removes the guilds the bot left from the result and the `KeepForLater` branch from both tasks (see MOD-7). Before
     the guild download finishes the set is empty, which is the same as today's "keep for later".
  3. Only if the plan is still history-bound: put a lower bound on the scan (`end_time > now - 90 days`), so an item
     that has failed for 90 days stops being retried. That needs a decision (see Questions).
- **Size:** step 2 is about +3 lines in Settings and −4 in the tasks. Step 3 is 3 lines.
- **Needs:** a production measurement. Step 3 needs an owner decision.
- **Interacts with:** P-5/S-8 (the `DropRedundantIndexes` migration keeps the primary key the probe uses, and the
  `end_time` index), section 3 (report the query shape once), F-9.

#### MOD-7 `ExpiredItemTriage` takes two bools and callers then use `!`  `state` `remove` · **low**
- **Where:** `Utilities/ExpiredItemTriage.cs` (34 lines), `MuteBackgroundTasks.cs:47-62`, `LockBackgroundTasks.cs:53-71`
  and `:103-120`.
- **Now:** the tasks turn "guild found" and "member/channel found" into two `bool`s, map them to an enum, `switch` on
  it, and then use `guild!`, `user!` and `channel!`, with a comment saying the triage guarantees them
  (`MuteBackgroundTasks.cs:61-62`).
- **Proposed:** patterns that keep the values:
  ```csharp
  if (!discordClient.Guilds.TryGetValue(mute.GuildId.Value, out var guild))
      return Unit.Value;                                   // keep for later (gone with MOD-6 step 2)
  if (await FindMemberAsync(guild, mute.UserId) is not { } member)
      return await RemoveMuteAsync(mute);                  // the member left: resolve
  ```
  Delete `ExpiredItemTriage` and `ExpiredItemTriageTests`.
- **Size:** about −45 lines including the test, 4 files.
- **Needs:** nothing. It combines with F-9's `Result<Unit>` return.
- **Interacts with:** F-9, T-5.

#### MOD-8 Background tasks: inject the singletons, constant reasons, and one unlock writer  `remove` · **low**
- **Where:** `MuteBackgroundTasks.RunTask` (`:24-27`) and `LockBackgroundTasks.RunTask` (`:24-27`), the static
  `Process…`/`Record…` methods that take 5 or 6 parameters each, and `RecordChannelUnlockAsync`/`RecordThreadUnlockAsync`
  (`LockBackgroundTasks.cs:123-147`). Also `MuteBackgroundTasks.cs:140`, where
  `new ModeratorId(discordClient.CurrentUser.Id)` duplicates `GetGrimoireModeratorId()`.
- **Now:** `SettingsModule`, `DiscordClient`, `GuildLog` and `GuildIssueReporter` are all singletons
  (`GrimoireServiceRegistration.cs:116, 220`, `SettingsServiceRegistration.cs:27`), but each tick resolves them from a new
  scope and threads them through every static method. Each `Record…UnlockAsync` validates a string literal with
  `ModerationReason.Create("Channel lock expired.")` and only then builds the unlock. That's T-4's second group, with a
  constant as the input.
- **Proposed:** primary-constructor parameters on both tasks, with instance methods. Well-known reasons as static
  members on the type, which can call its private constructor: `ModerationReason.LockExpired`,
  `ModerationReason.ChannelGone`. After S-12, each `Record…UnlockAsync` is a single call to
  `settingsModule.RemoveXLock(XUnlocked.Create(...), ct)`.
- **Size:** about −30 lines, 3 files.
- **Needs:** S-12 for the one-line unlocks. F-12 (the logger) goes with it.
- **Interacts with:** F-9, F-12, S-12, T-4.

#### MOD-9 `MuteRoleResolver` returns the role, not an id to look up again  `remove` · **low**
- **Where:** `MuteRoleResolver.Resolve` (`MuteRoleResolver.cs:17-20`), with callers at `UnmuteUser.cs:41-44`,
  `MuteBackgroundTasks.cs:71-84` and `UserJoinedWhileMutedEvent.cs:28-32`.
- **Now:** each caller passes `id => guild.Roles.ContainsKey(id.Value)`, gets a `RoleId?` back, and then indexes
  `guild.Roles[roleId.Value]` again.
- **Proposed:** `static DiscordRole? Resolve(RoleId? recorded, RoleId? configured, IReadOnlyDictionary<ulong, DiscordRole>
  roles)` using `TryGetValue`. It stays pure, so `MuteRoleResolverTests` passes a dictionary.
- **Size:** about −6 lines, 4 files plus the test.
- **Needs:** nothing.
- **Interacts with:** none.

#### MOD-10 Mute lookups: independent awaits, and a DB query before the cached gate  `perf` · **low**
- **Where:** `UnmuteUser.cs:37-40` (`GetActiveMute`, then `GetEffectiveMuteRole`) and
  `UserJoinedWhileMuted.HandleEventAsync` (`UserJoinedWhileMutedEvent.cs:21-25`), which runs on every member join.
- **Now:** `/Unmute` awaits a DB query and then a cached settings read that doesn't depend on it. On every join,
  `UserJoinedWhileMuted` queries the database for an active mute (no cache) *before* the cached module check. So
  guilds with Moderation off still pay a query per join.
- **Proposed:** `/Unmute` reads both with `Task.WhenAll` and uses `GetConfiguredMuteRole`, since the command is already
  module-gated. `UserJoinedWhileMuted` checks `IsModuleEnabled` first.
- **Size:** about ±0 lines, 2 files.
- **Needs:** nothing (simpler after F-1).
- **Interacts with:** F-1. Section 5 doesn't cover member joins.

#### MOD-11 `/Unban` renders its own outage message and leaks the handoff on other failures  `pipe` · **low**
- **Where:** `RemoveBanCommand.UnbanAsync` (`RemoveBanCommand.cs:33-53`).
- **Now:** a `ServerErrorException` gets a hand-written "error when communicating with discord" reply, which duplicates
  `CommandHandler`'s generic error. Any other exception, `UnauthorizedException` for example, leaves the
  `ExpectUnban` entry in place for up to 2 minutes. An outside unban of the same user in that window would then be
  credited to this moderator.
- **Proposed:** the same shape as `/Ban`: `catch (Exception) { CancelUnban; throw/return }`. Map `NotFoundException` to
  `new Error.NotFound($"{user.Username} was not unbanned because user is not banned.")`. Let everything else reach
  `CommandHandler` (F-8).
- **Size:** about −5 lines, 1 file.
- **Needs:** F-4, F-5.
- **Interacts with:** KI-09, KI-10, F-8.

#### MOD-12 `BanHandoff` keeps two copies of the same table  `remove` · **optional**
- **Where:** `BanHandoff` (`BanHandoff.cs:39-68`): `ExpectBan`/`TakeBan`/`CancelBan` and
  `ExpectUnban`/`TakeUnban`/`CancelUnban` over two `ConcurrentDictionary`s.
- **Now:** six methods that are pairwise identical apart from the value type.
- **Proposed:** a private generic `PendingTable<T>` with `Expect`/`Take`/`Cancel` and the freshness check, exposed as
  `Bans` and `Unbans`.
- **Size:** about −10 lines, 1 file plus 4 call sites.
- **Needs:** nothing.
- **Interacts with:** KI-03, KI-09.

#### MOD-13 `OverwriteChannelResult` is a success flag plus a channel  `state` `remove` · **low**
- **Where:** `Utilities/OverwriteChannelResult.cs`, `MuteAdminCommands.SetMuteRolePermissionsAsync`/`OverwriteChannelAsync`
  (`MuteAdminCommands.cs:50-81`), and the `.Where(x => !x.WasSuccessful)` in `Create.cs:47-49` and `Refresh.cs:34-36`.
- **Now:** every channel yields a result object, and both callers throw away the successes.
- **Proposed:** `SetMuteRolePermissionsAsync` yields only the channels it couldn't set (`IAsyncEnumerable<DiscordChannel>`),
  and `OverwriteChannelAsync` returns `bool` through `RetryDiscordCall(..., defaultValue: false)`. Delete the type.
- **Size:** about −18 lines, 4 files.
- **Needs:** nothing.
- **Interacts with:** KI-27.

#### MOD-14 Lock permissions go through `long.Parse(ToString())`  `remove` · **optional**
- **Where:** `LockChannel.ChannelLockAsync` (`LockChannel.cs:99-100`).
- **Now:** the lock bits are turned into a decimal string by `DiscordPermissions.ToString()` (a `BigInteger` in
  DSharpPlus) and parsed back with a culture-dependent `long.Parse`. The reverse direction already has its own
  extension, `RevertLockPermissions(long)` (`PermissionsExtensions.cs:34`).
- **Proposed:** `ToLockBits() : long` next to it in `PermissionsExtensions`, built with `InvariantCulture` or from the
  flags, so both directions sit in one place.
- **Size:** about ±0 lines, 2 files.
- **Needs:** nothing.
- **Interacts with:** none.

## C# 15

There are no `cs15` findings of this section's own. The hierarchies used here (`Mute`, `ChannelLock`, `ThreadLock`)
belong to section 2, and T-3 already marks them `closed`. Nothing in scope switches over them with a `_` arm. The only
`_` arm is the `DurationType` enum guard, which stays (T-4) and moves into `ModerationDuration.From` (MOD-5). There's
nothing for `[with(...)]`, labeled jumps or extension indexers: MOD-9 removes the double dictionary lookup that an
indexer would otherwise have covered.

## Checked, no finding

- **Deliberate choices kept.** `/Warn` keeps its channel-level `RequirePermissions` and has no rank check. Only
  `BanAddedEvent` writes ban sins, and `/Ban` waits on `PendingBan`.
- **Unlock loads the active lock twice.** `UnlockChannel.TryUnlockChannelAsync` reads it to revert, then
  `RemoveChannelLock` reads it again. The expiry task does the same for each item. The second read is the guard
  against a concurrent unlock, it's a primary-key lookup, and the command is rare. Keep it.
- **DSharpPlus lookups are cache-first.** `GetRoleAsync`, `GetMemberAsync` and `DiscordClient.GetChannelAsync` check
  the cache before REST, so `/Mute`'s role lookup and the expiry tasks' member and channel lookups cost no REST call in
  the common case.
- **The order in `/Ban` and `/Kick` (DM, then act) can't run in parallel.** After the action the bot no longer shares
  a guild with the member. `/Mute`'s DM and reply are independent, but T-1 sends the DM before the reply anyway
  (F-1 Question 5).
- **The mute-expiry DM is its own copy, not `DirectMessageAsync`.** Its failure is silent on purpose, because the guild
  log right after records the expiry anyway.
- **`/Mute` compensates** in `TryRecordMuteAsync` because the sin and the mute live in two contexts. A single
  transaction isn't available, so the undo stays (M-5, KI-04).

## Questions for the owner

1. **Refusals become ephemeral (MOD-1).** Today "You can't kick yourself.", the rank refusals, the missing mute role,
   "Channel not of valid type.", "is not locked", "could not be unlocked" and "is not banned" are public in the channel.
   Under T-1 only the moderator sees them. Is that what you want for all of them, including Unlock's "is not locked"?
2. **Blank lock reason (MOD-4).** `/Lock` refuses a whitespace-only reason, while `/Kick` and `/Mute` treat it as no
   reason. MOD-4 makes `/Lock` treat it as no reason too. OK?
3. **Expiry scan bound (MOD-6 step 3).** If the production plan is still history-bound after step 2, may an expired
   mute or lock that has failed for 90 days stop being retried? The alternative is a mutable "resolved" column, which
   breaks the append-only history.
4. **End time from a duration (MOD-5).** This moves the `endTime > setAt` invariant into a `ModerationDuration` type,
   so `MuteAdded`, `ChannelLocked` and `ThreadLocked` take `setAt + duration`. Section 2 owns those factories. Do you
   want them to change shape, or keep taking an end time and keep the check?
5. **Self-exemption in `ModerationRank` (MOD-2).** It allows acting on yourself, but every caller refuses that first.
   Can the exemption go, so that self-targeting is always refused for Ban, Kick and Mute?
