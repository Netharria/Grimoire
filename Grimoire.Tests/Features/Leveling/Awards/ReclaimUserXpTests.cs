// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Leveling.Awards;

namespace Grimoire.Tests.Features.Leveling.Awards;

public sealed class ReclaimUserXpValidateOptionTests
{
    [Fact]
    public void AmountOption_ZeroAmount_Fails()
        => ReclaimUserXp.ValidateOption(ReclaimUserXp.XpOption.Amount, 0)
            .ShouldBeOfType<Result<Unit>.Invalid>()
            .Error.Code.ShouldBe("reclaim.amount.zero");

    [Fact]
    public void AmountOption_PositiveAmount_Succeeds()
        => ReclaimUserXp.ValidateOption(ReclaimUserXp.XpOption.Amount, 5)
            .ShouldBeOfType<Result<Unit>.Success>();

    [Fact]
    public void AllOption_ZeroAmount_Succeeds()
        => ReclaimUserXp.ValidateOption(ReclaimUserXp.XpOption.All, 0)
            .ShouldBeOfType<Result<Unit>.Success>();

    [Fact]
    public void AllOption_NonZeroAmount_StillSucceeds_AmountIsIgnored()
        => ReclaimUserXp.ValidateOption(ReclaimUserXp.XpOption.All, 5)
            .ShouldBeOfType<Result<Unit>.Success>();
}
