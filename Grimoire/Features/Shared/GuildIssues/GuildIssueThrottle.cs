// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;

namespace Grimoire.Features.Shared.GuildIssues;

/// <summary>Lets each problem be shown to a guild's moderators at most once a day. Thread-safe.</summary>
public sealed class GuildIssueThrottle(TimeProvider timeProvider)
{
    private readonly AlertCooldown _cooldown = new(TimeSpan.FromDays(1), timeProvider);
    private readonly Lock _lock = new();

    /// <returns><c>null</c> if it was already shown within a day; otherwise how many times it happened meanwhile.</returns>
    public int? TryAcquire(GuildId guildId, string code)
    {
        lock (this._lock)
            return this._cooldown.TryAcquire($"{guildId}:{code}");
    }
}
