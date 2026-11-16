// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.PluralKit;

/// <summary>What PluralKit says about a message that was just deleted, from the delete handler's side (spec 1.5, 3.2).</summary>
public abstract record ProxyLookup
{
    private ProxyLookup()
    {
    }

    /// <summary>Not asked: PluralKit is off, or the message is over 60 s old.</summary>
    public sealed record NotAsked : ProxyLookup;

    /// <summary>PluralKit doesn't know it (404, code 20006), or the lookup failed and the delete is logged as usual.</summary>
    public sealed record NotProxied : ProxyLookup;

    /// <summary>PluralKit deleted this original and posted a proxy. Link them; log nothing.</summary>
    public sealed record ReplacedByProxy(ProxiedMessageLink Link) : ProxyLookup;

    /// <summary>
    ///     As above, but PluralKit sent no system or member: the member was deleted (Q37). Log nothing and save no link,
    ///     since <see cref="ProxiedMessageLink" /> keeps both ids required.
    /// </summary>
    public sealed record ReplacedByProxyOfDeletedMember(MessageId Proxy) : ProxyLookup;

    /// <summary>The deleted message is the proxy itself. Logged as usual; the stored link names the author.</summary>
    public sealed record IsTheProxy : ProxyLookup;
}

/// <summary>Which deletes are worth asking PluralKit about (spec 1.5, section 3.3).</summary>
public static class PluralKitWindow
{
    /// <summary>
    ///     PluralKit deletes an original within a second or two of posting the proxy, so a delete of an older message
    ///     isn't PluralKit's. Well inside the 30 minutes PluralKit answers for originals.
    /// </summary>
    public static readonly TimeSpan Age = TimeSpan.FromSeconds(60);

    /// <summary>True up to and including <see cref="Age" />, and for a creation time ahead of the clock.</summary>
    public static bool ShouldAsk(DateTimeOffset createdAt, DateTimeOffset now) => now - createdAt <= Age;
}
