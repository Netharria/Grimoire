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

    // ── Validation.Combine ───────────────────────────────────────────────────

    [Fact]
    public void Combine_AllValid_ReturnsSuccessWithAllValues()
    {
        var result = Validation.Combine(
            Validation<int>.Succeed(1),
            Validation<string>.Succeed("b"),
            Validation<bool>.Succeed(true),
            Validation<long>.Succeed(4L));
        var (i, s, b, l) = result.ShouldSucceed();
        i.ShouldBe(1);
        s.ShouldBe("b");
        b.ShouldBeTrue();
        l.ShouldBe(4L);
    }

    [Fact]
    public void Combine_OneInvalid_PropagatesItsErrors()
    {
        var result = Validation.Combine(
            Validation<int>.Succeed(1),
            Validation<string>.Succeed("b"),
            Validation<bool>.Fail(_errorC),
            Validation<long>.Succeed(4L));
        result.ShouldBeOfType<Validation<(int, string, bool, long)>.Invalid>()
            .Errors.ShouldHaveSingleItem().ShouldBe(_errorC);
    }

    [Fact]
    public void Combine_SeveralInvalid_AccumulatesAllErrors()
    {
        var result = Validation.Combine(
            Validation<int>.Fail(_errorA),
            Validation<string>.Fail(_errorB),
            Validation<bool>.Succeed(true),
            Validation<long>.Fail(_errorC));
        result.ShouldBeOfType<Validation<(int, string, bool, long)>.Invalid>()
            .Errors.ShouldBe([_errorA, _errorB, _errorC]);
    }

    // ── Result.WhenAll ───────────────────────────────────────────────────────

    private static Task<Result<int>> Ok(int value) => Task.FromResult(Result<int>.Ok(value));

    private static Task<Result<int>> Failed(Result<int> result) => Task.FromResult(result);

    [Fact]
    public async Task WhenAll_AllSucceed_ReturnsTuple()
        => (await Result.WhenAll(Ok(1), Ok(2), Ok(3), Ok(4), Ok(5), Ok(6)).ShouldSucceed())
            .ShouldBe((1, 2, 3, 4, 5, 6));

    [Fact]
    public async Task WhenAll_OneFails_ReturnsInvalidWithThatExactError()
        => (await Result.WhenAll(Ok(1), Ok(2), Failed(Result<int>.Fail(_errorA)), Ok(4), Ok(5), Ok(6)))
            .ShouldBeOfType<Result<(int, int, int, int, int, int)>.Invalid>()
            .Error.ShouldBe(_errorA);

    [Fact]
    public async Task WhenAll_NotFound_BecomesInvalid()
        => (await Result.WhenAll(Ok(1), Ok(2), Ok(3), Ok(4), Ok(5), Failed(new Result<int>.NotFound(_errorB))))
            .ShouldBeOfType<Result<(int, int, int, int, int, int)>.Invalid>()
            .Error.ShouldBe(_errorB);

    [Fact]
    public async Task WhenAll_SeveralFail_CombinesDistinctErrorMessages()
    {
        var result = await Result.WhenAll(
            Failed(Result<int>.Fail(_errorA)), Ok(2), Failed(Result<int>.Fail(_errorB)),
            Ok(4), Failed(Result<int>.Fail(_errorA)), Ok(6));
        var error = result.ShouldBeOfType<Result<(int, int, int, int, int, int)>.Invalid>().Error;
        error.Code.ShouldBe("validation.failed");
        error.Message.ShouldBe("Error A; Error B");
    }

    [Fact]
    public async Task WhenAll_SameErrorEverywhere_ReturnsItAsIs()
    {
        var failed = Failed(Result<int>.Fail(_errorA));
        (await Result.WhenAll(failed, failed, failed, failed, failed, failed))
            .ShouldBeOfType<Result<(int, int, int, int, int, int)>.Invalid>()
            .Error.ShouldBe(_errorA);
    }
}
