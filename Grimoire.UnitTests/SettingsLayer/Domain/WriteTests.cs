// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


namespace Grimoire.UnitTests.SettingsLayer.Domain;

/// <summary>The outcome of a settings write (spec 1.1, 3.3).</summary>
public sealed class WriteTests
{
    [Fact]
    public void Map_maps_a_saved_value()
        => new Write<int>.Saved(2).Map(x => x * 10).ShouldBe(new Write<int>.Saved(20));

    [Fact]
    public void Map_keeps_an_unchanged_write_and_its_message()
        => new Write<int>.Unchanged("already set").Map(x => x * 10).ShouldBe(new Write<int>.Unchanged("already set"));

    [Fact]
    public void Match_picks_the_branch_for_the_case()
    {
        new Write<int>.Saved(3).Match(saved: x => $"saved {x}", unchanged: m => m).ShouldBe("saved 3");
        new Write<int>.Unchanged("same").Match(saved: x => $"saved {x}", unchanged: m => m).ShouldBe("same");
    }
}
