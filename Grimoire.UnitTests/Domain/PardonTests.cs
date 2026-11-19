// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.UnitTests.Domain;

public sealed class PardonTests
{
    /// <summary>A reason's time is part of its key, so it is held as Postgres stores it (spec 2.3, section 3.1).</summary>
    [Fact]
    public void AReasonRow_IsTruncatedToTheMicrosecond()
    {
        var at = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);

        var row = PardonReasonHistory.Create(new SinId(1), ModerationReason.Create("sorry").ShouldSucceed(),
            new ModeratorId(9), at);

        row.SetAt.ShouldBe(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
        row.SetAt.UtcTicks.ShouldBe(row.SetAt.UtcTicks / TimeSpan.TicksPerMicrosecond * TimeSpan.TicksPerMicrosecond);
        (row.SinId, row.Reason.Value, row.ModeratorId).ShouldBe((new SinId(1), "sorry", new ModeratorId(9)));
    }

    [Fact]
    public void ATimeAlreadyInMicroseconds_IsKept()
    {
        var at = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero).AddTicks(10);

        PardonReasonHistory.Create(new SinId(1), ModerationReason.Create("x").ShouldSucceed(), new ModeratorId(9), at)
            .SetAt.ShouldBe(at);
    }
}
