// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

/// <summary>
///     A warning alert aggregated by <see cref="AlertKey" />. There is at most one open row
///     (<see cref="ReportedAt" /> is <c>null</c>) per key; repeats update it instead of adding rows.
/// </summary>
[UsedImplicitly]
public sealed record AlertOccurrence
{
    public long Id { get; init; }
    public required string AlertKey { get; init; }
    public required string AlertType { get; init; }
    public string? ExceptionType { get; init; }
    public required int Count { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required string SampleMessage { get; init; }
    public required GuildId[] GuildIds { get; init; }
    public DateTimeOffset? ReportedAt { get; init; }
}
