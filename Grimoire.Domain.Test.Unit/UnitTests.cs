// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

using Unit = global::Grimoire.Domain.Unit;

public sealed class UnitTests
{
    [Fact]
    public void Value_ReturnsDefaultInstance()
        => Unit.Value.ShouldBe(default(Unit));

    [Fact]
    public void AllInstances_AreEqual()
        => Unit.Value.ShouldBe(new Unit());

    [Fact]
    public void AsResultValue_RoundTripsThroughMatch()
        => Result<Unit>.Ok(Unit.Value).Match(v => v, _ => throw new InvalidOperationException())
            .ShouldBe(Unit.Value);
}
