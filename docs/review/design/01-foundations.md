# Section 1: Foundations

Reviewed 2026-11-07, against the code as it will be once every fix in `review-decisions.md` is in.

The biggest win is a rule: `Result` carries only outcomes a user or caller can act on, and infrastructure failures stay
exceptions (F-1). Today the settings layer catches every exception and returns `Result.Fail`, and 72 callers then default
the failure away with `GetOrElse`. Next comes one command boundary, `ctx.RunAsync`, which validates before the defer and
renders every reply, error and log in one place (F-5). After that, `Result<T>` shrinks to two cases with `Validation<T>`
merged into it (F-2, F-3), and `Error` loses its string `Code` (F-4). Findings F-1 to F-13 follow the target shapes.

## Target shapes

Sections 2 to 11 measure code against these shapes. Each one starts with a short rule, then gives a sketch and the
reason for it. The sketches build on net10/C# 14 unless a block is marked **cs15**. A cs15 block needs `net11.0`, which
isn't available yet.

### Rules for the other sections

- If a command has an `if (...) { await ctx.SendErrorResponseAsync(...); return; }` chain, a `.Match(onOk, onFail)` that
  ends in a hand-written reply, or a `Reply`/log pair, measure it against T-1. Cite F-5 and give the feature's line
  delta. Don't report each command as a separate design finding.
- If a `.Match(x => x, _ => throw new UnreachableException())` unwraps a value, sort it with the table in T-4 and cite
  F-6 or F-7.
- If a `catch (Exception)` turns an outage into `Result.Fail`, or a `GetOrElse(() => false | null | null!)` hides one,
  cite F-1.
- If code produces `NotModified`, or switches on `Error.Code`, cite F-3 or F-4.
- For each `abstract record` hierarchy, choose `closed` or `union` with the rule at the end of T-3. Never add a `_`
  arm to a switch over a closed hierarchy. Use a `null` arm where null can arrive.
- In a background item that returns or drops a `Result`, cite F-9. In an event handler that uses `Result`/`Validation`
  for "this message isn't for me", cite F-10.

### T-1 Command shape: validate → (defer) → load/act → reply

**Rule.** `ctx` is used only inside the command method. That method builds a pure `Result<TIn>` from its parameters and
hands it to `ctx.RunAsync`. Nothing below the command method takes a `CommandContext`. The act step is an ordinary
private method. It takes ids and Discord entities, does the I/O and returns a `Reply`. Pure helpers build embeds and
make decisions. Exceptions keep going to `CommandHandler`, which uses the same private-reply path (F-8).

```csharp
// Grimoire/Extensions/CommandContextExtension.cs (target)
public enum Visibility { Public, Private }

/// <summary>What a command shows on success. Log, if set, is queued after the reply is sent.</summary>
public sealed record Reply(DiscordEmbed Embed, GuildLogMessageBase? Log = null)
{
    public static Reply Logged(DiscordEmbed embed, GuildId guildId, GuildLogType logType)
        => new(embed, new GuildLogMessageCustomEmbed { GuildId = guildId, GuildLogType = logType, Embed = embed });

    public static Reply Logged(DiscordColor color, string message, GuildId guildId, GuildLogType logType)
        => Logged(BuildReplyEmbed(color, message), guildId, logType);
}

public static partial class CommandContextExtension
{
    extension(CommandContext ctx)
    {
        /// <summary>
        ///     Validates, then defers, then acts, then replies. <paramref name="input" /> is computed before the
        ///     defer, so it must be pure. A failure there is answered ephemerally straight away, with no deferred
        ///     response to delete. A failure from <paramref name="act" /> takes KI-21's delete-then-follow-up path.
        /// </summary>
        public Task RunAsync<TIn>(Result<TIn> input, Func<TIn, Task<Result<Reply>>> act,
            Visibility visibility = Visibility.Public)
            => input.Match(
                async valid =>
                {
                    await ctx.DeferAsync(visibility);
                    await (await act(valid)).Match(ctx.SendReplyAsync, ctx.SendErrorAsync);
                },
                ctx.SendErrorAsync);

        /// <summary>For an act step that can't fail. The signature says so (principle 10).</summary>
        public Task RunAsync<TIn>(Result<TIn> input, Func<TIn, Task<Reply>> act,
            Visibility visibility = Visibility.Public)
            => ctx.RunAsync(input, async valid => Result.Ok(await act(valid)), visibility);

        /// <summary>For a command with nothing to validate.</summary>
        public Task RunAsync(Func<Task<Result<Reply>>> act, Visibility visibility = Visibility.Public)
            => ctx.RunAsync(Result.Ok(Unit.Value), _ => act(), visibility);

        private ValueTask DeferAsync(Visibility visibility)
            => ctx is SlashCommandContext slash
                ? slash.DeferResponseAsync(visibility is Visibility.Private)
                : ctx.DeferResponseAsync();

        private async Task SendReplyAsync(Reply reply)
        {
            await ctx.ReplyAsync(embed: reply.Embed); // edits the deferred response, which keeps its visibility
            if (reply.Log is { } log)
                await ctx.Client.ServiceProvider.GetRequiredService<GuildLog>().SendLogMessageAsync(log);
        }

        private async Task SendErrorAsync(Error error)
        {
            LogUserFacingError(ctx.Client.Logger, ctx.Command.FullName, ctx.Guild?.Id, error.Message);
            await ctx.SendPrivateResponseAsync(ColorOf(error), error.Message); // KI-21, unchanged
        }
    }

    private static DiscordColor ColorOf(Error error)
        => error is Error.NotFound ? GrimoireColor.Yellow : GrimoireColor.Red;
}
```

**cs15.** `ColorOf` becomes `error switch { Error.Invalid => Red, Error.NotFound => Yellow }`, with no `_` arm, so
adding a third `Error` case breaks the build until the renderer handles it.

**Why.** Today each of the 78 commands defers first and then decides on its own how to report a failure. Some use red
or yellow `SendErrorResponseAsync`/`SendWarningResponseAsync` (38 calls), some use a public red `ReplyAsync` (Rewards),
and 12 setting commands drop the result (I-4). Each also writes its reply and log pair by hand. With one boundary, the
command can't defer with one visibility and reply with another, because `Visibility` is set once. It can't forget the
error path, and it can't log before it replies. Validating before the defer also helps KI-21. Most user errors, such as
a bad reason, warning yourself, a role above the bot or a missing channel, are known before any I/O. Those get a plain
ephemeral response, and nobody sees a "thinking…" message appear and then vanish. I-4's `ReplyResultAsync` is the tail
of `RunAsync`, so build it once in this shape.

**Warn, rewritten.** This is `Moderation/Warn/Warn.cs` after D-2, M-7 and the `RequiredGuild` ruling. `Sin.ForWarn`
takes the reason and returns `Sin`, because ids from Discord aren't zero (D-5/S-12). `ModerationReason` arrives as a
parameter type (F-6).

```csharp
[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequirePermissions([], [DiscordPermission.ManageMessages])]
internal sealed class Warn(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    [Command("Warn")]
    [Description("Issue a warning to the user.")]
    public Task WarnAsync(CommandContext ctx,
        [Parameter("user")] [Description("The user to warn.")]
        DiscordUser user,
        [Parameter("reason")] [Description("The reason for the warn.")]
        ModerationReason reason)
        => ctx.RunAsync(
            Result.Ok(reason).Ensure(_ => ctx.User != user, new Error.Invalid("You cannot warn yourself.")),
            validReason => this.WarnAsync(ctx.RequiredGuild, ctx.User, user, validReason));

    private async Task<Reply> WarnAsync(DiscordGuild guild, DiscordUser moderator, DiscordUser user,
        ModerationReason reason)
    {
        var sin = Sin.ForWarn(new ModerationActor.Moderator(moderator.GetModeratorId()), user.GetUserId(),
            guild.GetGuildId(), DateTimeOffset.UtcNow, reason);
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Sins.Add(sin);
            await dbContext.SaveChangesAsync();
        }

        if (await guild.GetMemberOrDefaultAsync(user.GetUserId()) is { } member)
            await guildLog.DirectMessageAsync(member, new DiscordEmbedBuilder()
                .WithAuthor($"Warning Id {sin.Id}")
                .WithDescription($"You have been warned by {moderator.Mention} for {reason}")
                .WithColor(GrimoireColor.Yellow), "warn");

        return Reply.Logged(new DiscordEmbedBuilder()
            .WithAuthor("Warn")
            .AddField("User", user.Mention, true)
            .AddField("Sin Id", $"**{sin.Id}**", true)
            .AddField("Moderator", moderator.Mention, true)
            .AddField("Reason", reason.Value)
            .WithColor(GrimoireColor.Yellow)
            .WithTimestamp(sin.SinOn), guild.GetGuildId(), GuildLogType.Moderation);
    }
}
```

**Line count.** I wrote both versions out and counted them, header and usings included.

| Version | Lines | Branches | Early returns | `UnreachableException` |
|---|---|---|---|---|
| Today | 87 | 3 | 1 | 2 |
| After D-2, M-7 and `RequiredGuild` | about 72 | 3 | 2 | 1 |
| Target | 56 | 1 (the member lookup) | 0 | 0 |

Most of the cut from 87 comes from the decided fixes. D-2 alone removes the 20-line `SinReasonHistory` block. The shape
itself saves about 16 lines here, plus the shared parts: the `ModerationReason` converter, which is one line per type
on a base class (F-6), and `RunAsync`, which is about 45 lines written once. Warn has only one error path, so it's a
modest showcase. Commands with three or four error paths, such as the settings setters and `/Mute`, save more. The
behaviour changes in one place: the DM is now sent before the reply instead of after it. The command is deferred, so
the user only waits for the DM call.

**The other two samples.** Rewards Add, after X-6 (Moderation log, says who did it):

```csharp
public Task AddAsync(CommandContext ctx, DiscordRole role, [MinMaxValue(0, int.MaxValue)] int level,
    [MinMaxLength(maxLength: 4096)] string? message = null)
    => ctx.RunAsync(
        BotCanGrant(ctx.RequiredGuild, role)
            .Bind(_ => RewardMessage.CreateIfNotNull(message))
            .Bind(validMessage => RewardAdded.Create(role.GetRoleId(), ctx.RequiredGuild.GetGuildId(), level,
                validMessage, ctx.GetModeratorId(), DateTimeOffset.UtcNow)),
        reward => this._settingsModule.SetRewardAsync(reward)
            .Map(saved => Reply.Logged(GrimoireColor.DarkPurple,
                $"{ctx.User.Mention} updated the rewards to include {role.Mention} at level {saved.RewardLevel}.",
                saved.GuildId, GuildLogType.Moderation)));

private static Result<DiscordRole> BotCanGrant(DiscordGuild guild, DiscordRole role)
    => guild.CurrentMember.Hierarchy < role.Position
        ? new Error.Invalid($"{guild.CurrentMember.DisplayName} will not be able to apply this reward role "
                            + "because the role has a higher rank than it does.")
        : role;
```

This file goes from 87 lines to about 45: `OnFail` and `OnAddSuccess` go. The hierarchy check now runs before the
defer, so it's ephemeral, and its "Was not able to update the rewards for the server:" prefix goes. That prefix is
pinned by `RewardCommandsTests.Add_ARoleAboveTheBot_IsRefused`. Rewards View uses the no-input overload. X-5's
`GetOrElse(() => null!)` becomes a `.Map` there.

`CustomCommandSettings.Learn` fits the shape too. `CustomCommandContent` becomes a parameter type, as
`CustomCommandName` already is. `BuildRoles` (with P-1's deny arm) and the `Create` call make up the input. After C-6
and D-5, the save step returns a plain `Task<Reply>` whose `Log` is a `GuildLogMessage` with its own description,
because Learn's log text differs from its reply. The ten role parameters are most of that file, and the shape doesn't
change them.

**Where the shape doesn't fit.** Paged replies (`SinLog`, `/Commands History`), autocomplete providers and modals. These
keep calling the primitives (`ReplyAsync`, `FollowUpAsync`) directly. If three or more commands end up needing pages,
give `Reply` a `Pages` list. Until then, leave it out.

### T-2 Error model

**Rule.** An `Error` is a user-facing reason, and the case says what kind of reason it is. There is no string code and
no target. The renderer switches on the case.

```csharp
// net10
public abstract record Error(string Message)
{
    /// <summary>The request breaks a rule and must change. Rendered red.</summary>
    public sealed record Invalid(string Message) : Error(Message);

    /// <summary>The thing it's about doesn't exist or isn't in that state: no such sin, not muted, no xp. Yellow.</summary>
    public sealed record NotFound(string Message) : Error(Message);
}
```

```csharp
// cs15: only the modifier changes, and outside assemblies can no longer derive from it
public closed record Error(string Message)
{
    public sealed record Invalid(string Message) : Error(Message);
    public sealed record NotFound(string Message) : Error(Message);
}
```

**Why.** No production code needs `Code` except `TextCustomCommandProcessor`. Its `switch (error.Code)` picks a log
line for a case that shouldn't be an error in the first place (F-10). `GainUserXp` also logs `error.Code`, but that's
for an outage, which F-1 turns into an exception. `Target` has one use, a command name, which is in scope where the
lookup happens. The 173 `new Error("some.code", "…")` calls each carry a string that nothing reads. Two cases cover the
red/yellow split that call sites now choose by hand (24 error calls, 11 warning calls), and they choose
inconsistently. "Auto pardon can't be longer than 100 years" is invalid input but shows yellow. I found no `Forbidden`
or `Conflict` case worth bringing back (D-3 deletes them). A missing bot permission is an exception that
`CommandHandler` already reports. `NotModified` isn't a failure (T-3). About 60 test assertions check codes. They become
`ShouldBeOfType<Error.Invalid>()` plus the message, which is what the user sees anyway (see Questions).

### T-3 `Result<T>`, and `Validation<T>` merged into it

**Rule.** There is one result type with two cases, `Ok(T)` and `Fail(Error)`. A write that changed nothing is still a
success. If the caller needs to know that nothing changed, the success value says so: for example, the empty set of
items that were actually added. `Validation<T>` goes, because nothing accumulates errors.

```csharp
// net10 interim: no switch, so no `_ => throw new UnreachableException()`. Each case implements Match.
public abstract record Result<T>
{
    private Result() { } // only the nested cases can derive

    public sealed record Ok(T Value) : Result<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> ok, Func<Error, TOut> fail) => ok(this.Value);
    }

    public sealed record Fail(Error Error) : Result<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> ok, Func<Error, TOut> fail) => fail(this.Error);
    }

    public abstract TOut Match<TOut>(Func<T, TOut> ok, Func<Error, TOut> fail);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder) => this.Match(binder, error => error);
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) => this.Bind(value => Result.Ok(mapper(value)));
    public Result<T> Ensure(Func<T, bool> predicate, Error error) => this.Bind(value => predicate(value) ? this : error);
    public Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> binder)
        => this.Match(binder, error => Task.FromResult<Result<TOut>>(error));
    public T GetOrElse(Func<T> fallback) => this.Match(value => value, _ => fallback());

    public static implicit operator Result<T>(T value) => new Ok(value);
    public static implicit operator Result<T>(Error error) => new Fail(error);
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new Result<T>.Ok(value);
}
// ResultTaskExtensions keeps the Task<Result<T>> forms of Map, Bind, BindAsync, TapAsync and Match that are used.
```

```csharp
// cs15 final: `closed` replaces the private constructor, and callers get exhaustive switches.
public closed record Result<T>
{
    public sealed record Ok(T Value) : Result<T>;
    public sealed record Fail(Error Error) : Result<T>;

    public TOut Match<TOut>(Func<T, TOut> ok, Func<Error, TOut> fail) => this switch
    {
        Ok(var value) => ok(value),
        Fail(var error) => fail(error),
    };
    // Bind, Map, Ensure, BindAsync, GetOrElse and both conversions are unchanged.
}
```

**Why two cases.** After D-3 there are four cases. `NotModified` is produced in six places, and nothing in `Grimoire`
reads it. I-4 already decided to treat it as success, and X-12 needs the "nothing changed" information as data, so it
belongs in the success value. No caller passes the optional `onNotFound` handler (D-3). The only code that tells
`Invalid` apart from the rest is `MuteBackgroundTasks.cs:67`, and it does that to turn an outage back into an
exception (F-1). The kind of failure matters only to the renderer, so it lives on `Error` (T-2), not on `Result`. With
two cases, every combinator is a one-line call to `Match`. `Result.cs` goes from 170 lines to about 50, and its 8
unreachable throws go now, on net10.

**Why the implicit conversions.** `return role;` and `return new Error.Invalid("…");` replace
`Result<T>.Ok(...)`/`Result<T>.Fail(...)` and `Validation<T>.Succeed(...)`/`.Fail(...)`. That covers most of the 383
`Validation<` and 268 `Result<` mentions. C# doesn't apply a user-defined conversion from an interface type, so
`Result<IReadOnlySet<X>>` still needs `Result.Ok(set)`. That's a compile error, not a runtime one.

**Why not a `union`.** A `union` lowers to a struct, so `default(Result<T>)` holds null contents. The skill notes that
this switch throws `SwitchExpressionException` at run time, which is D-7's trap again. The cases also need wrapper
records anyway. `union Result<T>(T, Error)` can't tell `Ok(null)` from empty for `Result<ChannelId?>`. With
`union Result<T>(Ok<T>, Error)`, the automatic conversion only comes from `Ok<T>`, not from `T`, so it saves nothing
over the operators above. We own both cases and they share `Map`/`Bind`. That is the skill's description of a closed
hierarchy.

**Why merge `Validation<T>`.** It exists to collect several errors, and no production code does. No call site builds a
`Validation` with more than one error. Every list of errors is joined into one string before anyone sees it
(`Validation.ToResult`'s `CombineErrors`, and `SendErrorResponseAsync(IEnumerable<Error>)`). Fifteen `.ToResult()`
calls exist only to cross between the two types. The one `Combine` caller goes with S-1, and the one `Result.WhenAll`
caller goes with F-1. If a real need to collect errors turns up, add `Result.All(...)` then, which follows the strategy
doc's own phase-5 rule.

**Closed or union, for section 2 and the rest.** Use `closed record` when we own every case and the cases share members
or state. That covers `Result`, `Error`, `ModerationActor`, `CachedSetting`, `XpIgnoredItem`, the lock and mute event
records, and `GuildLogMessageBase`. Use `union` only to combine types we don't own or that belong to other families.
Section 1 has none. With either, use `null` arms, never `_` arms.

### T-4 Unwrapping values already known to be valid (the 45 `UnreachableException`s)

**Rule.** Don't add an `Unwrap()` or `OrThrow()`. It moves the throw somewhere else without removing it. Make the type
carry the proof instead. Here are the 45, grouped by what removes them:

| Group | Count today | Sites | Removed by |
|---|---|---|---|
| Combinator arms on `Result`/`Validation` | 16 | `Result.cs` 8, `Validation.cs` 6, `ResultCombinators.cs` 1, `ValidationTaskExtensions.cs` 1 | T-3 (F-2, F-3), net10. D-4 deletes one file first |
| A smart constructor unwrapped on input known to be valid | 15 | `Warn` 2, `KickUser` 2, `MuteUser` 2, `SpamEvents` 2, `BanAddedEvent` 2, `UpdateSinReason` 1, `GainUserXp` 1, `AwardUserXp` 1, `ReclaimUserXp` 1, `DiscordMessageExtensions.GetMessageContent` 1 | D-2 and M-7 take most of them. The rest: parameter types (F-6), total conversions and typed producers (F-7) |
| `_` arm on one of our own hierarchies | 8 | Locks 2 (S-10), `CustomCommand` 2 (D-8), Ignores 2, AutoPardon 1, `UserExtensions.Mention(ModerationActor)` 1 | Decided fixes take 4. **cs15** `closed` takes the other 4 (sections 2 and 4) |
| `_` arm on an enum | 6 | `ModuleCommands.Set`, `SinLog`, `GetLeaderboard`, `SetLevelSettings`, `SettingsModule.Levelsettings`, `GetChannelOption` | Keep them. C# enums are open, so these guards are correct. Only `SinLog`'s "Handled above" is a restructuring (section 8) |

Four techniques remove the second group:

1. **Take the validated type as the parameter.** A DSharpPlus converter does the `Create`, so the command never sees
   a raw string. `CustomCommandName` and `CustomCommandEmbedColor` already work this way. F-6 extends it to
   `ModerationReason`, `CustomCommandContent`, `RewardMessage` and `PositiveXpAmount`.
2. **Total conversions between validated types.** `PositiveXpAmount.Negate() : NegativeXpAmount`, and
   `XpGainAmount.AsPositiveXp()` (or `EarnedXp` takes `XpGainAmount`). The source type already proves the invariant.
3. **Producers return the validated type.** `SpamTrackerModule` returns a `ModerationReason`, not a string.
   `FetchXpToTakeAsync` returns `Result<PositiveXpAmount>`.
4. **A trusted factory for data from Discord or the database.** `FromDiscord` and `FromDatabase`, as decided in D-6
   and S-1. `GetMessageContent` is a sixth case of D-6, since Discord caps content at 4,000 characters.

Once the decided fixes and T-3 are in, the `UnreachableException` count falls from 45 to the 6 enum guards plus the 4
cs15 ones, and then to 6 once `closed` lands.

### T-5 Background-service failure shape

**Rule.** A tick or an item fails in one of two ways. It can return a `Fail`, which is expected: the item is retried
with backoff, a warning is logged, and the failure goes into the daily report. Or it can throw, which is unexpected
and is handled the same way, with urgent escalation after three failed ticks in a row, as now. The work never drops a
`Result`.

```csharp
// GenericBackgroundService (target). The logger is the subclass's own (F-12).
protected async Task ProcessItemAsync(string item, Func<Task<Result<Unit>>> work, CancellationToken cancellationToken)
{
    if (!this._backoff.ShouldAttempt(item))
        return;

    try
    {
        if (await work() is Result<Unit>.Fail(var error))
        {
            this.RecordItemFailure(item, error.Message, exception: null);
            return;
        }

        this._backoff.RecordSuccess(item);
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
        // Missing permissions are expected and the work reports them to the guild, so no alert for those.
        this.RecordItemFailure(item, ex.Message, ex is UnauthorizedException ? null : ex);
    }
}

// LockBackgroundTasks: the unlock write's result is now the item's result
return await RecordChannelUnlockAsync(expiredLock, "Channel lock expired.", settingsModule, discordClient, ct)
    .TapAsync(_ => SendLockExpiredLogAsync(channel, guildLog, ct))
    .Map(_ => Unit.Value);
```

**cs15.** Nothing here needs it.

**Why.** Today `ProcessItemAsync(string, Func<Task>, ...)` treats anything that doesn't throw as success. The lock and
mute tasks call settings writes that return `Result`, and they drop the result (F-9). `Alert`, `AlertService` and the
tick loop are fine as they are. `Alert.Type` is a string, but each of the ten types is written in exactly one place and
is stored as text in `AlertOccurrence`, so an enum would only add lines.

## Findings

#### F-1 Infrastructure failures stay exceptions; `Result` is for domain outcomes  `pipe` · **high**
- **Where:** `Grimoire.Settings/Services/SettingsModule.cs:143` (`ExecuteSafelyAsync`, 15 users), the
  `catch (Exception ex)` blocks at `SettingsModule.cs:59`, `:87` and `:128` (`GetGuildSetting`, `GetGuildSettings`,
  `SetGuildSetting`), and `SettingsModule.Leveling.Levelsettings.cs` (`GetLevelingSettings`). That is 10
  `catch (Exception)` in Settings in all. Consumers include 72 `GetOrElse(() => false | null | null!)` in `Grimoire`,
  `MuteBackgroundTasks.cs:67`, `GainUserXp.cs:76` and `ResultCombinators.cs:57` (`Result.WhenAll`).
- **Now:** a database or cache outage becomes `Result.Fail` with a friendly message. Callers then either default it
  away (29 `=> false`, 34 `=> null`), crash on `null!` (I-5, X-5), or turn it back into an exception
  (`MuteBackgroundTasks.cs:67`). Only the settings logger sees the outage. No alert is raised.
- **Proposed:** settings reads return plain values (`Task<bool>`, or `ValueTask<bool>` where the cache usually hits).
  Writes return the written value, or `Result<T>` only where there's a domain rule. Delete `ExecuteSafelyAsync`, the
  ten catches and `Result.WhenAll` (`GetAllModuleState` becomes a `Task.WhenAll` of plain values). The three
  boundaries already log and alert exceptions: `CommandHandler` with an error id, `IClientErrorHandler` for events, and
  `GenericBackgroundService`. This is the rule C-6 already adopted for custom commands. The behaviour during an outage:
  commands show the generic error with an id. Event handlers don't run, so a member gets no XP, which is the same
  outcome S-4's "counts as ignored" was meant to give. Settings already cached keep being served for their day.
- **Size:** about −150 lines in Settings. The 72 `.GetOrElse(...)` tails become plain awaits. The +40 lines S-4 would
  add to wrap eight more methods aren't needed.
- **Needs:** an owner decision, because it reverses how S-4 is done (see Questions).
- **Interacts with:** S-4, S-2 (fixed by this, since the failing cache factory no longer caches defaults), I-4, I-5,
  X-5, C-6, M-5.

#### F-2 Merge `Validation<T>` into `Result<T>`  `remove` · **med**
- **Where:** `Grimoire.Domain/Validation.cs`, `ValidationCombinators.cs`, `ValidationTaskExtensions.cs` and
  `ResultCombinators.cs`, `CommandContextExtension.cs:83` (`SendErrorResponseAsync(IEnumerable<Error>)`), and 15
  `.ToResult()` calls.
- **Now:** two types with the same two outcomes. Nothing builds more than one error. Every error list is joined into
  one string before display.
- **Proposed:** T-3. Factories return `Result<T>`. Rename the 383 `Validation<` mentions in 58 files mechanically.
  Delete the four files.
- **Size:** about −350 lines in `Grimoire.Domain`. At call sites the line count stays about the same, but each line
  gets shorter. `ValidationTests` and `ValidationCombinatorTests` go.
- **Needs:** F-3. S-1 for `Combine`'s only caller, and F-1 for `WhenAll`'s.
- **Interacts with:** D-3, D-4, S-1, S-7. It reverses `error-result-validation-strategy.md`'s "don't unify" rule,
  which D-3 already says needs updating.

#### F-3 `Result<T>` with two cases and `Match` on each case  `pipe` `state` · **med**
- **Where:** `Grimoire.Domain/Result.cs:12-170` and `ResultTaskExtensions.cs`. `NotModified` is produced in six places,
  for example `SettingsModule.cs:119` and `SettingsModule.Moderation.DiagnosticsIgnores.cs:48`.
- **Now:** six cases, four after D-3. Each combinator repeats every failure arm and ends with
  `_ => throw new UnreachableException()`, 8 times in all. `NotModified` sometimes means "unchanged" and sometimes
  "you passed nothing" (`ValidateNotEmpty`, `DiagnosticsIgnores.cs:48`), and nothing reads it.
- **Proposed:** the net10 sketch in T-3 now, and `closed` with net11. An unchanged write returns `Ok`. A no-op that the
  user should hear about is part of the value (the items actually added). "Nothing was given" becomes
  `Error.Invalid`. Every `NotFound` becomes `Error.NotFound`.
- **Size:** `Result.cs` goes from 170 lines to about 50, and `ResultTaskExtensions.cs` from 72 to about 30. Six
  producers change one line each.
- **Needs:** nothing for net10. `net11.0` for the `closed` step, which is three lines.
- **Interacts with:** D-3 (this replaces its "one private conversion"), D-4, I-4 (`NotModified`-as-success becomes
  automatic), X-12.

#### F-4 Drop `Error.Code` and `Error.Target`; put the kind in the case  `state` · **med**
- **Where:** `Grimoire.Domain/Error.cs:10`, 173 `new Error(` sites, `TextCustomCommandProcessor.cs:59`
  (`switch (error.Code)`) and `GainUserXp.cs:76`.
- **Now:** every error carries a dotted code string, and one place dispatches on it. Whether an error shows red or
  yellow depends on which method the call site picked.
- **Proposed:** T-2. The renderer chooses the colour from the case.
- **Size:** about +6 lines in `Error.cs`. The 173 sites lose their code string and keep their line count. About 60
  test assertions change from `Code` to the case and the message.
- **Needs:** F-3. F-10 for the one production use of `Code`.
- **Interacts with:** none in `review-decisions.md`.

#### F-5 One command boundary: `ctx.RunAsync` and `Reply`  `pipe` `state` · **high**
- **Where:** `Grimoire/Extensions/CommandContextExtension.cs:18-85` (`ReplyAsync`, `SendErrorResponseAsync`,
  `SendWarningResponseAsync`). Uses across `Features`: 24 `SendErrorResponseAsync`, 11 `SendWarningResponseAsync`, 9
  ephemeral-defer branches, 11 private `On…`/`OnFail` helpers that take `ctx`, and 6
  `GetChannelOption … is Invalid { Errors: var e }` blocks.
- **Now:** each command defers, then chooses its own error method, colour and visibility, writes its reply and log
  pair by hand, and returns early. Rewards replies to failures publicly, and 12 setting commands drop them (I-4).
- **Proposed:** T-1. `GetChannelOption`'s result goes straight in as `RunAsync`'s input, which removes about 5 lines
  at each of its 6 callers. `SendWarningResponseAsync` and the `IEnumerable<Error>` overload go. After that,
  `SendErrorResponseAsync` is only used by commands that don't fit the shape.
- **Size:** about +45 lines in `CommandContextExtension.cs` and −20 lines there. Per command, between −5 and −35
  lines (Warn −16 after the fixes, Rewards Add −40). Sections 7 to 11 give the per-feature totals.
- **Needs:** F-3 and F-4. F-6 makes the inputs smaller but isn't required.
- **Interacts with:** I-4 (`ReplyResultAsync` is the tail of `RunAsync`, so build it once in this shape), I-19
  (`DeferPrivateAsync` becomes `Visibility.Private`), KI-21 (kept, and validation errors no longer need the delete
  step), I-5, X-5, X-6 and the `RequiredGuild` ruling. A test pins the Rewards error text:
  `RewardCommandsTests.Add_ARoleAboveTheBot_IsRefused`.

#### F-6 Validated parameter types through one converter base  `state` `pipe` · **med**
- **Where:** `Moderation/Warn/Warn.cs` (`string reason`), plus `KickUser`, `MuteUser`, `UpdateSinReason`, `PardonSin`,
  `AwardUserXp` (`int xpToAward` with `[MinMaxValue(1)]`), `RewardCommandGroup.AddReward` (`string? message`) and
  `CustomCommandSettings.Learn` (`string content`). Also `CommandNameArgumentConverter.cs:27`, where `FromNoValue`
  throws away the reason the conversion failed.
- **Now:** commands take strings and call `Create` in the body, either with a hand-written error branch or with an
  unreachable throw. The existing converters drop the validation message.
- **Proposed:**
  ```csharp
  // Grimoire.Domain
  public interface IValidated<TSelf> where TSelf : IValidated<TSelf>
  {
      static abstract Result<TSelf> Create(string input);
  }

  // Grimoire
  public abstract class ValidatedConverter<T> : ISlashArgumentConverter<T>, ITextArgumentConverter<T>
      where T : IValidated<T>
  {
      public DiscordApplicationCommandOptionType ParameterType => DiscordApplicationCommandOptionType.String;
      public ConverterInputType RequiresText => ConverterInputType.Always;
      public abstract string ReadableName { get; }

      public Task<Optional<T>> ConvertAsync(ConverterContext context)
          => Task.FromResult(context.Argument is string text
              ? T.Create(text).Match(Optional.FromValue, error => throw new ArgumentValidationException(error))
              : Optional.FromNoValue<T>());
  }

  public sealed class ModerationReasonConverter : ValidatedConverter<ModerationReason>
  {
      public override string ReadableName => "Reason";
  }
  ```
  `CommandHandler`'s `ArgumentParseException` arm shows `ConversionResult.Error?.Message` when it is an
  `ArgumentValidationException`. That reply goes out before any defer, so it is ephemeral without the KI-21 delete.
  The throw sits at a framework edge whose API only offers exceptions or "no value".
- **Size:** about +35 lines (the base, the exception, and one short converter per type). About −40 lines in commands.
  The two existing converters shrink to a few lines each.
- **Needs:** a spike against DSharpPlus 5.0.0-nightly-02614. The XML docs say `ArgumentFailedConversionResult.Error`
  holds "the exception that occurred during conversion". Check that it's really passed through, and that
  `[MinMaxLength]` still reaches Discord for a converted string option. If either fails, keep `Create` in the input
  pipeline (T-1 works either way).
- **Interacts with:** M-7 (the converter refuses blank reasons instead of a check in each command), D-2, KI-32.

#### F-7 The remaining unwraps: total conversions and typed producers  `state` · **low**
- **Where:** `GainUserXp.BuildEarnedXp` (`GainUserXp.cs:105`), `ReclaimUserXp.PersistReclaimAsync` (`:97`),
  `AwardUserXp.AwardAsync` (`:40`), `DiscordMessageExtensions.GetMessageContent` (`:19`), and the reason `Create` that
  D-2 leaves in `SpamEvents` and `BanAddedEvent`.
- **Now:** each one calls a validating factory on a value it already knows is valid, and throws
  `UnreachableException` on the impossible branch.
- **Proposed:** the four techniques in T-4. `XpGainAmount.AsPositiveXp()`. `PositiveXpAmount.Negate()`, with
  `FetchXpToTakeAsync` returning `Result<PositiveXpAmount>`. Award takes `PositiveXpAmount` (F-6).
  `MessageContent.FromDiscord`, following D-6. `SpamTrackerModule` returns `ModerationReason`. For the audit-log ban
  reason, use `CreateIfNotNull`, where a blank reason means no reason, which is a real fallback.
- **Size:** about −15 lines and 6 throws, across 7 files.
- **Needs:** D-2, D-5, D-6.
- **Interacts with:** D-5, D-6, D-2, X-13.

#### F-8 `CommandHandler` renders errors by different rules from KI-21  `pipe` · **med**
- **Where:** `Grimoire/Features/Shared/CommandHandler.cs:148` (`SendOrEditMessageAsync`), called from all four branches
  of `HandleEventAsync(CommandErroredEventArgs)`.
- **Now:** if the command has follow-ups, it edits the response. Otherwise a slash command gets
  `RespondAsync(embed, true)`. After a public defer with no follow-ups, that calls `RespondAsync` on an interaction
  that has already been acknowledged. I didn't confirm what DSharpPlus does there. Either the exception's
  "[ID …]" message lands in the public deferred response, or the call throws and the user is left with "thinking…".
  Either way, it differs from `SendPrivateResponseAsync`, which deletes the deferred response and follows up
  ephemerally.
- **Proposed:** make `SendPrivateResponseAsync` internal, taking an embed, and call it from `CommandHandler`. Delete
  `SendOrEditMessageAsync`. Every failure then goes through one renderer.
- **Size:** about −9 lines, 2 files.
- **Needs:** nothing.
- **Interacts with:** KI-21. Section 6 shouldn't re-report this.

#### F-9 Background items drop `Result` failures  `pipe` · **med**
- **Where:** `LockBackgroundTasks.ProcessExpiredChannelLockAsync`/`ProcessExpiredThreadLockAsync`
  (`LockBackgroundTasks.cs:89`, `:116`), `MuteBackgroundTasks.RemoveMuteAsync` (`MuteBackgroundTasks.cs:134`, which
  returns `Task<Result<MuteAdded>>` as a plain `Task`) and `GenericBackgroundService.ProcessItemAsync`
  (`GenericBackgroundService.cs:72`). These run every 5 seconds.
- **Now:** a `Fail` from the unlock write skips the log, records success, and leaves the expired lock in place. So the
  revert runs again 5 seconds later, with no backoff and no alert. Once S-4 wraps `RemoveMute` in `ExecuteSafelyAsync`,
  a failed `RemoveMute` sends the "mute expired" DM and guild log again on every tick until the write succeeds. On
  later ticks the role is already gone, so nothing stops the loop.
- **Proposed:** T-5. `ProcessItemAsync` takes `Func<Task<Result<Unit>>>`, and the work returns the write's result.
  Where `NotFound` means "already done" (the lock was already removed), the work maps it to `Ok`. Without F-1, the
  `throw new InvalidOperationException` at `MuteBackgroundTasks.cs:68` also becomes `return` of that result.
- **Size:** about +6 lines in the base and about ±0 in the tasks. 3 files.
- **Needs:** F-3. With F-1, only the domain-outcome half remains.
- **Interacts with:** S-4 (which makes the mute half real), S-10, M-6.

#### F-10 "This message isn't for me" is not an error  `perf` `pipe` · **med**
- **Where:** `TextCustomCommandProcessor.cs:30-65`, on every `MessageCreated`.
- **Now:** every message goes through `Validation`/`Result`. A message that isn't a command allocates `Error`,
  `ImmutableArray`, `Validation.Invalid` and `Result.Invalid` records, runs `Distinct().ToArray()` in `ToResult`, and
  passes through two `TapAsync` state machines. That's roughly ten allocations per message, just to reach
  `switch (error.Code)` and choose which log line to skip.
- **Proposed:** return early with a pattern, as in
  `if (ev.Message is not { MessageType: Default or Reply, Author: DiscordMember { IsBot: false } member, Content: ['!', ..] }) return;`.
  Then run the pipeline only for real commands, and log "not found" in `FetchCommandAsync`, where the name is in
  scope. This is the general rule for event handlers: skip with `is not` and `return`, and keep `Result` for outcomes.
- **Size:** about −15 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** C-9. Sections 5 and 11 should cite this rather than re-report it.

#### F-11 Make CS8509 a build error before relying on exhaustive switches  `state` `cs15` · **low**
- **Where:** each `*.csproj` (`<WarningsAsErrors>Nullable</WarningsAsErrors>`, repeated in six files).
- **Now:** a switch expression that misses a case is only a warning. Once cs15 removes the `_` arms, a new case added to
  a closed hierarchy gives warnings instead of build failures, which swaps a runtime throw for a warning that's easy
  to miss.
- **Proposed:** set `<WarningsAsErrors>Nullable;CS8509</WarningsAsErrors>` once in a `Directory.Build.props`, and drop
  the six copies. Do this before the cs15 step. First check the current build for CS8509 warnings.
- **Size:** about +8 / −6 lines, 7 files.
- **Needs:** nothing (do it before `net11.0`).
- **Interacts with:** none.

#### F-12 Background services log under the base class's category  `remove` · **low**
- **Where:** `GenericBackgroundService.cs:19` (`ILogger<GenericBackgroundService> logger`), `DailyWarningReportService.cs:17`,
  `GatewayWatchdogService.cs:16`, and `WarningFlushService.cs:17-20`, which takes `baseLogger` and `logger`, both
  `ILogger<WarningFlushService>`.
- **Now:** two services inject `ILogger<GenericBackgroundService>`, so their logs aren't filed under their own
  category. One service injects the same logger twice.
- **Proposed:** the base takes `ILogger`, and every subclass passes its own `ILogger<Self>`. `ILogger<T>` is
  covariant, so the current subclasses already compile. Delete `baseLogger`.
- **Size:** about −3 lines, 4 files.
- **Needs:** nothing.
- **Interacts with:** none.

#### F-13 `ReplyAsync`'s optional parameters pick modes  `state` · **optional**
- **Where:** `CommandContextExtension.ReplyAsync` (`CommandContextExtension.cs:18-39`).
- **Now:** if `embed` is passed, `color`, `message`, `title`, `footer` and `timeStamp` are silently ignored.
  `ephemeral` only takes effect before the interaction has been acknowledged.
- **Proposed:** two overloads, `ReplyAsync(DiscordEmbed)` and `ReplyAsync(DiscordColor, string message, string title = "")`,
  with no `ephemeral`. Visibility is set at the defer (T-1). Once F-5 is in, most commands no longer call `ReplyAsync`.
- **Size:** about ±0 lines, 1 file plus call sites.
- **Needs:** F-5 to be worth doing.
- **Interacts with:** I-19.

## Questions for the owner

1. **F-1 against S-4.** S-4 decided to wrap eight more Settings methods in `ExecuteSafelyAsync`, so outages become
   `Result.Fail`. F-1 goes the other way and deletes `ExecuteSafelyAsync`, letting outages reach the boundaries that
   already alert on them, as C-6 does for custom commands. The XP outcome is the same either way: no XP during an
   outage. Commands show the generic "[ID …]" error instead of a friendly one, and outages raise alerts instead of
   only writing a log line. Which do you want? Everything else in this section works either way. F-9 matters more if
   S-4 stands.
2. **Merging `Validation<T>` (F-2).** `error-result-validation-strategy.md` says to keep the two types apart. In
   practice nothing collects errors. Merge them, or keep `Validation` for when error collection is wanted later?
3. **Dropping `Error.Code` (F-4).** About 60 unit-test assertions name a specific code. Without codes they would check
   the case and the message. Is that acceptable, or do you want codes kept for tests?
4. **Two colours.** Should `Error.Invalid` show red and `Error.NotFound` yellow, or should every refusal use one colour
   and let the `Error` cases exist only where code branches on them? With one colour, `Error` could be a single sealed
   record.
5. **Warn's DM before the reply (T-1).** In the target shape the moderator's reply waits for the DM call. Is that
   fine, or should `Reply` carry an "after reply" effect for DMs? That would add about 10 lines to the boundary.
6. **F-6 spike.** If DSharpPlus doesn't pass the converter's exception through, would you accept DSharpPlus's generic
   parse message for blank reasons, or keep `ModerationReason.Create` in each command's input pipeline?
