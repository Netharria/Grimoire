# Architecture

The current description of how Grimoire's code is put together: the layers, effects and runtimes, the error model,
the boundaries, and how each is tested. It's kept up to date as the language-ext migration
([`langext-migration/plan.md`](langext-migration/plan.md)) lands. Code that hasn't been ported yet still works the
old way (EF Core queries in command bodies, exceptions to `CommandHandler`); the plan lists which section ports what.

## 1. Layers

```
DSharpPlus ──► shell: [Command] method, IEventHandler, EffectBackgroundService
                 │ copies what the core needs into Grimoire records (Invocation, event records)
                 ▼
               core: records, smart constructors, pure decisions, embed builders
                 │
                 ▼
               Eff<RT, A> pipelines over the capabilities (built, not run)
                 │
                 ▼
               boundary: CommandRunner / EventRunner / the worker's schedule runs it with a runtime
                 │
                 ▼
               capabilities (IO<A>): the only code that touches DSharpPlus, the database, the cache
```

- **Shell.** Thin classes DSharpPlus calls. They copy the arguments into Grimoire records and hand an effect to a
  runner. Discord types a test can't construct (`DiscordMember`, `DiscordGuild`, `DiscordChannel`, `DiscordMessage`)
  stay here (plan C5).
- **Core.** Pure code: records, `Create` smart constructors returning `Validation<Error, T>` when they can fail
  (TD-5), decisions, and embeds. Builder types such as `DiscordEmbedBuilder` are allowed (C5).
- **Effects.** A feature's work is an `Eff<RT, A>`: a description that runs only when a boundary runs it.
- **Capabilities.** One interface per kind of side effect, each method returning `IO<A>`. Live implementations
  are the imperative shell; unit tests use recording fakes.

Code lives in `Grimoire/Functional/`: `Capabilities/`, `Runtime/`, `Effects/`, `Boundary/` and `Workers/`.

## 2. Effects and runtimes

| Capability | Effect module | What it does | Section |
|---|---|---|---|
| `ITimeIO` | `Clock<RT>` | Wall-clock time and monotonic timestamps | 1.2 |
| `ILogIO` | `Log<RT>` | Structured log lines (message templates, so Serilog keeps the properties) | 1.2 |
| `IAlertIO` | `Alerts<RT>` | Alerts, through `IAlertSender` | 1.2 |
| `IGuildLogIO` | `GuildLogs<RT>` | Guild log posts, through the `GuildLog` queue | 1.2 |
| `ITraceIO` | `Trace<RT>` | The error id quoted in the generic reply (the trace id from 1.6) | 1.2 |
| `ICommandIO` | `Command<RT>` | Defer, reply, refuse and react, for one invocation | 1.2 |
| `IDatabaseIO` | `Db<RT>` | Dapper over Npgsql | 1.3 |
| `IDiscordIO` | `Discord<RT>` | Discord lookups and actions, returning the error cases | 1.4 |
| `ISettingsIO` | `Settings<RT>` | Typed guild configuration | 3.1 |

Two runtimes hold the capabilities. **`GrimoireRuntime`** is for events and workers. **`CommandRuntime`** adds
`ICommandIO`, so only a command can reply: an event handler that tries doesn't compile. Each runtime implements
`Has<Eff<RT>, Capability>` once per capability, and a function's `where RT : Has<…>` clause lists exactly the effects
it can have. Both are built from one `GrimoireEnv`; `CommandRuntime` is built per invocation.

Helpers in `Effect`: `Done`, `ForSome`, `Validated` (a `Validation` into an effect) and `Require`, which turns an
absent `Option` into an error case. Never bind an `Option` directly in an `Eff` query: language-ext fails it with
`Errors.None`, which the boundary treats as a defect.

## 3. Error model

From spec 1.1, section 3.

- **Results** are `Fin<T>`. **Input checks** are `Validation<Error, T>`, which collects every failure; a command
  converts it once, before any I/O (Q2a). **Absent values** are `Option<T>`, never `null`.
- **An error is its case plus a message** (Q2b). Four cases, all `Expected`, in `Grimoire.Domain/Errors.cs`:

  | Case | Means | Shown to the user | Colour |
  |---|---|---|---|
  | `Invalid` | Bad input, or an action the rules don't allow | its message | red |
  | `NotFound` | The thing asked for doesn't exist | its message | yellow |
  | `Forbidden` | Discord or a permission check refused | its message | red |
  | `Unavailable` | A dependency is down (Q1) | the generic reply only | yellow (generic) |

- **Any other `Error` is a defect**: an exception, `Errors.None`, or a language-ext error that isn't one of the four.
  Timeouts and cancellations that aren't shutdown count as `Unavailable`.
- **Several errors** (a `ManyErrors`) take their worst case: defect, then `Unavailable`, `Forbidden`, `Invalid`,
  `NotFound`. A refusal shows each distinct message on its own line.
- **A settings write that changed nothing** is a success: `Write<T>.Unchanged`.
- **Exceptions** are only for defects and cancellation. An outage becomes `Unavailable` at the one IO edge that
  catches it.

## 4. Boundaries

The boundaries are the only places a failure turns into a reply, a log line or an alert. None of them throws to
DSharpPlus (plan C2): `CommandHandler` and `IClientErrorHandler` only see failures that happen before our code runs
(checks, converters) and failures of code that hasn't been ported yet.

### 4.1 Commands

```csharp
[Command("Warn")]
public Task WarnAsync(CommandContext ctx, DiscordUser user, string reason)
    => runner.Run(ctx, invocation => WarnRequest.Validate(…), WarnUser<CommandRuntime>.Run);
```

1. **Privacy** (TD-11): `[PrivateReply]` on the method, `[PrivateReply(DirectMessageOnText = true)]`, or a privacy
   function passed to the runner when it depends on data.
2. **Delivery**: slash public, slash ephemeral, or for text: public, DM, or a 🔒 reaction and nothing else (C11).
3. **Validate**: pure. A failure is refused at once, with no defer.
4. **Defer** with the delivery.
5. **Act**: the command's `Eff`, returning a `Reply` (an embed, follow-up pages, and an optional guild log post).
6. **Render**: the reply, then the log post; or the refusal; or, for an outage or a defect, a log line and an
   Urgent alert first, then the generic "[ID …]" reply.

| Outcome | Slash | Text |
|---|---|---|
| Success | the deferred response is edited; follow-ups keep its visibility | public reply, or DM |
| Refusal before the defer | ephemeral initial response | public reply, or DM |
| Refusal after the defer | deferred response deleted, ephemeral follow-up (KI-21) | public reply, or DM |
| Outage or defect | Urgent alert, then the generic reply, ephemeral | the same, public or DM |
| Private command run as text | — | 🔒 reaction, nothing runs |
| DM-only command, DMs closed | — | 🔒 reaction |

Text custom commands aren't commands to DSharpPlus. Their handler reacts ❓ for both "not found" and "not allowed",
so a restricted command's existence isn't revealed (Q44, section 3.17).

The runner records each run's outcome, and `CommandHandler`'s "Command … finished with {Outcome}" line reports it.

### 4.2 Events

`EventRunner.Run(handlerName, guildId, () => effect)`. `NotFound` is a debug line (the thing was deleted while the
handler ran). `Invalid` and `Forbidden` are warnings and a Warning alert. `Unavailable` and defects are errors and an
Urgent alert.

### 4.3 Workers

A worker derives from `EffectBackgroundService` and returns one `Eff` tick, which runs on `Schedule.spaced`. Each
item goes through `Items.Item(key, work)`, with its own backoff (30 s doubling to 15 minutes). A failed item is
logged, alerted (except `Forbidden`) and backed off, and the next item still runs (F-9). An outage fails the tick
without using up the item's backoff (Q38). Three failed ticks in a row are Urgent, unless the cause is `NotFound`. A
summary line is logged every five minutes. All the state is one immutable `WorkerState` in an `Atom` (C6).

## 5. Tests

| Project | Holds | May touch |
|---|---|---|
| `Grimoire.UnitTests` | Smart constructors, pure decisions, embed builders, and whole `Eff` pipelines run against the fake runtime (`Functional/Fakes`): outages, refusals, defects, ordering | Nothing outside the process |
| `Grimoire.IntegrationTests` | SQL against Postgres, migrations, DSharpPlus wiring, event dispatch, PluralKit/HTTP, and one end-to-end smoke test per command and handler (C7) | Docker, the fake Discord harness |

The fake runtime is the production `GrimoireRuntime` and `CommandRuntime` built from recording fakes (`TestEnv`).
Each fake can be told to fail with a given error, and a shared `Journal` records the order of calls across them.
Stryker runs against the unit tests only.
