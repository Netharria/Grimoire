// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

public sealed class ValidationTests
{
    private static readonly Error _errorA = new("code.a", "Error A");
    private static readonly Error _errorB = new("code.b", "Error B");

    // ── Construction ─────────────────────────────────────────────────────────

    [Fact]
    public void Succeed_IsValid()
        => Validation<int>.Succeed(42).ShouldBeOfType<Validation<int>.Valid>().Value.ShouldBe(42);

    [Fact]
    public void Fail_SingleError_IsInvalidWithThatError()
    {
        var result = Validation<int>.Fail(_errorA);
        var invalid = result.ShouldBeOfType<Validation<int>.Invalid>();
        invalid.Errors.ShouldHaveSingleItem().ShouldBe(_errorA);
    }

    [Fact]
    public void Fail_MultipleErrors_IsInvalidWithAllErrors()
    {
        var result = Validation<int>.Fail([_errorA, _errorB]);
        var invalid = result.ShouldBeOfType<Validation<int>.Invalid>();
        invalid.Errors.Length.ShouldBe(2);
        invalid.Errors.ShouldContain(_errorA);
        invalid.Errors.ShouldContain(_errorB);
    }

    // ── Map ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_OnValid_TransformsValue()
        => Validation<int>.Succeed(3).Map(x => x * 2).ShouldSucceed().ShouldBe(6);

    [Fact]
    public void Map_OnInvalid_PropagatesErrorsWithoutCallingMapper()
    {
        var called = false;
        var result = Validation<int>.Fail(_errorA).Map(x =>
        {
            called = true;
            return x;
        });
        result.ShouldBeOfType<Validation<int>.Invalid>().Errors.ShouldContain(_errorA);
        called.ShouldBeFalse();
    }

    // ── Bind ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_OnValid_AppliesBinderSuccessfully()
        => Validation<int>.Succeed(5)
            .Bind(x => Validation<string>.Succeed($"val:{x}"))
            .ShouldSucceed()
            .ShouldBe("val:5");

    [Fact]
    public void Bind_OnValid_BinderReturnsInvalid_PropagatesError()
        => Validation<int>.Succeed(5)
            .Bind(_ => Validation<string>.Fail(_errorA))
            .ShouldBeOfType<Validation<string>.Invalid>()
            .Errors.ShouldContain(_errorA);

    [Fact]
    public void Bind_OnInvalid_DoesNotCallBinder()
    {
        var called = false;
        Validation<int>.Fail(_errorA).Bind(x =>
        {
            called = true;
            return Validation<string>.Succeed($"{x}");
        });
        called.ShouldBeFalse();
    }

    // ── Tap ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Tap_OnValid_ExecutesActionWithValue()
    {
        var seen = -1;
        Validation<int>.Succeed(7).Tap(x => seen = x);
        seen.ShouldBe(7);
    }

    [Fact]
    public void Tap_OnInvalid_DoesNotExecuteAction()
    {
        var called = false;
        Validation<int>.Fail(_errorA).Tap(_ => called = true);
        called.ShouldBeFalse();
    }

    [Fact]
    public void Tap_ReturnsSameValidation()
    {
        var validation = Validation<int>.Succeed(1);
        validation.Tap(_ => { }).ShouldBe(validation);
    }

    // ── Match ────────────────────────────────────────────────────────────────

    [Fact]
    public void Match_OnValid_CallsOnValid()
        => Validation<int>.Succeed(9).Match(v => v * 10, _ => -1).ShouldBe(90);

    [Fact]
    public void Match_OnInvalid_CallsOnInvalid()
        => Validation<int>.Fail(_errorA).Match(_ => 1, errors => errors.Length).ShouldBe(1);

    // ── OrElse / GetOrElse ───────────────────────────────────────────────────

    [Fact]
    public void OrElse_OnValid_ReturnsSelf()
    {
        var validation = Validation<int>.Succeed(1);
        var called = false;
        validation.OrElse(() =>
        {
            called = true;
            return Validation<int>.Succeed(99);
        }).ShouldBe(validation);
        called.ShouldBeFalse();
    }

    [Fact]
    public void OrElse_OnInvalid_ReturnsFallback()
        => Validation<int>.Fail(_errorA)
            .OrElse(() => Validation<int>.Succeed(99))
            .ShouldSucceed()
            .ShouldBe(99);

    [Fact]
    public void GetOrElse_OnValid_ReturnsValue()
        => Validation<int>.Succeed(42).GetOrElse(() => -1).ShouldBe(42);

    [Fact]
    public void GetOrElse_OnInvalid_ReturnsFallbackValue()
        => Validation<int>.Fail(_errorA).GetOrElse(() => -1).ShouldBe(-1);

    // ── ToResult ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToResult_OnValid_ReturnsSuccess()
        => Validation<int>.Succeed(5).ToResult().ShouldSucceed().ShouldBe(5);

    [Fact]
    public void ToResult_OnInvalid_SingleError_ReturnsThatExactError()
    {
        var result = Validation<int>.Fail(_errorA).ToResult();
        result.ShouldBeOfType<Result<int>.Invalid>().Error.ShouldBe(_errorA);
    }

    [Fact]
    public void ToResult_OnInvalid_MultipleDistinctErrors_CombinesMessages()
    {
        var result = Validation<int>.Fail([_errorA, _errorB]).ToResult();
        var invalid = result.ShouldBeOfType<Result<int>.Invalid>();
        invalid.Error.Code.ShouldBe("validation.failed");
        invalid.Error.Message.ShouldContain(_errorA.Message);
        invalid.Error.Message.ShouldContain(_errorB.Message);
    }

    [Fact]
    public void ToResult_OnInvalid_DuplicateErrors_DeduplicatesBeforeCombining()
    {
        var result = Validation<int>.Fail([_errorA, _errorA]).ToResult();
        // After dedup there is one error, so it should be returned directly (no "validation.failed" wrapper).
        var invalid = result.ShouldBeOfType<Result<int>.Invalid>();
        invalid.Error.ShouldBe(_errorA);
    }

    // ── Async variants ───────────────────────────────────────────────────────

    [Fact]
    public async Task TapAsync_OnValid_ExecutesAction()
    {
        var seen = -1;
        await Validation<int>.Succeed(3).TapAsync(x =>
        {
            seen = x;
            return Task.CompletedTask;
        });
        seen.ShouldBe(3);
    }

    [Fact]
    public async Task MapAsync_OnValid_TransformsValue()
        => (await Validation<int>.Succeed(4).MapAsync(x => Task.FromResult(x + 1)))
            .ShouldSucceed().ShouldBe(5);

    [Fact]
    public async Task BindAsync_OnValid_AppliesBinder()
        => (await Validation<int>.Succeed(2).BindAsync(x => Task.FromResult(Validation<int>.Succeed(x * 3))))
            .ShouldSucceed().ShouldBe(6);

    [Fact]
    public async Task BindAsync_OnInvalid_PropagatesError()
        => (await Validation<int>.Fail(_errorA).BindAsync(_ => Task.FromResult(Validation<int>.Succeed(99))))
            .ShouldBeOfType<Validation<int>.Invalid>()
            .Errors.ShouldContain(_errorA);

    [Fact]
    public async Task MatchAsync_OnValid_CallsOnValid()
        => (await Validation<int>.Succeed(5).MatchAsync(v => Task.FromResult(v * 2), _ => 0)).ShouldBe(10);
}
