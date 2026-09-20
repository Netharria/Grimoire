// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Tests.Features.Alerts;
using Grimoire.Utilities;

namespace Grimoire.Tests.Utilities;

public sealed class RetryBackoffTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void An_item_that_never_failed_is_attempted()
        => new RetryBackoff(this._time).ShouldAttempt("a").ShouldBeTrue();

    [Fact]
    public void A_failed_item_waits_before_being_retried()
    {
        var backoff = new RetryBackoff(this._time);
        backoff.RecordFailure("a");

        backoff.ShouldAttempt("a").ShouldBeFalse();
        this._time.Advance(RetryBackoff.FirstDelay - TimeSpan.FromSeconds(1));
        backoff.ShouldAttempt("a").ShouldBeFalse();
        this._time.Advance(TimeSpan.FromSeconds(1));
        backoff.ShouldAttempt("a").ShouldBeTrue();
    }

    [Fact]
    public void The_wait_doubles_with_each_failure()
    {
        var backoff = new RetryBackoff(this._time);
        backoff.RecordFailure("a");
        this._time.Advance(RetryBackoff.FirstDelay);
        backoff.RecordFailure("a");

        this._time.Advance(RetryBackoff.FirstDelay * 2 - TimeSpan.FromSeconds(1));
        backoff.ShouldAttempt("a").ShouldBeFalse();
        this._time.Advance(TimeSpan.FromSeconds(1));
        backoff.ShouldAttempt("a").ShouldBeTrue();
    }

    [Fact]
    public void The_wait_never_exceeds_the_maximum_however_often_it_fails()
    {
        var backoff = new RetryBackoff(this._time);
        for (var i = 0; i < 100; i++)
        {
            backoff.RecordFailure("a");
            this._time.Advance(RetryBackoff.MaxDelay);
        }

        backoff.RecordFailure("a");
        this._time.Advance(RetryBackoff.MaxDelay);
        backoff.ShouldAttempt("a").ShouldBeTrue();
    }

    [Fact]
    public void Success_clears_the_wait_and_the_count()
    {
        var backoff = new RetryBackoff(this._time);
        backoff.RecordFailure("a");
        backoff.RecordFailure("a");
        backoff.RecordSuccess("a");

        backoff.ShouldAttempt("a").ShouldBeTrue();
        backoff.RecordFailure("a");
        this._time.Advance(RetryBackoff.FirstDelay);
        backoff.ShouldAttempt("a").ShouldBeTrue();
    }

    [Fact]
    public void Items_are_tracked_independently()
    {
        var backoff = new RetryBackoff(this._time);
        backoff.RecordFailure("a");

        backoff.ShouldAttempt("b").ShouldBeTrue();
    }
}
