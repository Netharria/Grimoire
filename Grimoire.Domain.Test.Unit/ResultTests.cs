// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

public sealed class ResultTests
{
    private static readonly Error _errorA = new("code.a", "Error A");
    private static readonly Error _errorB = new("code.b", "Error B");

    // ── Construction ─────────────────────────────────────────────────────────

    [Fact]
    public void Ok_IsSuccess()
        => Result<int>.Ok(42).ShouldBeOfType<Result<int>.Success>().Value.ShouldBe(42);

    [Fact]
    public void Fail_IsInvalid()
        => Result<int>.Fail(_errorA).ShouldBeOfType<Result<int>.Invalid>().Error.ShouldBe(_errorA);

    // ── Map ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_OnSuccess_TransformsValue()
        => Result<int>.Ok(3).Map(x => x * 2).ShouldSucceed().ShouldBe(6);

    [Fact]
    public void Map_OnInvalid_PropagatesWithoutCallingMapper()
    {
        var called = false;
        Result<int>.Fail(_errorA).Map(x => { called = true; return x; });
        called.ShouldBeFalse();
    }

    [Fact]
    public void Map_OnNotFound_PropagatesAsNotFound()
    {
        var result = new Result<int>.NotFound(_errorA).Map(x => x.ToString());
        result.ShouldBeOfType<Result<string>.NotFound>().Error.ShouldBe(_errorA);
    }

    [Fact]
    public void Map_OnConflict_PropagatesAsConflict()
    {
        var result = new Result<int>.Conflict(_errorA).Map(x => x.ToString());
        result.ShouldBeOfType<Result<string>.Conflict>().Error.ShouldBe(_errorA);
    }

    [Fact]
    public void Map_OnForbidden_PropagatesAsForbidden()
    {
        var result = new Result<int>.Forbidden(_errorA).Map(x => x.ToString());
        result.ShouldBeOfType<Result<string>.Forbidden>().Error.ShouldBe(_errorA);
    }

    [Fact]
    public void Map_OnNotModified_PropagatesAsNotModified()
    {
        var result = new Result<int>.NotModified(_errorA).Map(x => x.ToString());
        result.ShouldBeOfType<Result<string>.NotModified>().Error.ShouldBe(_errorA);
    }

    // ── Bind ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_OnSuccess_AppliesBinder()
        => Result<int>.Ok(5)
            .Bind(x => Result<string>.Ok($"val:{x}"))
            .ShouldSucceed()
            .ShouldBe("val:5");

    [Fact]
    public void Bind_OnSuccess_BinderReturnsFail_PropagatesError()
        => Result<int>.Ok(5)
            .Bind(_ => Result<string>.Fail(_errorA))
            .ShouldBeOfType<Result<string>.Invalid>()
            .Error.ShouldBe(_errorA);

    [Fact]
    public void Bind_OnInvalid_DoesNotCallBinder()
    {
        var called = false;
        Result<int>.Fail(_errorA).Bind(x => { called = true; return Result<string>.Ok($"{x}"); });
        called.ShouldBeFalse();
    }

    [Fact]
    public void Bind_OnNotFound_PropagatesAsNotFound()
    {
        var result = new Result<int>.NotFound(_errorA).Bind(x => Result<string>.Ok($"{x}"));
        result.ShouldBeOfType<Result<string>.NotFound>().Error.ShouldBe(_errorA);
    }

    // ── Tap ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Tap_OnSuccess_ExecutesAction()
    {
        var seen = -1;
        Result<int>.Ok(7).Tap(x => seen = x);
        seen.ShouldBe(7);
    }

    [Fact]
    public void Tap_OnInvalid_DoesNotExecuteAction()
    {
        var called = false;
        Result<int>.Fail(_errorA).Tap(_ => called = true);
        called.ShouldBeFalse();
    }

    [Fact]
    public void Tap_ReturnsSameResult()
    {
        var result = Result<int>.Ok(1);
        result.Tap(_ => { }).ShouldBe(result);
    }

    // ── Match ────────────────────────────────────────────────────────────────

    [Fact]
    public void Match_OnSuccess_CallsOnSuccess()
        => Result<int>.Ok(9).Match(v => v * 10, _ => -1).ShouldBe(90);

    [Fact]
    public void Match_OnInvalid_CallsOnFailure()
        => Result<int>.Fail(_errorA).Match(_ => 1, e => e.Code.Length).ShouldBe(_errorA.Code.Length);

    [Fact]
    public void Match_OnNotFound_WithSpecificHandler_CallsHandler()
        => new Result<int>.NotFound(_errorA)
            .Match(_ => 0, _ => 1, onNotFound: _ => 2)
            .ShouldBe(2);

    [Fact]
    public void Match_OnNotFound_WithoutSpecificHandler_FallsBackToOnFailure()
        => new Result<int>.NotFound(_errorA)
            .Match(_ => 0, _ => 1)
            .ShouldBe(1);

    [Fact]
    public void Match_OnConflict_WithSpecificHandler_CallsHandler()
        => new Result<int>.Conflict(_errorA)
            .Match(_ => 0, _ => 1, onConflict: _ => 3)
            .ShouldBe(3);

    [Fact]
    public void Match_OnForbidden_WithSpecificHandler_CallsHandler()
        => new Result<int>.Forbidden(_errorA)
            .Match(_ => 0, _ => 1, onForbidden: _ => 4)
            .ShouldBe(4);

    [Fact]
    public void Match_OnNotModified_WithSpecificHandler_CallsHandler()
        => new Result<int>.NotModified(_errorA)
            .Match(_ => 0, _ => 1, onNotModified: _ => 5)
            .ShouldBe(5);

    // ── OrElse / GetOrElse ───────────────────────────────────────────────────

    [Fact]
    public void OrElse_OnSuccess_ReturnsSelf()
    {
        var result = Result<int>.Ok(1);
        var called = false;
        result.OrElse(() => { called = true; return Result<int>.Ok(99); }).ShouldBe(result);
        called.ShouldBeFalse();
    }

    [Fact]
    public void OrElse_OnInvalid_ReturnsFallback()
        => Result<int>.Fail(_errorA)
            .OrElse(() => Result<int>.Ok(99))
            .ShouldSucceed()
            .ShouldBe(99);

    [Fact]
    public void GetOrElse_OnSuccess_ReturnsValue()
        => Result<int>.Ok(42).GetOrElse(() => -1).ShouldBe(42);

    [Fact]
    public void GetOrElse_OnInvalid_ReturnsFallbackValue()
        => Result<int>.Fail(_errorA).GetOrElse(() => -1).ShouldBe(-1);

    // ── ToValidation ─────────────────────────────────────────────────────────

    [Fact]
    public void ToValidation_OnSuccess_ReturnsValid()
        => Result<int>.Ok(5).ToValidation().ShouldSucceed().ShouldBe(5);

    [Fact]
    public void ToValidation_OnInvalid_ReturnsInvalidWithSameError()
        => Result<int>.Fail(_errorA).ToValidation()
            .ShouldBeOfType<Validation<int>.Invalid>()
            .Errors.ShouldHaveSingleItem().ShouldBe(_errorA);

    [Fact]
    public void ToValidation_OnNotFound_ReturnsInvalid()
        => new Result<int>.NotFound(_errorA).ToValidation()
            .ShouldBeOfType<Validation<int>.Invalid>()
            .Errors.ShouldContain(_errorA);

    [Fact]
    public void ToValidation_OnConflict_ReturnsInvalid()
        => new Result<int>.Conflict(_errorA).ToValidation()
            .ShouldBeOfType<Validation<int>.Invalid>()
            .Errors.ShouldContain(_errorA);

    [Fact]
    public void ToValidation_OnForbidden_ReturnsInvalid()
        => new Result<int>.Forbidden(_errorA).ToValidation()
            .ShouldBeOfType<Validation<int>.Invalid>()
            .Errors.ShouldContain(_errorA);

    // ── Async variants ───────────────────────────────────────────────────────

    [Fact]
    public async Task TapAsync_OnSuccess_ExecutesAction()
    {
        var seen = -1;
        await Result<int>.Ok(3).TapAsync(x => { seen = x; return Task.CompletedTask; });
        seen.ShouldBe(3);
    }

    [Fact]
    public async Task MapAsync_OnSuccess_TransformsValue()
        => (await Result<int>.Ok(4).MapAsync(x => Task.FromResult(x + 1)))
            .ShouldSucceed().ShouldBe(5);

    [Fact]
    public async Task BindAsync_OnSuccess_AppliesBinder()
        => (await Result<int>.Ok(2).BindAsync(x => Task.FromResult(Result<int>.Ok(x * 3))))
            .ShouldSucceed().ShouldBe(6);

    [Fact]
    public async Task BindAsync_OnInvalid_PropagatesError()
        => (await Result<int>.Fail(_errorA).BindAsync(_ => Task.FromResult(Result<int>.Ok(99))))
            .ShouldBeOfType<Result<int>.Invalid>()
            .Error.ShouldBe(_errorA);

    [Fact]
    public async Task MatchAsync_OnSuccess_CallsOnSuccess()
        => (await Result<int>.Ok(5).MatchAsync(v => Task.FromResult(v * 2), _ => 0)).ShouldBe(10);

    [Fact]
    public async Task MatchAsync_OnInvalid_CallsOnFailure()
        => (await Result<int>.Fail(_errorB).MatchAsync(_ => Task.FromResult(0), _ => 99)).ShouldBe(99);
}
