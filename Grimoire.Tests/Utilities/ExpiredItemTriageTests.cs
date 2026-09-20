// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Utilities;

namespace Grimoire.Tests.Utilities;

public sealed class ExpiredItemTriageTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Guilds_the_bot_is_not_in_are_kept_for_later_whatever_else_is_true(bool targetExists)
        => ExpiredItemTriage.For(false, targetExists).ShouldBe(ExpiredItemAction.KeepForLater);

    [Fact]
    public void A_missing_target_in_a_guild_the_bot_is_in_is_resolved()
        => ExpiredItemTriage.For(true, false).ShouldBe(ExpiredItemAction.Resolve);

    [Fact]
    public void An_existing_target_in_a_guild_the_bot_is_in_is_processed()
        => ExpiredItemTriage.For(true, true).ShouldBe(ExpiredItemAction.Process);
}
