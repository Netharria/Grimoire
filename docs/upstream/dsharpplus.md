# DSharpPlus: suspected and confirmed problems

Problems found in DSharpPlus while working on Grimoire, collected here for the owner to review, then report upstream
or fix. Grimoire's side of each one (a workaround, a containment, a skipped test) stays in
[`../testing/known-issues.md`](../testing/known-issues.md) or the migration spec that found it. This file holds what
we know about DSharpPlus's side: what we saw, where in its source, and how sure we are.

**Version:** `5.0.0-nightly-02614`, source commit
[`6d4bff4d`](https://github.com/DSharpPlus/DSharpPlus/tree/6d4bff4d27fc13b7f5d768f77dbbc19c2e12ee59). Re-check every
open entry when the version changes.

**Status values:** *suspected* (read in the source, or seen without a cause), *confirmed* (reproduced, with the
cause found), *reported* (an upstream issue or PR exists), *fixed upstream* (in a version Grimoire uses), *not a
bug* (checked and dropped).

**Adding an entry:** give it the next `DSP-` number. Say what Grimoire saw, quote the DSharpPlus code by file and
method, give a reproduction if there is one, and link the KI or spec. Never fix it by reaching into DSharpPlus's
internals from Grimoire. Contain it at our edge and leave the fix to upstream.

## Summary

| # | Status | Area | Problem | Grimoire side |
|---|---|---|---|---|
| DSP-1 | confirmed | Gateway events | `GuildMemberUpdated`'s "before" values equal the new values | Q17, Q17d |
| DSP-2 | confirmed | Gateway | Socket-closed, zombied and resumed events never fire | KI-48 |
| DSP-3 | suspected, not reproduced | REST rate limits | A timeout on a route's first request leaves the route "pending" for good | spec 1.4, section 3.10 |
| DSP-4 | confirmed | Gateway cache | A guild's cached channels have no client reference for a moment at startup, so a post to one throws `NullReferenceException` | KI-54, spec 1.4 |
| DSP-5 | not a bug | REST retries | A timed-out POST may be sent again, posting twice | spec 1.4, section 3.10 |
| DSP-6 | suspected | REST | A resilience context and a `CancellationTokenSource` leak on every failed request | — |
| DSP-7 | not a bug | REST errors | 401 and 403 throw the same exception, but the status code is kept on it | spec 1.4, section 3.5 |
| DSP-8 | fixed upstream | Audit log | A message-delete entry's `Target` was the moderator | KI-39 |

## Entries

### DSP-1 `GuildMemberUpdated`'s "before" values can't be trusted · confirmed

- **Seen:** `UsernameBefore` and `AvatarHashBefore` always equal the new values. `NicknameBefore` and
  `GuildAvatarHashBefore` are right only for a member that was already cached, and the event args don't say which
  case applies.
- **Where:** `DiscordClient.Dispatch.cs`, `OnGuildMemberUpdateEventAsync`. `UpdateUserCache(userAfter)` runs before
  the event is raised, and `DiscordMember.Username` and `.AvatarHash` read through `Discord.UserCache[Id]`. An
  uncached member's "before" is built from the new payload.
- **Verified:** 2026-11-10, by reading the source (design decision Q17).
- **Grimoire:** compares against stored history and its own last-known cache (Q17d), never DSharpPlus's before
  values.
- **To report:** the before values should be captured before the cache update, and the args should say whether the
  member was cached.

### DSP-2 Gateway disconnects and resumes are never raised · confirmed

- **Seen (nightly-02611):** `SocketClosedEventArgs` and `ZombiedEventArgs` can have handlers registered but are never
  dispatched. The resume loop consumes the RESUMED payload without forwarding it, so `SessionResumedEventArgs` doesn't
  fire after a resume.
- **Where:** `GatewayClient`.
- **Grimoire:** KI-48. The gateway watchdog can't detect an outage. Two harness tests are skipped. Section 3.20 decides
  another health signal.
- **To do:** re-check on nightly-02614, then report.

### DSP-3 A timeout on a route's first request leaves the route stuck · suspected, not reproduced

- **Read in the source (2026-11-15):** `Net/Rest/RateLimitStrategy.cs`, `ExecuteCore`. For a route it hasn't seen,
  it stores `"pending"` with `routeHashes.TryAdd(route, "pending")`, then awaits the request **without a
  try/catch**. The cleanup that follows (`globalBucket.CompleteReservation()`, `UpdateRateLimitBuckets`,
  `routeHashes.Remove(route, out _)`) is skipped when the request throws. The known-route path does have a
  `catch (Exception e)` that cancels the reservations.
- **Effect, if right:** every later request on that route takes the `hash == "pending"` branch and gets a
  synthesized pre-emptive rate limit, which DSharpPlus's own retry (unlimited attempts, about 2.5 s apart) runs
  again. `CleanAsync` only removes routes whose value is a known bucket, and `"pending"` never is, so this lasts until
  restart.
- **Tested (2026-11-16):** not reproduced. `DSharpPlusTimeoutInvestigation.AfterTheRoutesFirstPostTimesOut_…` times
  out the first POST on a channel's messages route, on a fresh bot each run, then posts again: the next post went
  through within 10 s in 150 of 150 runs (5 passes of 30). Either a path not read here clears the entry, or the fake's
  timeout (a `TaskCanceledException` from the handler) doesn't reach this branch the way a real HttpClient timeout
  would. Worth a look when reading the source again, but low priority.

### DSP-4 A guild's cached channels have no client reference for a moment at startup · confirmed

- **Seen:** `NullReferenceException` posting to a channel, from either of two stacks:
  ```
  at DSharpPlus.Entities.DiscordChannel.get_Guild()
  at DSharpPlus.Net.DiscordRestApiClient.PopulateMessage(TransportUser author, DiscordMessage ret)
  at DSharpPlus.Net.DiscordRestApiClient.PrepareMessage(JToken msgRaw)
  at DSharpPlus.Net.DiscordRestApiClient.CreateMessageAsync(...)
  at DSharpPlus.Entities.DiscordChannel.SendMessageAsync(String content)
  ```
  or with `DiscordChannel.SendMessageAsync(String content)` as the top frame.
- **Cause (2026-11-16, `DSharpPlusTimeoutInvestigation`):** the channel is DSharpPlus's own cached object from
  `GUILD_CREATE` (`GetChannelAsync` returned the same instance as `guild.Channels`), and at that moment its `Discord`
  property is null. Three seconds later the same object has its client and a post goes through, and other cached
  channels are fine. So the guild's channels are in the cache before the `GUILD_CREATE` handling has given them their
  client. `DiscordChannel.Guild` is
  `this.GuildId.HasValue && this.Discord.Guilds.TryGetValue(...) ? guild : null`, which dereferences `Discord` with
  no check, and `PopulateMessage` calls it on the cached channel (`client.InternalGetCachedChannel(ret.ChannelId)`)
  after the POST has already succeeded. So in the first stack the message **was posted** and only the response's
  parsing failed.
- **When:** only the first bot started in a test process: run 1 failed in 9 of the 10 passes that warmed the route,
  and no later run ever did. That is when
  JIT compilation makes the window widest. Production is always a first start, so a post in the first moments after
  connecting can hit it. Not checked against real Discord.
- **This is KI-54.** The timeout in KI-54's reproduction was a coincidence of timing, not the cause.
- **Grimoire:** contained at the Discord edge (spec 1.4): a `NullReferenceException` thrown inside DSharpPlus is
  `Unavailable`, not retried. Not retrying a send is right here, since the first stack means the post went through.
  The publisher logs the lost log; a command gets the generic reply.
- **To report:** set each channel's `Discord` (and any other back-references) before adding the guild's channels to
  the cache; or make `DiscordChannel.Guild` null-safe (`this.Discord?.Guilds…`); and don't fail a message create
  that succeeded because the reply couldn't be enriched.

### DSP-5 A timed-out POST may be sent twice · not a bug

- **Read in the source:** `RestClient`'s retry pipeline has `MaxRetryAttempts = int.MaxValue`, with a 2.5 s fallback
  delay for anything other than a rate limit. Which exceptions it handles depends on Polly's default `ShouldHandle`.
- **Tested (2026-11-16):** one POST per timed-out send, in all 150 runs of each timeout case. DSharpPlus doesn't
  resend a POST whose request timed out (a `TaskCanceledException` with an inner `TimeoutException`, as HttpClient
  raises it).

### DSP-6 A resilience context and a token source leak on a failed request · suspected

- **Read in the source:** in `RestClient.ExecuteRequestAsync`, `ResilienceContextPool.Shared.Return(context)` runs
  only on the success path, so a thrown exception never returns the context to the pool. The method's
  `CancellationTokenSource` is never disposed.
- **Effect:** a small allocation per failed request. Probably harmless at Grimoire's volume.
- **To report:** move both into the `finally`.

### DSP-7 401 and 403 throw the same exception · not a bug

- **Read in the source:** `ExecuteRequestAsync` maps `Unauthorized` and `Forbidden` both to `UnauthorizedException`.
- **Checked by the owner (2026-11-15):** the exception keeps the response's `HttpStatusCode`, so a caller can tell a
  bad token (401) from a missing permission (403). Nothing to report.
- **Grimoire:** `DiscordErrors.Classify` branches on that status, then on the JSON code (spec 1.4, section 3.5).

### DSP-8 A message-delete audit entry's `Target` was the moderator · fixed upstream

- **Seen (nightly-02611):** the entry's `Target` was built from the responsible user's id, so "Deleted By" never
  matched. It regressed in PR #1612, which closed #1569.
- **Fixed** upstream in nightly-02614, which Grimoire uses (KI-39).
