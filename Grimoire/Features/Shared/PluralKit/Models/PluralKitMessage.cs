// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

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
    /// <summary>A system or member, by PluralKit's short id.</summary>
    public sealed record Entity([property: JsonPropertyName("id")] string? Id);
}
