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
        Result<int>.Fail(_errorA).Map(x =>
        {
            called = true;
            return x;
        });
        called.ShouldBeFalse();
    }

    [Fact]
    public void Map_OnNotFound_PropagatesAsNotFound()
    {
        var result = new Result<int>.NotFound(_errorA).Map(x => x.ToString());
        result.ShouldBeOfType<Result<string>.NotFound>().Error.ShouldBe(_errorA);
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
        Result<int>.Fail(_errorA).Bind(x =>
        {
            called = true;
            return Result<string>.Ok($"{x}");
        });
        called.ShouldBeFalse();
    }

    [Fact]
    public void Bind_OnNotFound_PropagatesAsNotFound()
    {
        var result = new Result<int>.NotFound(_errorA).Bind(x => Result<string>.Ok($"{x}"));
        result.ShouldBeOfType<Result<string>.NotFound>().Error.ShouldBe(_errorA);
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
            .Match(_ => 0, _ => 1, _ => 2)
            .ShouldBe(2);

    [Fact]
    public void Match_OnNotFound_WithoutSpecificHandler_FallsBackToOnFailure()
        => new Result<int>.NotFound(_errorA)
            .Match(_ => 0, _ => 1)
            .ShouldBe(1);

    [Fact]
    public void Match_OnNotModified_WithSpecificHandler_CallsHandler()
        => new Result<int>.NotModified(_errorA)
            .Match(_ => 0, _ => 1, onNotModified: _ => 5)
            .ShouldBe(5);

    [Fact]
    public void Match_OnNotModified_WithoutSpecificHandler_FallsBackToOnFailure()
        => new Result<int>.NotModified(_errorA)
            .Match(_ => 0, _ => 1, onNotFound: _ => 2)
            .ShouldBe(1);

    [Fact]
    public void Match_OnFailure_PassesTheError()
        => new Result<int>.NotFound(_errorB)
            .Match(_ => null!, error => error)
            .ShouldBe(_errorB);

    // ── GetOrElse ────────────────────────────────────────────────────────────

    [Fact]
    public void GetOrElse_OnSuccess_ReturnsValue()
        => Result<int>.Ok(42).GetOrElse(() => -1).ShouldBe(42);

    [Fact]
    public void GetOrElse_OnInvalid_ReturnsFallbackValue()
        => Result<int>.Fail(_errorA).GetOrElse(() => -1).ShouldBe(-1);

    // ── Async variants ───────────────────────────────────────────────────────

    [Fact]
    public async Task TapAsync_OnSuccess_ExecutesAction()
    {
        var seen = -1;
        await Result<int>.Ok(3).TapAsync(x =>
        {
            seen = x;
            return Task.CompletedTask;
        });
        seen.ShouldBe(3);
    }

    [Fact]
    public async Task BindAsync_OnSuccess_AppliesBinder()
        => (await Result<int>.Ok(2).BindAsync(x => Task.FromResult(Result<int>.Ok(x * 3))))
            .ShouldSucceed().ShouldBe(6);

    [Fact]
    public async Task BindAsync_OnNotModified_PropagatesAsNotModified()
        => (await new Result<int>.NotModified(_errorA).BindAsync(_ => Task.FromResult(Result<string>.Ok("x"))))
            .ShouldBeOfType<Result<string>.NotModified>()
            .Error.ShouldBe(_errorA);

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

    // ── MatchAsync (void Task, side-effecting) ──────────────────────────────

    [Fact]
    public async Task MatchAsync_VoidTask_OnSuccess_InvokesOnSuccessOnly()
    {
        var successCalled = false;
        var failureCalled = false;

        await Result<int>.Ok(1).MatchAsync(
            _ =>
            {
                successCalled = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                failureCalled = true;
                return Task.CompletedTask;
            });

        successCalled.ShouldBeTrue();
        failureCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task MatchAsync_VoidTask_OnInvalid_InvokesOnFailureOnly()
    {
        var successCalled = false;
        Error? seen = null;

        await Result<int>.Fail(_errorA).MatchAsync(
            _ =>
            {
                successCalled = true;
                return Task.CompletedTask;
            },
            e =>
            {
                seen = e;
                return Task.CompletedTask;
            });

        successCalled.ShouldBeFalse();
        seen.ShouldBe(_errorA);
    }

    [Fact]
    public Task MatchAsync_VoidTask_OnNotFound_RoutesToOnFailure()
        => AssertVoidMatchRoutesToFailure(new Result<int>.NotFound(_errorA));

    [Fact]
    public Task MatchAsync_VoidTask_OnNotModified_RoutesToOnFailure()
        => AssertVoidMatchRoutesToFailure(new Result<int>.NotModified(_errorA));

    private static async Task AssertVoidMatchRoutesToFailure(Result<int> result)
    {
        var failureCalled = false;
        await result.MatchAsync(_ => Task.CompletedTask, _ =>
        {
            failureCalled = true;
            return Task.CompletedTask;
        });
        failureCalled.ShouldBeTrue();
    }

    // ── Task<Result<T>>.MatchAsync (ResultTaskExtensions, void Task) ────────

    [Fact]
    public async Task TaskMatchAsync_VoidTask_OnSuccess_InvokesOnSuccessOnly()
    {
        var successCalled = false;
        var failureCalled = false;

        await Task.FromResult(Result<int>.Ok(1)).MatchAsync(
            _ =>
            {
                successCalled = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                failureCalled = true;
                return Task.CompletedTask;
            });

        successCalled.ShouldBeTrue();
        failureCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task TaskMatchAsync_VoidTask_OnInvalid_InvokesOnFailureOnly()
    {
        var successCalled = false;
        Error? seen = null;

        await Task.FromResult(Result<int>.Fail(_errorB)).MatchAsync(
            _ =>
            {
                successCalled = true;
                return Task.CompletedTask;
            },
            e =>
            {
                seen = e;
                return Task.CompletedTask;
            });

        successCalled.ShouldBeFalse();
        seen.ShouldBe(_errorB);
    }

    // ── Task<Result<T>>.Match with handlers that return a task ──────────────

    [Fact]
    public async Task TaskMatch_TaskHandler_AwaitsTheHandler()
    {
        var finished = false;

        await Task.FromResult(Result<int>.Ok(1)).Match(
            async _ =>
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
                finished = true;
            },
            _ => Task.CompletedTask);

        finished.ShouldBeTrue();
    }

    [Fact]
    public async Task TaskMatch_ValueTaskHandler_AwaitsTheHandler()
    {
        var finished = false;

        await Task.FromResult<Result<int>>(new Result<int>.NotFound(_errorA)).Match(
            _ => ValueTask.CompletedTask,
            _ => ValueTask.CompletedTask,
            onNotFound: async ValueTask (_) =>
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
                finished = true;
            });

        finished.ShouldBeTrue();
    }

    [Fact]
    public async Task TaskMatch_TaskHandler_PropagatesTheHandlersException()
        => await Should.ThrowAsync<InvalidOperationException>(() => Task.FromResult(Result<int>.Fail(_errorA)).Match(
            _ => Task.CompletedTask,
            async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException();
            }));
}
