// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Grimoire.Features.Shared.PluralKit.Models;

public sealed record PluralKitMessage
{
    [JsonPropertyName("timestamp")] public required DateTime Timestamp { get; init; }

    [JsonPropertyName("id")] public required string Id { get; init; }

    [JsonPropertyName("original")] public required string OriginalId { get; init; }

    [JsonPropertyName("sender")] public required string SenderId { get; init; }

    [JsonPropertyName("channel")] public required string ChannelId { get; init; }

    [JsonPropertyName("guild")] public required string GuildId { get; init; }

    [JsonPropertyName("system")] public PluralKitSystem? PluralKitSystem { get; init; }

    [JsonPropertyName("member")] public PluralKitMember? Member { get; init; }
}
