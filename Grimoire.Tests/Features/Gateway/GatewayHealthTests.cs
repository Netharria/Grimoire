// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Gateway;
using Grimoire.Tests.Features.Alerts;

namespace Grimoire.Tests.Features.Gateway;

public sealed class GatewayHealthTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_gateway_that_was_never_disconnected_reports_no_downtime()
    {
        var health = new GatewayHealth(this._time);

        health.DisconnectedFor().ShouldBeNull();
        health.MarkConnected().ShouldBeNull();
    }

    [Fact]
    public void Downtime_is_measured_from_the_first_disconnect()
    {
        var health = new GatewayHealth(this._time);
        health.MarkDisconnected();
        this._time.Advance(TimeSpan.FromSeconds(30));
        health.MarkDisconnected(); // a repeated close event must not reset the clock
        this._time.Advance(TimeSpan.FromSeconds(30));

        health.DisconnectedFor().ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Reconnecting_returns_the_downtime_and_clears_it()
    {
        var health = new GatewayHealth(this._time);
        health.MarkDisconnected();
        this._time.Advance(TimeSpan.FromMinutes(3));

        health.MarkConnected().ShouldBe(TimeSpan.FromMinutes(3));
        health.DisconnectedFor().ShouldBeNull();
        health.MarkConnected().ShouldBeNull();
    }
}
