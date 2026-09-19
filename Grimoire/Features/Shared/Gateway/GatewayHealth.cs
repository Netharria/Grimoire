// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Gateway;

/// <summary>Tracks how long the Discord gateway connection has been down. Thread-safe.</summary>
public sealed class GatewayHealth(TimeProvider timeProvider)
{
    private readonly Lock _lock = new();
    private DateTimeOffset? _disconnectedSince;

    /// <summary>Keeps the earliest time if already disconnected, so repeated close events don't reset the clock.</summary>
    public void MarkDisconnected()
    {
        lock (this._lock)
            this._disconnectedSince ??= timeProvider.GetUtcNow();
    }

    /// <returns>How long the gateway was down, or <c>null</c> if it wasn't known to be down.</returns>
    public TimeSpan? MarkConnected()
    {
        lock (this._lock)
        {
            var downtime = this.DisconnectedFor();
            this._disconnectedSince = null;
            return downtime;
        }
    }

    public TimeSpan? DisconnectedFor()
    {
        lock (this._lock)
            return this._disconnectedSince is { } since ? timeProvider.GetUtcNow() - since : null;
    }
}
