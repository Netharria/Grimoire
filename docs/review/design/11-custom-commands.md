# Section 11: Custom commands

Reviewed 2026-11-09 against commit 3580d02 plus the uncommitted tree, as the code will be once every fix in
`review-decisions.md` is in. That includes C-2's deny arm and C-3's removal of the sanitiser, which aren't in the code yet.

The biggest win is a per-guild cache of current command definitions (CC-1). Learn, Forget and Revert are the only
writers, and all three live in `CustomCommandSettings`, so invalidation is two lines. With the cache, the `!` path,
`/Command` and the name autocomplete (which runs on every keystroke) make no database query at all, and the
`LatestVersions` anti-join runs once per guild per day instead of on every lookup. Next comes F-5 applied to the six
`/Commands` subcommands, about −80 lines (CC-2). After that, `/Commands History` can reuse `ReplyPagedAsync` (CC-5), the
role rows can stop restating their version's key (CC-6), and the access policy can move onto `CustomCommand` (CC-7).
Findings CC-1 to CC-13 follow.

## Findings

#### CC-1 Cache each guild's current command definitions  `perf` `remove` · **high**
- **Where:** `TextCustomCommandProcessor.FetchCommandAsync` (`TextCustomCommandProcessor.cs:87-100`, runs on every
  message that starts with `!` and has a word after it), `GetCustomCommand.FetchAuthorizedCommandAsync`
  (`GetCustomCommand.cs:59-69`, every `/Command`), `GetCustomCommandOptions.GetChoicesAsync`
  (`GetCustomCommandOptions.cs:30-54`, every keystroke in a `name` field of `/Command`, Forget, Revert, History, Stats and
  Leaderboard), `CustomCommandDatabaseQueryHelpers.LatestVersions`/`GetCustomCommandQuery`
  (`CustomCommandDatabaseQueryHelpers.cs:13-24`), and the existence checks in `QueryStatsAsync` (`Stats.cs:43`) and
  `GetCommandLeaderboardAsync` (`Leaderboard.cs:74`).
- **Now:** every lookup opens a `DbContext` and runs a `NOT EXISTS` anti-join over every version of the guild's commands.
  The `!` path and `/Command` also `Include` the roles. Autocomplete computes `levenshtein(lower(name), …)` over every
  current command in the guild and ships up to 5 × 2,000 characters of `Content`, only to check for `%mention`/`%message`.
  A message like `!!!` or `!lol` that names no command still costs a query. Definitions change only through `/Commands`,
  so nearly all of these queries return the same rows.
- **Proposed:** one `CustomCommandCatalog` service on the `HybridCache` that `SettingsModule` already uses, with the same
  one-day expiry. It holds one entry per guild:
  ```csharp
  internal sealed class CustomCommandCatalog(IDbContextFactory<GrimoireDbContext> dbContextFactory, HybridCache cache)
  {
      public ValueTask<IReadOnlyDictionary<CustomCommandName, CustomCommand>> GetAsync(GuildId guildId)
          => cache.GetOrCreateAsync($"custom-commands:{guildId}", async ct =>
          {
              await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
              return (IReadOnlyDictionary<CustomCommandName, CustomCommand>)(await dbContext.CustomCommands
                  .LatestVersions().AsNoTracking().Include(x => x.Roles)
                  .Where(x => x.GuildId == guildId)
                  .ToListAsync(ct)).ToFrozenDictionary(x => x.Name);
          }, Options);

      public ValueTask InvalidateAsync(GuildId guildId) => cache.RemoveAsync($"custom-commands:{guildId}");
  }
  ```
  `SaveCommandAsync` and `DeleteCommandAsync` call `InvalidateAsync` after their write. They are the only writers:
  nothing else in `Grimoire`, `Grimoire.Data` or `Grimoire.Settings` touches `CustomCommands`. The consumers then:
  - `!` path: `(await catalog.GetAsync(guildId)).GetValueOrDefault(name)`, with no `DbContext`.
  - `/Command`: the same lookup.
  - Name autocomplete: rank the dictionary's values in memory with a small Levenshtein, or `StartsWith`, then
    `Contains` (see the questions). A guild has tens to a few hundred commands. Section 5 should count the `!` path as
    zero database calls once this is in.
  - Stats, Leaderboard: `ContainsKey` instead of `AnyAsync`.

  `GetCustomCommandQuery` and `FetchAuthorizedCommandAsync` go. `LatestVersions` keeps one caller. If the in-memory
  ranking is taken, the `fuzzystrmatch` extension (`GrimoireDbContext.cs:133`) has no user left and can be dropped in a
  later migration. Cached records are immutable (`init`-only, apart from `Roles`, see CC-6), so sharing them across
  events is safe.
- **Size:** about +30 lines (the catalog and its registration), about −35 lines (two query helpers, two fetch methods,
  the autocomplete query, two `AnyAsync`). 8 files.
- **Needs:** nothing. CC-6 makes `Roles` read-only, which is advisable before sharing instances.
- **Interacts with:** section 4's question on caching `GrimoireDbContext` rows (this is its answer for custom commands),
  section 5's table, KI-86 (with the roles in memory, autocomplete can filter by the member's roles for free), KI-87 (the
  dictionary's comparer decides case sensitivity in one place), C-9. `AutocompleteQueryTests` pins
  `GetCustomCommandQuery` and needs to move to the catalog.

#### CC-2 The `/Commands` subcommands against T-1  `pipe` · **med**
- **Where:** `CustomCommandSettings.Learn.cs:53-114`, `Forget.cs:30-69`, `Revert.cs:34-83`, `Stats.cs:28-37`,
  `Leaderboard.cs:28-39`. Each one has `DeferResponseAsync`, then `ctx.RequiredGuild`, then a `.Match` that ends in a
  hand-written reply, or a private `On…Success(ctx, …)` helper that sends the reply and then the log.
- **Now:** five reply/log pairs written by hand, three error colours chosen per call site (Learn red, Revert/History/
  Leaderboard yellow, Stats both), and `Forget`'s `bool alreadyForgotten` choosing between two reply shapes inside
  `OnForgetSuccess`.
- **Proposed:** F-5. Learn's input is the validation chain, and its act step is `SaveCommandAsync` followed by
  `new Reply(embed, Log: new GuildLogMessage { … })`, because the log text differs from the reply. Forget returns
  `deleted ? Reply.Logged(…) : new Reply(…)` straight from the delete count, so the `bool` and `OnForgetSuccess` go.
  Revert: see CC-4. Stats and Leaderboard use the no-input overload, and their "no such command"/"not used yet" errors
  become `Error.NotFound` (F-4). History and `/Command` don't fit (paged reply, and a deleted response).
- **Size:** Learn about −20, Forget −18, Revert −10 (−24 with CC-4), Stats −6, Leaderboard −6. About −60 to −75 lines in
  the feature in all.
- **Needs:** F-3, F-4, F-5. F-6 makes Learn's input shorter (see CC-3).
- **Interacts with:** C-6 (already removed the catches), KI-88 (Learn's reply can say "Updated" when the catalog shows the
  name already exists, at no extra cost after CC-1).

#### CC-3 Learn's input: two `bool`s choose modes, and the factories split by type  `state` · **low**
- **Where:** `CustomCommandSettings.Learn` (`Learn.cs:36-79`), `ParseEmbedColor` (`:82-89`), `AllowRoleIds` (`:94-102`),
  `TextCustomCommand.Create`/`EmbedCustomCommand.Create` (`CustomCommand.cs:113-152`), `CustomCommand.AllowRoles`
  (`:37-45`).
- **Now:** `embed` + `embed_color` choose the type (the colour is refused without `embed`, C-8). `restricted_use` +
  `permission_role_1..10` choose the policy. After C-2's deny arm, `restricted_use: false` with roles means "everyone
  except these roles", which a user can't guess from the option name. The four-level `Bind` pyramid carries every value
  into the last lambda, and the two `Create` calls repeat six arguments in each arm of `embed ? … : …`. The factories take
  only allow-role ids, so C-2 has to add a second list to both.
- **Proposed:**
  - Replace `restricted_use` with an enum choice option, `access: Everyone | OnlyTheseRoles | EveryoneExceptTheseRoles`
    (`[Parameter("access")]`, snake_case per the parameter-naming rule). The role switch then reads its mode from the
    name, and "roles given with `Everyone`" becomes one clear refusal.
  - The factories take one `CommandAccess(IReadOnlySet<RoleId> Allow, IReadOnlySet<RoleId> Deny)` value (CC-7) instead
    of a growing list of id lists. `AllowRoles` becomes `Roles(access, …)` with both arms.
  - The pipeline builds `(name, format, access, content)` first and calls one local function that picks the factory, so
    the six repeated arguments appear once.
- **Size:** about −8 lines in `Learn.cs`, ±0 in `CustomCommand.cs`. Behaviour change for the option name (see the
  questions).
- **Needs:** C-2 done. F-6 for `name` and `content` as parameter types (C-4's string `name` can go back to the converter
  once F-6 passes the reason through).
- **Interacts with:** C-2, C-4, C-8, D-5, D-8, F-6.

#### CC-4 Revert: a typed version, and the timestamp it already has  `state` `remove` · **low**
- **Where:** `CustomCommandSettings.Revert` (`Revert.cs:24-49`), `ParseVersion` (`:51-55`), `OnRevertSuccessAsync`
  (`:71-83`), `GetCommandVersionOptions.BuildChoices` (`GetCommandVersionOptions.cs:39`).
- **Now:** the version is a `string` holding `DateTimeOffset.Ticks`. `ParseVersion` turns it into a `long`, a `Map`
  carries `(Target, Ticks)` along, and `OnRevertSuccessAsync` rebuilds a `DateTimeOffset` from the ticks, though
  `Target.CreatedAt` is the same value. A hand-typed out-of-range number throws in `new DateTimeOffset(ticks)` (KI-83).
- **Proposed:** a `CustomCommandVersion` parameter type through F-6's `ValidatedConverter`, whose `Create` parses and
  range-checks the ticks and holds a `DateTimeOffset`. The command then reads
  `FetchVersionAsync(guildId, name, version.CreatedAt)`, and the reply uses `target.CreatedAt.ToUnixTimeSeconds()`. The
  tuple, `ParseVersion` and the second `DateTimeOffset` construction go, and KI-83 is fixed by the type. Ticks are still
  needed in the option value: they don't fit Discord's integer option (2^53), so the option stays a string.
- **Size:** about −14 lines in `Revert.cs`, +8 for the type and converter. 3 files.
- **Needs:** F-6 (or keep `Create` in the input pipeline if the spike fails).
- **Interacts with:** KI-83, D-8 (`AsNewVersion` stays as is), C-8 (the numbering fix stays).

#### CC-5 `/Commands History` has its own pager  `remove` · **low**
- **Where:** `CustomCommandSettings.SendHistoryPagesAsync` and `BuildPages` (`History.cs:55-80`).
- **Now:** `BuildPages` packs entries into 2,048-character pages with its own `StringBuilder` loop, and each page is a
  separate `ctx.ReplyAsync`. So a long history arrives as several messages, while every other long list uses
  `ctx.ReplyPagedAsync` (`PagedReplyExtensions.cs:27`, X-10), whose `Paginate` does the same packing at 4,096 characters
  with buttons.
- **Proposed:** `await ctx.ReplyPagedAsync(new DiscordEmbedBuilder().WithColor(GrimoireColor.Purple).WithTitle(…),
  versions.Select((v, i) => FormatEntry(v, versions.Count - i, i == 0)))`. `FormatEntry` drops its trailing `\n`, because
  `Paginate` joins lines.
- **Size:** about −25 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** X-10 (extends its list to History). Check `HistoryCommandTests` for anything that counts replies.

#### CC-6 Role rows restate their version's key  `state` `remove` · **low**
- **Where:** `CustomCommandRole` (`CustomCommandRole.cs:13-20`), `CustomCommand.Roles` (`CustomCommand.cs:21`),
  `CustomCommand.AsNewVersion` (`:27-34`), `CustomCommand.AllowRoles` (`:37-45`), `CustomCommandRoleConfiguration`
  (`CustomCommandRoleConfiguration.cs:18-28`).
- **Now:** each role carries `required` `Name`, `GuildId` and `CreatedAt`, plus a `CustomCommand?` back-reference, which
  together are its version's primary key. Nothing stops a role whose `CreatedAt` differs from its command's, which is why
  `AsNewVersion` has to rewrite `CreatedAt` and null `CustomCommand` on every copied role, and why `AllowRoles` takes three
  extra parameters. `Roles` is a public mutable `ICollection`, so any holder of a (soon cached, CC-1) command can add to it.
- **Proposed:** make the three key columns and the navigation shadow properties, so `CustomCommandRole` is just
  `RoleId` (plus its Allow/Deny case). EF fills a dependent's foreign key from its principal when the graph is added. The
  column names stay, so no migration should be needed (check with `migrations add`). `Roles` becomes
  `IReadOnlyCollection<CustomCommandRole>` over a backing field. `AsNewVersion` becomes
  `this with { CreatedAt = createdAt, ModeratorId = moderatorId, Roles = [.. this.Roles.Select(r => r with { })] }`.
  The copy is still needed, because EF must see new instances.
- **Size:** about −12 lines in the domain, +4 in the configuration. 3 files.
- **Needs:** check that shadow key properties keep the `CustomCommandName` converter and P-4's strongly-typed-id
  convention.
- **Interacts with:** D-8, C-9 (its "set `CustomCommand` to null" goes), section 2 (role hierarchy), section 3 (shadow
  keys).

#### CC-7 The access policy belongs on `CustomCommand`  `state` · **low**
- **Where:** `GetCustomCommand.IsUserAuthorized` (both overloads, `GetCustomCommand.cs:91-107`), called from
  `TextCustomCommandProcessor.cs:97` and `GetCustomCommand.cs:66`.
- **Now:** the spec'd policy lives on the slash-command class, and the `!` processor reaches across into it. Each check
  runs `OfType` twice over the roles and builds two `HashSet`s, per use.
- **Proposed:** `CustomCommand.Access` (a `CommandAccess(FrozenSet<RoleId> Allow, FrozenSet<RoleId> Deny)`, built once
  when the catalog loads, CC-1) and a pure `bool CommandAccess.Allows(IEnumerable<RoleId> memberRoles)` with the spec's
  one expression. The spec's code block points there. With CC-1, the sets are built once per guild load, not once per use.
- **Size:** ±0 lines, 3 files. Tests for the pure overload move with it.
- **Needs:** CC-1 for the per-use saving. CC-3 shares the type.
- **Interacts with:** C-9 (pure overload), KI-86.

#### CC-8 The `!` processor after F-10  `pipe` `perf` · **med**
- **Where:** `TextCustomCommandProcessor.HandleEventAsync` (`TextCustomCommandProcessor.cs:27-58`), `LogFailure`
  (`:61-74`), `NotFoundCode`/`ModuleDisabledCode` (`:24-25`), `ParsedCommand` (`:139-143`), `FetchCommandAsync`
  (`:87-100`). This is the internal design. The per-message cost is section 5's.
- **Now:** besides F-10's allocations for non-commands, the whole content is split on every space before the name is even
  looked up (up to 4,000 characters for a message that only starts with `!`), and `%Message` is rebuilt by re-joining the
  words, which collapses runs of spaces. The module check uses `GetOrElse(() => false)` (F-1). `ParsedCommand` exists only
  to carry four values through `TapAsync`.
- **Proposed:** F-10's early `is not` return, then one straight-line method:
  ```csharp
  if (ev.Message is not { MessageType: Default or Reply, Author: DiscordMember { IsBot: false } member,
          Content: ['!', ..] content }
      || CustomCommandName.Create(FirstWord(content)) is not Result<CustomCommandName>.Ok(var name)
      || !await settingsModule.IsModuleEnabled(Module.Commands, member.GetGuildId())          // F-1: plain bool
      || (await catalog.GetAsync(member.GetGuildId())).GetValueOrDefault(name) is not { } command
      || !command.Access.Allows(member.GetRoleIds()))
      return; // log "not found" only for the last two, where the name is in scope
  ```
  Split with `content.AsSpan(1).Split(' ')` for the first two words only, and pass the rest of the message verbatim as
  `%Message`. The mention arg can be read from `ev.Message.MentionedUsers`/`MentionedRoles` by id instead of
  `sender.GetUserOrDefaultAsync` (`ResolveSnowflake`, `:130-137`), which may go to REST on a cache miss.
  `LogFailure`, the two code constants, `ParsedCommand` and the pipeline go.
- **Size:** the file goes from 144 lines to about 95 (−50). 1 file.
- **Needs:** F-10, CC-1, CC-7. F-1 for the plain `bool`.
- **Interacts with:** F-10, F-4 (the last production `switch (error.Code)`), C-8 (the first-word fix stays), C-9.
  Changing `%Message` to keep the original spacing is a visible change (see the questions).

#### CC-9 `/Command` without a defer  `pipe` · **low**
- **Where:** `GetCustomCommand.CallCommand` (`GetCustomCommand.cs:27-57`).
- **Now:** it defers publicly, looks up the command, and on "not found or not allowed" or an empty render deletes the
  response. That's two identical `DeleteResponseAsync; return;` branches, and the user sees "thinking…" appear and then
  vanish. `FetchAuthorizedCommandAsync` returns a `Result` whose error nobody reads.
- **Proposed:** after CC-1 the lookup and render are in memory, so `/Command` can answer well within Discord's 3-second
  window without deferring. Respond directly with the rendered message. On failure, send one ephemeral "No command named
  `x` that you can use." (or keep a silent delete, see the questions). Merge the two branches into one `is not { }`
  pattern either way, and drop the `Result`.
- **Size:** about −12 lines, 1 file.
- **Needs:** CC-1 (without it, keep the defer and just merge the branches, about −6).
- **Interacts with:** C-1 (its "delete the response" becomes the ephemeral or silent path), KI-21.

#### CC-10 Leaderboard and Stats round trips  `perf` · **low**
- **Where:** `GetOverallLeaderboardAsync` (`Leaderboard.cs:41-60`), `QueryStatsAsync` and `FetchUsageStatsAsync`
  (`Stats.cs:39-62`).
- **Now:** the overall leaderboard runs three queries: the top 15 groups, `Distinct().CountAsync()` and `CountAsync()`.
  The per-name groups are already all computed by the first query before `Take(15)`. Stats returns `UsageStats?`, so the
  embed needs four `?? 0`/`?.` fallbacks for "never used", and `StatsQueryResult` carries the nullable.
- **Proposed:** overall: one `GroupBy(x => x.Name).Select(g => new { g.Key, Count = g.Count() }).ToListAsync()`. A guild
  has at most a few hundred names, so the footer is `list.Count` and `list.Sum(x => x.Count)`, and the top 15 are taken in
  memory. Stats: `?? UsageStats.None` on the query, so `UsageStats` isn't nullable. `StatsQueryResult` can then go and the
  act step returns the embed. The per-name leaderboard keeps two queries, because it groups by user.
- **Size:** about −8 lines, 2 files. One round trip instead of three for the overall leaderboard.
- **Needs:** nothing. CC-1 removes the `AnyAsync` in both.
- **Interacts with:** C-8 (the footer fix stays, done with one query).

#### CC-11 Rendering lives on the slash-command class  `remove` · **optional**
- **Where:** `GetCustomCommand` statics: `MaxMessageLength`, `MaxEmbedDescriptionLength`, `TruncateForDiscord`,
  `RenderContent`, `BuildResponse`, `BuildEmbed`, `ApplyMention`, `ApplyMessage` (`GetCustomCommand.cs:19-20, 109-158`),
  used by `TextCustomCommandProcessor`.
- **Now:** both entry points share the rendering through the `/Command` class, as with CC-7's policy.
- **Proposed:** move them to a `static class CustomCommandRenderer` in the feature folder. `GetCustomCommand` then holds
  only the command. After C-3, `ApplyMessage` no longer takes `guildId`.
- **Size:** ±0 lines, 3 files plus `GetCustomCommandTests`.
- **Needs:** C-3.
- **Interacts with:** C-3, C-9.

#### CC-12 Version autocomplete: two queries per keystroke  `perf` · **optional**
- **Where:** `GetCommandVersionOptions.GetVersionsAsync` (`GetCommandVersionOptions.cs:52-64`).
- **Now:** every keystroke in Revert's `version` field runs `CountAsync` and then the newest-25 query. The typed text is
  ignored, so every keystroke gives the same answer.
- **Proposed:** one query that takes the newest 25 and its total with
  `.Select(x => new { x.CreatedAt, x.ModeratorId, Total = versions.Count() })`, which EF sends as a scalar subquery.
  Only worth doing if Revert is used enough to notice. The real fix for "same answer per keystroke" would be a short
  cache, which doesn't pay for itself here.
- **Size:** about −2 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** C-8 (the numbering).

#### CC-13 `closed` for the command and role families  `cs15` · **low**
- **Where:** `CustomCommand` (`CustomCommand.cs:14`, cases `TextCustomCommand`, `EmbedCustomCommand`), `CustomCommandRole`
  (`CustomCommandRole.cs:13`, cases Allow and Deny). Type tests at `GetCustomCommand.cs:130`, `:137`, and the two
  `OfType`s at `:94-95`.
- **Now:** both are `abstract record`s that outside assemblies could extend. The render code tests
  `command is EmbedCustomCommand` and treats everything else as text.
- **Proposed:** `closed record` for both (T-3's rule: we own the cases and they share state). `BuildResponse` becomes an
  exhaustive `command switch { EmbedCustomCommand { EmbedColor: var c } => …, TextCustomCommand => … }`, with no `_` arm,
  so a third format won't compile until it renders. With CC-7, the role kinds are read only when building
  `CommandAccess`, as a switch with the same property.
- **Size:** about +2 / −2 lines, 3 files.
- **Needs:** `net11.0`, and section 3's EF spike for closed TPH hierarchies. Section 2 makes the final call per hierarchy.
- **Interacts with:** D-8, section 2.

## Not findings

- Versions as rows (each Learn inserts a full copy) earn their place: Revert and History are built on them. CC-1 removes
  the read cost of the anti-join, which is cheaper than remodelling into "current plus history" tables.
- `CustomCommandUsage` keyed by name with no foreign key: Forget deletes it explicitly, and the branch review accepted the
  two deletes not sharing a transaction.
- `ModeratorId?` on `CustomCommand` is null only for legacy rows. New versions always set it.
- The `UnauthorizedException` catch in `SendCommandResponseAsync` is an expected failure at a framework edge that reports
  a guild issue. It stays.

## Questions for the owner

1. **CC-1 ranking.** With the catalog in memory, should the name autocomplete keep Levenshtein order (needs ~10 lines of
   in-memory Levenshtein) or switch to "prefix matches first, then contains", which is shorter and usually what people
   expect while typing? The second lets `fuzzystrmatch` go.
2. **CC-3 option change.** Replace `restricted_use` with an `access` choice (Everyone / Only these roles / Everyone
   except these roles)? It changes the slash command's options, so anyone used to `restricted_use` notices.
3. **CC-8 `%Message` spacing.** Passing the rest of the message verbatim keeps double spaces and the user's own
   formatting, where today words are re-joined with single spaces. Is that change wanted?
4. **CC-9 failure reply.** When `/Command` names a command the member can't use, should they get an ephemeral "not found"
   reply, or should the response stay silently deleted, as now (C-1 kept the delete)?
5. **Usage retention.** `CustomCommandUsages` gets one row per use and is never trimmed. Stats only looks back a year
   beyond "Total Uses". Keep everything forever, or add it to `LogCleanup` with a retention period?
