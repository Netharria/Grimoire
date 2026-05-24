// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

public sealed class ValidationCombinatorTests
{
    private static readonly Error _errorA = new("code.a", "Error A");
    private static readonly Error _errorB = new("code.b", "Error B");
    private static readonly Error _errorC = new("code.c", "Error C");

    // ── Validation.Combine — 2-tuple ─────────────────────────────────────────

    [Fact]
    public void Combine2_BothValid_ReturnsSuccessWithBothValues()
    {
        var result = Validation.Combine(
            Validation<int>.Succeed(1),
            Validation<string>.Succeed("a"));
        var (i, s) = result.ShouldSucceed();
        i.ShouldBe(1);
        s.ShouldBe("a");
    }

    [Fact]
    public void Combine2_FirstInvalid_PropagatesItsErrors()
    {
        var result = Validation.Combine(
            Validation<int>.Fail(_errorA),
            Validation<string>.Succeed("a"));
        result.ShouldBeOfType<Validation<(int, string)>.Invalid>()
            .Errors.ShouldContain(_errorA);
    }

    [Fact]
    public void Combine2_SecondInvalid_PropagatesItsErrors()
    {
        var result = Validation.Combine(
            Validation<int>.Succeed(1),
            Validation<string>.Fail(_errorB));
        result.ShouldBeOfType<Validation<(int, string)>.Invalid>()
            .Errors.ShouldContain(_errorB);
    }

    [Fact]
    public void Combine2_BothInvalid_AccumulatesAllErrors()
    {
        var result = Validation.Combine(
            Validation<int>.Fail(_errorA),
            Validation<string>.Fail(_errorB));
        var invalid = result.ShouldBeOfType<Validation<(int, string)>.Invalid>();
        invalid.Errors.ShouldContain(_errorA);
        invalid.Errors.ShouldContain(_errorB);
        invalid.Errors.Length.ShouldBe(2);
    }

    // ── Validation.Combine — 3-tuple ─────────────────────────────────────────

    [Fact]
    public void Combine3_AllValid_ReturnsSuccessWithAllValues()
    {
        var result = Validation.Combine(
            Validation<int>.Succeed(1),
            Validation<string>.Succeed("b"),
            Validation<bool>.Succeed(true));
        var (i, s, b) = result.ShouldSucceed();
        i.ShouldBe(1);
        s.ShouldBe("b");
        b.ShouldBeTrue();
    }

    [Fact]
    public void Combine3_AllInvalid_AccumulatesAllErrors()
    {
        var result = Validation.Combine(
            Validation<int>.Fail(_errorA),
            Validation<string>.Fail(_errorB),
            Validation<bool>.Fail(_errorC));
        var invalid = result.ShouldBeOfType<Validation<(int, string, bool)>.Invalid>();
        invalid.Errors.ShouldContain(_errorA);
        invalid.Errors.ShouldContain(_errorB);
        invalid.Errors.ShouldContain(_errorC);
        invalid.Errors.Length.ShouldBe(3);
    }

    // ── Result.WhenAll — typed Task overloads ────────────────────────────────

    [Fact]
    public async Task WhenAll2_BothSucceed_ReturnsTuple()
    {
        var (a, b) = await Result.WhenAll(
            Task.FromResult(Result<int>.Ok(1)),
            Task.FromResult(Result<string>.Ok("x"))).ShouldSucceed();
        a.ShouldBe(1);
        b.ShouldBe("x");
    }

    [Fact]
    public async Task WhenAll2_FirstFails_ReturnsInvalid()
    {
        var result = await Result.WhenAll(
            Task.FromResult(Result<int>.Fail(_errorA)),
            Task.FromResult(Result<string>.Ok("x")));
        result.ShouldBeOfType<Result<(int, string)>.Invalid>().Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task WhenAll2_BothFail_CombinesErrors()
    {
        var result = await Result.WhenAll(
            Task.FromResult(Result<int>.Fail(_errorA)),
            Task.FromResult(Result<string>.Fail(_errorB)));
        var invalid = result.ShouldBeOfType<Result<(int, string)>.Invalid>();
        invalid.Error.Message.ShouldContain(_errorA.Message);
        invalid.Error.Message.ShouldContain(_errorB.Message);
    }

    // ── Result.WhenAll — IEnumerable<Task<Result<T>>> ────────────────────────

    [Fact]
    public async Task WhenAllList_AllSucceed_ReturnsFullList()
    {
        var tasks = new[]
        {
            Task.FromResult(Result<int>.Ok(1)), Task.FromResult(Result<int>.Ok(2)),
            Task.FromResult(Result<int>.Ok(3))
        };
        var list = await Result.WhenAll(tasks).ShouldSucceed();
        list.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task WhenAllList_SingleFail_ReturnsInvalidWithThatExactError()
    {
        var tasks = new[]
        {
            Task.FromResult(Result<int>.Ok(1)), Task.FromResult(Result<int>.Fail(_errorA)),
            Task.FromResult(Result<int>.Ok(3))
        };
        var result = await Result.WhenAll(tasks);
        result.ShouldBeOfType<Result<IReadOnlyList<int>>.Invalid>().Error.ShouldBe(_errorA);
    }

    [Fact]
    public async Task WhenAllList_MultipleFail_CombinesDistinctErrorMessages()
    {
        var tasks = new[]
        {
            Task.FromResult(Result<int>.Fail(_errorA)), Task.FromResult(Result<int>.Ok(2)),
            Task.FromResult(Result<int>.Fail(_errorB))
        };
        var result = await Result.WhenAll(tasks);
        var invalid = result.ShouldBeOfType<Result<IReadOnlyList<int>>.Invalid>();
        invalid.Error.Message.ShouldContain(_errorA.Message);
        invalid.Error.Message.ShouldContain(_errorB.Message);
    }

    [Fact]
    public async Task WhenAllList_DuplicateFailures_DeduplicatesBeforeCombining()
    {
        var tasks = new[] { Task.FromResult(Result<int>.Fail(_errorA)), Task.FromResult(Result<int>.Fail(_errorA)) };
        var result = await Result.WhenAll(tasks);
        // Both failures carry the same error, so after dedup there is one — returned as-is.
        result.ShouldBeOfType<Result<IReadOnlyList<int>>.Invalid>().Error.ShouldBe(_errorA);
    }
}
