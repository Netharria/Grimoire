// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json.Serialization;
using Grimoire.Functional.PluralKit;
using LanguageExt;

namespace Grimoire.Features.Shared.PluralKit.Models;

/// <summary>
///     The parts of PluralKit's message object the bot reads. Everything else is left out on purpose: each extra
///     required or typed field is one more way for a change to PluralKit's API to make the lookup fail.
/// </summary>
public sealed record PluralKitMessage(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("original")] string? OriginalId,
    [property: JsonPropertyName("system")] PluralKitMessage.Entity? PluralKitSystem,
    [property: JsonPropertyName("member")] PluralKitMessage.Entity? Member)
{
    /// <summary>
    ///     What this answer means for the message that was deleted (LOG-4, Q37). PluralKit's ids are parsed here, once,
    ///     at the edge. An answer that can't be read, or that is about another message, is <c>Unavailable</c>, so the
    ///     delete is logged as usual rather than linked wrongly.
    /// </summary>
    public Fin<ProxyLookup> Classify(MessageId deleted)
    {
        if (Snowflake(this.Id).Case is not ulong proxy || Snowflake(this.OriginalId).Case is not ulong original)
            return PluralKitErrors.Unreadable("the message ids are missing or not snowflakes");
        if (proxy == deleted.Value)
            return new ProxyLookup.IsTheProxy();
        return original == deleted.Value
            ? this.Replaced(new MessageId(proxy), new MessageId(original))
            : PluralKitErrors.Unreadable("the answer is about another message");
    }

    private Fin<ProxyLookup> Replaced(MessageId proxy, MessageId original) =>
        (this.PluralKitSystem?.Id, this.Member?.Id) switch
        {
            // PluralKit sends no system or member when the member was deleted (Q37).
            (null, _) or (_, null) => new ProxyLookup.ReplacedByProxyOfDeletedMember(proxy),
            var (system, member) =>
                (from systemId in PluralKitSystemId.Create(system).ToFin()
                    from memberId in PluralKitMemberId.Create(member).ToFin()
                    select (ProxyLookup)new ProxyLookup.ReplacedByProxy(new ProxiedMessageLink
                    {
                        ProxyMessageId = proxy,
                        OriginalMessageId = original,
                        SystemId = systemId,
                        MemberId = memberId
                    }))
                .MapFail(_ => PluralKitErrors.Unreadable("a system or member id is blank"))
        };

    private static Option<ulong> Snowflake(string? value) =>
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? Some(id)
            : None;

    /// <summary>A system or member, by PluralKit's short id.</summary>
    public sealed record Entity([property: JsonPropertyName("id")] string? Id);
}
