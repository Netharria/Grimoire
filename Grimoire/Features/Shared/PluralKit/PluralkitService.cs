// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Functional.Runtime;
using LanguageExt;
using Microsoft.Extensions.Hosting;

namespace Grimoire.Features.Shared.PluralKit;

/// <summary>
///     The bridge for <c>DeleteMessageEvent</c>, which stays on EF until section 3.10 (spec 1.5, section 3.9). 3.10
///     calls <see cref="DeletedMessageLookup{RT}" /> from the ported handler and deletes this.
/// </summary>
public interface IPluralkitService
{
    /// <summary>What PluralKit says about a deleted message. A failure has been logged and is answered as NotProxied.</summary>
    ValueTask<ProxyLookup> Lookup(MessageId deleted, DateTimeOffset createdAt);
}

public sealed class PluralkitService(GrimoireEnv env, IHostApplicationLifetime lifetime) : IPluralkitService
{
    public async ValueTask<ProxyLookup> Lookup(MessageId deleted, DateTimeOffset createdAt)
    {
        using var envIO = EnvIO.New(token: lifetime.ApplicationStopping);
        var result = await DeletedMessageLookup<GrimoireRuntime>.Run(deleted, createdAt)
            .RunAsync(new GrimoireRuntime(env), envIO);
        // Only cancellation gets through, at shutdown; the delete is then handled as an ordinary one.
        return result.Match(Succ: lookup => lookup, Fail: _ => new ProxyLookup.NotProxied());
    }
}
