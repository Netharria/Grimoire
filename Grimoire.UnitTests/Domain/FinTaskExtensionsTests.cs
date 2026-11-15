// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.UnitTests.Domain;

#pragma warning disable CS0618 // The bridge is obsolete on purpose; these tests pin its behaviour until it goes.

/// <summary>The temporary <see cref="FinTaskExtensions" /> bridge (spec 1.1, 3.4).</summary>
public sealed class FinTaskExtensionsTests
{
    private static readonly Invalid _error = new("bad");

    private static Task<Fin<int>> Ok(int value) => Task.FromResult(Fin.Succ(value));

    private static Task<Fin<int>> Failed() => Task.FromResult(Fin.Fail<int>(_error));

    [Fact]
    public async Task MapFin_maps_a_success()
        => (await Ok(2).MapFin(x => x * 3)).ShouldSucceed().ShouldBe(6);

    [Fact]
    public async Task MapFin_keeps_a_failure_and_its_case()
        => (await Failed().MapFin(x => x * 3)).ShouldFail().ShouldBe(_error);

    [Fact]
    public async Task BindFin_skips_the_function_on_failure()
    {
        var called = false;
        (await Failed().BindFin(x =>
        {
            called = true;
            return Fin.Succ(x);
        })).ShouldFail().ShouldBe(_error);
        called.ShouldBeFalse();
    }

    [Fact]
    public async Task BindAsync_chains_a_success()
        => (await Ok(1).BindAsync(x => Ok(x + 1))).ShouldSucceed().ShouldBe(2);

    [Fact]
    public async Task BindAsync_keeps_the_failure_of_the_function()
        => (await Ok(1).BindAsync(_ => Task.FromResult(Fin.Fail<int>(new NotFound("none")))))
            .ShouldFail().ShouldBeOfType<NotFound>();

    [Fact]
    public async Task TapAsync_runs_only_on_success()
    {
        var seen = new List<int>();
        await Ok(5).TapAsync(x =>
        {
            seen.Add(x);
            return Task.CompletedTask;
        });
        await Failed().TapAsync(x =>
        {
            seen.Add(x);
            return Task.CompletedTask;
        });
        seen.ShouldBe([5]);
    }

    /// <summary>Without the <c>Task</c> overload, <c>await</c> would finish before the handler does.</summary>
    [Fact]
    public async Task Match_with_Task_handlers_awaits_the_handler()
    {
        var done = false;
        await Ok(1).Match(async _ =>
        {
            await Task.Delay(20);
            done = true;
        }, _ => Task.CompletedTask);
        done.ShouldBeTrue();
    }

    [Fact]
    public async Task Match_with_ValueTask_handlers_awaits_the_handler()
    {
        var done = false;
        await Failed().Match(_ => ValueTask.CompletedTask, async _ =>
        {
            await Task.Delay(20);
            done = true;
        });
        done.ShouldBeTrue();
    }

    [Fact]
    public async Task MatchAsync_routes_a_failure_to_onFail()
        => (await Failed().MatchAsync(x => Task.FromResult(x.ToString()), error => error.Message)).ShouldBe("bad");

    [Fact]
    public async Task OrDefaultDuringMigration_gives_the_value_or_the_fallback()
    {
        (await Ok(3).OrDefaultDuringMigration(0)).ShouldBe(3);
        (await Failed().OrDefaultDuringMigration(0)).ShouldBe(0);
    }

    [Fact]
    public void OrNullDuringMigration_gives_the_id_or_null()
    {
        Fin.Succ(Some(new RoleId(7UL))).OrNullDuringMigration().ShouldBe(new RoleId(7UL));
        Fin.Succ(Option<RoleId>.None).OrNullDuringMigration().ShouldBeNull();
        Fin.Fail<Option<RoleId>>(_error).OrNullDuringMigration().ShouldBeNull();
    }
}
