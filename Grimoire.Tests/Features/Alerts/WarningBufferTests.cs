// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;

namespace Grimoire.Tests.Features.Alerts;

public sealed class WarningBufferTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Repeats_of_the_same_alert_are_counted_in_one_entry()
    {
        var buffer = new WarningBuffer(this._time);
        buffer.Add(AlertTestHelpers.Warning());
        this._time.Advance(TimeSpan.FromMinutes(1));
        buffer.Add(AlertTestHelpers.Warning());

        var entry = buffer.Drain().ShouldHaveSingleItem();
        entry.Count.ShouldBe(2);
        entry.FirstSeen.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        entry.LastSeen.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Guilds_are_collected_across_repeats()
    {
        var buffer = new WarningBuffer(this._time);
        buffer.Add(AlertTestHelpers.Warning(guild: 1));
        buffer.Add(AlertTestHelpers.Warning(guild: 2));
        buffer.Add(AlertTestHelpers.Warning(guild: 2));

        buffer.Drain().ShouldHaveSingleItem().GuildIds.ShouldBe([new GuildId(1), new GuildId(2)], true);
    }

    [Fact]
    public void Different_discriminators_are_separate_entries()
    {
        var buffer = new WarningBuffer(this._time);
        buffer.Add(AlertTestHelpers.Warning(discriminator: "level"));
        buffer.Add(AlertTestHelpers.Warning(discriminator: "mute"));

        buffer.Drain().Count.ShouldBe(2);
    }

    [Fact]
    public void Drain_empties_the_buffer()
    {
        var buffer = new WarningBuffer(this._time);
        buffer.Add(AlertTestHelpers.Warning());
        buffer.Drain();

        buffer.Drain().ShouldBeEmpty();
    }

    [Fact]
    public void New_keys_are_dropped_once_the_buffer_is_full_but_existing_keys_still_count()
    {
        var buffer = new WarningBuffer(this._time);
        for (var i = 0; i < WarningBuffer.MaxKeys; i++)
            buffer.Add(AlertTestHelpers.Warning(discriminator: $"cmd{i}")).ShouldBeTrue();

        buffer.Add(AlertTestHelpers.Warning(discriminator: "overflow")).ShouldBeFalse();
        buffer.Add(AlertTestHelpers.Warning(discriminator: "cmd0")).ShouldBeTrue();

        var drained = buffer.Drain();
        drained.Count.ShouldBe(WarningBuffer.MaxKeys);
        drained.Single(x => x.Type == "SlowCommand (cmd0)").Count.ShouldBe(2);
    }

    [Fact]
    public void Guild_list_is_capped()
    {
        var buffer = new WarningBuffer(this._time);
        for (ulong guild = 1; guild <= PendingWarning.MaxGuildIds + 10; guild++)
            buffer.Add(AlertTestHelpers.Warning(guild: guild));

        buffer.Drain().ShouldHaveSingleItem().GuildIds.Count.ShouldBe(PendingWarning.MaxGuildIds);
    }
}
