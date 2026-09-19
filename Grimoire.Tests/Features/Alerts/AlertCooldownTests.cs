// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;

namespace Grimoire.Tests.Features.Alerts;

public sealed class AlertCooldownTests
{
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(5);

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void First_alert_goes_out_with_no_suppressed_repeats()
        => new AlertCooldown(_window, this._time).TryAcquire("a").ShouldBe(0);

    [Fact]
    public void Repeat_inside_the_window_is_suppressed()
    {
        var cooldown = new AlertCooldown(_window, this._time);
        cooldown.TryAcquire("a");
        this._time.Advance(TimeSpan.FromMinutes(1));

        cooldown.TryAcquire("a").ShouldBeNull();
    }

    [Fact]
    public void After_the_window_the_alert_reports_how_many_repeats_were_suppressed()
    {
        var cooldown = new AlertCooldown(_window, this._time);
        cooldown.TryAcquire("a");
        cooldown.TryAcquire("a");
        cooldown.TryAcquire("a");
        this._time.Advance(_window);

        cooldown.TryAcquire("a").ShouldBe(2);
        cooldown.TryAcquire("a").ShouldBeNull();
    }

    [Fact]
    public void Keys_are_independent()
    {
        var cooldown = new AlertCooldown(_window, this._time);
        cooldown.TryAcquire("a");

        cooldown.TryAcquire("b").ShouldBe(0);
    }
}
