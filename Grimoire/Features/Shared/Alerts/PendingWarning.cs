// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Alerts;

public sealed record PendingWarning(
    string Key,
    string Type,
    string? ExceptionType,
    string SampleMessage,
    int Count,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    IReadOnlySet<GuildId> GuildIds)
{
    public const int MaxGuildIds = 20;
    public const int MaxSampleLength = 1000;

    public static PendingWarning From(Alert alert, string key, DateTimeOffset now) => new(
        key,
        alert.Discriminator is null ? alert.Type : $"{alert.Type} ({alert.Discriminator})",
        alert.Exception?.GetType().FullName,
        alert.Message.Length > MaxSampleLength ? alert.Message[..MaxSampleLength] : alert.Message,
        1,
        now,
        now,
        alert.GuildId is { } guildId ? new HashSet<GuildId> { guildId } : new HashSet<GuildId>());

    public PendingWarning Merge(PendingWarning other) => this with
    {
        Count = this.Count + other.Count,
        FirstSeen = this.FirstSeen < other.FirstSeen ? this.FirstSeen : other.FirstSeen,
        LastSeen = this.LastSeen > other.LastSeen ? this.LastSeen : other.LastSeen,
        GuildIds = this.GuildIds.Union(other.GuildIds).Take(MaxGuildIds).ToHashSet()
    };
}
