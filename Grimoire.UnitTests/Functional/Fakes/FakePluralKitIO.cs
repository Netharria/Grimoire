// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.PluralKit.Models;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.PluralKit;
using LanguageExt;

namespace Grimoire.UnitTests.Functional.Fakes;

/// <summary>
///     PluralKit's message lookup (spec 1.5, section 3.11). Answers for either id of a message it knows, and <c>None</c>
///     for anything else, as PluralKit's 404/20006 does. Every call is in the <see cref="Journal" /> as
///     <c>pluralkit messages/{id}</c>. Its retry has no delay.
/// </summary>
public sealed class FakePluralKitIO(Journal journal) : IPluralKitIO
{
    private readonly Atom<Seq<(Error Error, int Remaining)>> _failures = Atom(Seq<(Error, int)>());
    private readonly Atom<HashMap<ulong, PluralKitMessage>> _messages = Atom(HashMap<ulong, PluralKitMessage>());

    /// <summary>How long each call takes, to test the cap. Real time: keep it short.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public int Calls => journal.Entries.Count(entry => entry.StartsWith("pluralkit ", StringComparison.Ordinal));

    public bool IsEnabled { get; set; } = true;

    public Schedule ReadRetry => Schedule.recurs(2);

    public TimeSpan Cap { get; set; } = TimeSpan.FromSeconds(15);

    public IO<Option<PluralKitMessage>> GetMessage(MessageId message) =>
        IO.liftAsync<Fin<Option<PluralKitMessage>>>(async env =>
        {
            journal.Add($"pluralkit messages/{message.Value}");
            if (this.Delay > TimeSpan.Zero)
                await Task.Delay(this.Delay, env.Token);
            return this.TakeFailure().Case is Error error
                ? Fin.Fail<Option<PluralKitMessage>>(error)
                : Fin.Succ(this._messages.Value.Find(message.Value));
        }).Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<Option<PluralKitMessage>>));

    /// <summary>PluralKit knows this message, by its proxy id and by its original id.</summary>
    public FakePluralKitIO Knows(PluralKitMessage message)
    {
        foreach (var id in new[] { message.Id, message.OriginalId })
            if (ulong.TryParse(id, out var key))
                this._messages.Swap(messages => messages.AddOrUpdate(key, message));
        return this;
    }

    /// <summary>The next <paramref name="times" /> calls fail with <paramref name="error" />, then calls succeed.</summary>
    public FakePluralKitIO Fails(Error error, int times = int.MaxValue)
    {
        this._failures.Swap(failures => failures.Add((error, times)));
        return this;
    }

    /// <summary>A 5xx as the live edge reports it: transient, so it's retried.</summary>
    public static Error Outage() =>
        PluralKitErrors.FromResponse(System.Net.HttpStatusCode.BadGateway, string.Empty)
            .Match(Succ: _ => throw new InvalidOperationException(), Fail: error => error);

    /// <summary>A 401 as the live edge reports it: a refusal, never retried.</summary>
    public static Error Refused() =>
        PluralKitErrors.FromResponse(System.Net.HttpStatusCode.Unauthorized,
                """{"message": "401: Missing or invalid Authorization header", "code": 0}""")
            .Match(Succ: _ => throw new InvalidOperationException(), Fail: error => error);

    private Option<Error> TakeFailure()
    {
        if (this._failures.Value.Head.Case is not (Error error, int remaining and > 0))
            return None;

        this._failures.Swap(failures => remaining > 1 ? [(error, remaining - 1), ..failures.Tail] : failures.Tail);
        return error;
    }
}
