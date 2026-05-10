 ---
Domain Model Review — Grimoire.Domain

  ---
I. Result & Validation Machinery

1. Invalid accumulates, other error cases don't — semantic mismatch

Result<T>.Invalid holds ImmutableArray<Error>, but NotFound, NotModified, Conflict, and Forbidden each hold a single Error. The Match method
then wraps the single-error cases into an array on the fly:

NotFound(var e) => onNotFound is not null ? onNotFound([e]) : onFailure([e]),

This mixed shape leaks through the API. Pick one shape consistently: either all error cases hold ImmutableArray<Error>, or all hold a single
Error and Invalid gets a different name (e.g., ValidationFailed).

2. Validation<T> doesn't accumulate errors — the name lies

The defining property of Validation in functional programming is error accumulation — you run all validations independently and collect every
failure. Your Validation<T> short-circuits on the first Bind, identical to Result<T>. The semantic contract the name implies isn't
delivered. To fix this, add an applicative combinator:

public static Validation<(T1, T2)> Combine<T1, T2>(Validation<T1> v1, Validation<T2> v2)
=> (v1, v2) switch
{
(Valid<T1>(var a), Valid<T2>(var b)) => Succeed((a, b)),
_ => new Invalid([.. (v1 is Invalid i1 ? i1.Errors : []), .. (v2 is Invalid i2 ? i2.Errors : [])])
};

Without this, Validation is just a re-skinned Result and the two types carry no semantic distinction.

3. Validation<T> missing Tap / TapAsync

Result<T> has both but Validation<T> doesn't. If you're chaining validations and need a side effect on success (e.g., logging), you have to
break the chain. Add them for consistency.

4. Result.WhenAll loses all but the first failure

await Task.WhenAll(t1, t2);
if (t1.Result is not Result<T1>.Success(var v1)) return t1.Result.Map<(T1, T2)>(_ => default!);
if (t2.Result is not Result<T2>.Success(var v2)) return t2.Result.Map<(T1, T2)>(_ => default!);

Both tasks run in parallel; if both fail, only t1's error is returned. The combinator's purpose — parallel independent operations — suggests
you'd want all errors collected. Use Validation.Combine internally and convert at the end, or collect into an Invalid with all errors.

Also: Map<(T1, T2)>(_ => default!) uses default! to satisfy the type parameter. This is a null-suppression smell. A static helper private
static Result<TOut> PropagateError<TIn, TOut>(Result<TIn> r) => ... would be cleaner.

5. MatchAsync takes async onSuccess but sync failure handlers

In both Result<T>.MatchAsync and its task extension, the failure handlers are synchronous:

public Task<TOut> MatchAsync<TOut>(
Func<T, Task<TOut>> onSuccess,
Func<ImmutableArray<Error>, TOut> onFailure,   // sync
...)

This forces callers to block or use Task.FromResult in the failure arm. ValidationTaskExtensions adds the async-invalid overload only for
Validation, not for Result. Add the fully-async overload to Result.MatchAsync as well.

6. Parameter name divergence: onNotChanged vs onNotModified

Result.Match uses onNotModified. ResultTaskExtensions.Match uses onNotChanged. These must be the same name — one of them is wrong.

7. No IEnumerable-arity combinator

Result.WhenAll goes up to 6 fixed-arity overloads but there's no:

Task<Result<IReadOnlyList<T>>> WhenAll<T>(IEnumerable<Task<Result<T>>> tasks)

This is the most practically useful form and currently missing.

8. No ToResult() extension on Task<Validation<T>>

You have Validation<T>.ToResult() on the instance, but no extension on Task<Validation<T>>. Callers must await and then call the instance
method, breaking pipeline chains.

  ---
II. Strongly-Typed IDs

9. SinId and AttachmentId are co-located with their entities, not with the other IDs

StronglyTypedIds.cs has the six Discord IDs; SinId lives in Sin.cs and AttachmentId lives in Attachment.cs. Pick one convention: all IDs in
StronglyTypedIds.cs, or each ID in its entity file.

10. SinId has no TryParse and is signed while all others are unsigned

All Discord IDs use ulong. SinId uses long (matching PostgreSQL bigserial) but lacks TryParse. Add it — and add a comment on the long choice
so the next reader doesn't "fix" it to ulong.

11. No implicit conversion between ModeratorId and UserId

A moderator IS a Discord user. When you have a UserId and need a ModeratorId (or vice versa), you must do new ModeratorId(userId.Value) which
defeats the type-system protection. Add an explicit conversion operator or a factory method:

public readonly record struct ModeratorId(ulong Value)
{
public static explicit operator ModeratorId(UserId id) => new(id.Value);
public static explicit operator UserId(ModeratorId id) => new(id.Value);
...
}

Explicit (not implicit) keeps you from accidentally conflating the two.

  ---
III. Value Types — Inconsistent "Invalid state unrepresentable" coverage

12. Nickname, Username, AvatarFileName, InviteCode, InviteUrl have public constructors

These are publicly new-able with any string, including null (the string field defaults to null on a default struct):

public readonly record struct Nickname(string Value)   // public constructor, no validation

Compare to ModerationReason, MessageContent, and CustomCommandName which all have private constructors + Create() + Validation<T>. The
inconsistency means half your value types can represent invalid state. Apply the same pattern to all of them.

13. Static Equals(a, b) pattern is non-standard and confusing

AvatarFileName, Nickname, Username, and MessageContent all define:

public static bool Equals(Nickname? a, Nickname? b) => ...
public static bool Equals(Nickname? a, Nickname? b, StringComparison) => ...

readonly record struct already generates value-based == and .Equals(). These static methods shadow object.Equals(object, object) — they're an
entirely different method, but naming them Equals is confusing. The intent is string-comparison control. More idiomatic: extension methods
EqualsOrdinalIgnoreCase(this Nickname a, Nickname b) or instance methods. The static form is unexpected by C# consumers.

14. Attachment.FileName is a raw string

Every other filename-like field is wrapped (e.g., AvatarFileName). Attachment.FileName is a plain string. Either wrap it in a value type or
document why it's intentionally not wrapped.

15. ProxiedMessageLink.SystemId and MemberId are raw strings

These are PluralKit identifiers. Nothing stops you from passing a SystemId where a MemberId is expected. Two lightweight value types
(PluralKitSystemId, PluralKitMemberId) would prevent this class of mistake.

16. CustomCommand.Content is a raw string

TextCustomCommand and EmbedCustomCommand store content as required string Content. No length bounds, no empty check. This should go through
the same validation-value-type treatment as MessageContent.

  ---
IV. Entity Model Issues

17. DateTimeOffset { get; } = DateTimeOffset.UtcNow is a latent bug

Several entities use:

public DateTimeOffset CreatedTimestamp { get; } = DateTimeOffset.UtcNow;

Without a setter, EF Core must set the backing field via reflection at materialization time. If EF Core column mapping isn't configured
precisely, an entity materialized from the DB will have the object-creation time, not the stored time. This affects Message.CreatedTimestamp,
Avatar.Timestamp, Sin.SinOn, NicknameHistory.Timestamp, UsernameHistory.Timestamp, and OldLogMessage.CreatedAt.

The fix is uniform: use required DateTimeOffset CreatedAt { get; init; } on all of these and require callers (including EF mappings) to
provide the value. That makes the contract explicit instead of relying on EF reflection magic.

18. Sin.Id uses private set — breaks record immutability

public SinId Id { get; private set; }

All other records use init. The private set exists because SinId is a database-generated identity and EF Core needs to write it after INSERT.
Two cleaner options:

- Mark it init and configure EF to use the backing field (Property(x => x.Id).HasField("_id"))
- Keep private set but document that this is intentionally EF-only mutation

The inconsistency with every other entity's immutable pattern is worth resolving.

19. required ModeratorId? ModeratorId — required nullable is semantically confusing

In SinReasonHistory and Sin:

public required ModeratorId? ModeratorId { get; init; }

required + nullable reads as "you must provide this, but null is a valid value." That's not wrong, but it models "system vs. moderator actor"
as a nullable ID instead of a discriminated union. Consider:

public abstract record Actor;
public sealed record ModeratorActor(ModeratorId Id) : Actor;
public sealed record SystemActor : Actor;

This makes the intent explicit at the type level rather than relying on "null = system."

20. Invite.Inviter is a Username, not a UserId

public required Username Inviter { get; init; }

The inviter's display name can change; the invite record becomes stale. The identity of the inviter is their Discord user ID, not their
current username. This should be UserId (or UserId + Username as a pair if display is needed at record time).

21. Invite has no GuildId

An invite is scoped to a guild, but the entity has no GuildId. Querying all invites for a guild is impossible without a join to some other
table — but there's nothing to join to since invites have no navigation back to guild.

22. LeaderboardView is not sealed

Every final entity is sealed record. LeaderboardView is just record. If it's not meant to be subclassed, seal it.

23. XpHistory.TimeOut is a misleading name

TimeOut reads as a duration or a deadline. The property stores when the XP history entry was recorded. RecordedAt or Timestamp would be
unambiguous.

24. ICollection<T> navigation properties are mutable from outside

public ICollection<Pardon> Pardons { get; init; } = [];
public ICollection<PublishedMessage> PublishMessages { get; init; } = [];

The collections are settable (init) to a new list, but the list itself is ICollection<T> — callers can call .Add() / .Remove() on the
collection directly. Expose IReadOnlyCollection<T> or IReadOnlyList<T> from the domain; let EF Core access the backing list via shadow
properties or field-based access:

private readonly List<Pardon> _pardons = [];
public IReadOnlyList<Pardon> Pardons => _pardons;

25. MessageHistoryEntry.TimeStamp — casing inconsistency

MessageHistoryEntry.TimeStamp (capital S). Every other time property in the model uses Timestamp (lowercase s). One of them is wrong.

26. Dual ProxiedMessageLink properties on Message are confusing

public ProxiedMessageLink? ProxiedMessageLink { get; init; }
public ProxiedMessageLink? OriginalMessageLink { get; init; }

Both are the same type. The type ProxiedMessageLink already contains both ProxyMessageId and OriginalMessageId. The reason Message has two
separate references isn't obvious. Rename them to make the intent clear, e.g., AsProxyLink and AsOriginalLink, or add a comment explaining
the two-direction navigation.

---
V. Missing Domain Layer Boundary Enforcement

27. EF Core navigation properties bleed into domain entities

public Sin? Sin { get; init; } on Pardon, public Message? Message { get; init; } on Attachment — these are EF Core navigation properties, not
domain concepts. The domain entity shouldn't know about its EF graph. This is a known trade-off with EF Core, but at minimum, consider
whether those navigation properties are ever used in domain logic or only in queries.

28. No aggregate root, no encapsulated invariants

The model has clusters that should form aggregates (Sin + Pardons + ReasonHistory + PublishedMessages), but any code can add directly to any
collection, bypassing invariants. For example: can a Sin have two Pardons? The model allows it. If not, the enforcement lives nowhere visible
in the domain.

---
Summary Table

┌─────┬──────────┬──────────────┬──────────────────────────────────────────────────────────────┐
│  #  │ Severity │   Category   │                            Issue                             │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 1   │ High     │ Result       │ Inconsistent error shape (array vs single) across subtypes   │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 2   │ High     │ Validation   │ Validation<T> doesn't accumulate errors — misleading name    │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 4   │ High     │ Combinators  │ WhenAll silently drops all but the first failure             │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 17  │ High     │ Entities     │ { get; } = UtcNow timestamps may not round-trip through EF   │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 12  │ Medium   │ Value Types  │ Half the value types allow invalid/null state (no factory)   │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 13  │ Medium   │ Value Types  │ Static Equals pattern is non-standard                        │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 20  │ Medium   │ Entities     │ Invite.Inviter is a Username not a UserId                    │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 19  │ Medium   │ Entities     │ required ModeratorId? models actor as nullable instead of DU │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 24  │ Medium   │ Entities     │ ICollection<T> nav props expose mutable state                │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 11  │ Medium   │ IDs          │ No conversion between ModeratorId ↔ UserId                   │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 3   │ Low      │ Validation   │ Validation<T> missing Tap/TapAsync                           │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 5   │ Low      │ Result       │ MatchAsync failure handlers are always sync                  │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 6   │ Low      │ Extensions   │ onNotChanged vs onNotModified — name mismatch                │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 7   │ Low      │ Combinators  │ No IEnumerable-arity WhenAll                                 │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 8   │ Low      │ Extensions   │ No ToResult() extension on Task<Validation<T>>               │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 9   │ Low      │ IDs          │ SinId/AttachmentId placement inconsistency                   │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 10  │ Low      │ IDs          │ SinId missing TryParse, signed vs unsigned unexplained       │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 14  │ Low      │ Value Types  │ Attachment.FileName is raw string                            │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 15  │ Low      │ Value Types  │ PluralKit SystemId/MemberId are raw strings                  │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 16  │ Low      │ Value Types  │ CustomCommand.Content is raw string                          │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 18  │ Low      │ Entities     │ Sin.Id uses private set, breaks record immutability          │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 21  │ Low      │ Entities     │ Invite has no GuildId                                        │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 22  │ Low      │ Entities     │ LeaderboardView is not sealed                                │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 23  │ Low      │ Entities     │ XpHistory.TimeOut name is misleading                         │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 25  │ Low      │ Entities     │ MessageHistoryEntry.TimeStamp casing inconsistency           │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 26  │ Low      │ Entities     │ Dual ProxiedMessageLink on Message is confusing              │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 27  │ Low      │ Architecture │ EF navigation properties in domain entities                  │
├─────┼──────────┼──────────────┼──────────────────────────────────────────────────────────────┤
│ 28  │ Low      │ Architecture │ No aggregate roots or encapsulated invariants                │
└─────┴──────────┴──────────────┴──────────────────────────────────────────────────────────────┘

The three highest-priority fixes are: (2) give Validation<T> real applicative combining so it earns its name, (17) replace the
defaulted-timestamp anti-pattern with required init timestamps so EF round-trips are unambiguous, and (12) apply the smart-constructor
pattern uniformly across all value types that currently have public constructors.

