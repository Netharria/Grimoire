// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using DSharpPlus.Exceptions;
using Grimoire.Utilities;

namespace Grimoire.Tests.Utilities;

/// <summary>
///     Which Discord failures are retried (backlog A-53). The policy waits about 3 s before its first retry and its
///     clock can't be replaced, so a retry is shown by cancelling during that wait instead of sitting through it.
/// </summary>
public sealed class DiscordRetryPolicyTests
{
    /// <summary>DSharpPlus builds these from a REST response; the policy only looks at the type.</summary>
    private static T Discord<T>() where T : DiscordException
        => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    [Fact]
    public async Task A_call_that_succeeds_runs_once_and_returns_its_result()
    {
        var calls = 0;

        var result = await DiscordRetryPolicy.RetryDiscordCall(_ =>
        {
            calls++;
            return ValueTask.FromResult(7);
        }, TestContext.Current.CancellationToken);

        result.ShouldBe(7);
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task A_client_error_is_not_retried()
    {
        var calls = 0;

        await Should.ThrowAsync<NotFoundException>(async () => await DiscordRetryPolicy.RetryDiscordCall(_ =>
        {
            calls++;
            throw Discord<NotFoundException>();
        }, TestContext.Current.CancellationToken));

        calls.ShouldBe(1);
    }

    /// <summary>
    ///     A server error is waited on and retried: cancelling during the wait ends the call with a cancellation, not the
    ///     server error.
    /// </summary>
    [Fact]
    public async Task A_server_error_is_retried_after_a_wait()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var calls = 0;

        await Should.ThrowAsync<OperationCanceledException>(async () => await DiscordRetryPolicy.RetryDiscordCall(_ =>
        {
            calls++;
            cancellation.Cancel();
            throw Discord<ServerErrorException>();
        }, cancellation.Token));

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task With_a_default_a_refused_call_gives_the_default()
        => (await DiscordRetryPolicy.RetryDiscordCall<int>(_ => throw Discord<UnauthorizedException>(), -1,
            TestContext.Current.CancellationToken)).ShouldBe(-1);

    [Fact]
    public async Task With_a_default_a_successful_call_gives_its_result()
        => (await DiscordRetryPolicy.RetryDiscordCall(_ => ValueTask.FromResult(3), -1,
            TestContext.Current.CancellationToken)).ShouldBe(3);

    [Fact]
    public async Task With_a_default_other_exceptions_still_escape()
        => await Should.ThrowAsync<InvalidOperationException>(async () =>
            await DiscordRetryPolicy.RetryDiscordCall<int>(_ => throw new InvalidOperationException(), -1,
                TestContext.Current.CancellationToken));

    [Fact]
    public async Task The_void_overload_runs_the_call()
    {
        var calls = 0;

        await DiscordRetryPolicy.RetryDiscordCall(_ =>
        {
            calls++;
            return ValueTask.CompletedTask;
        }, TestContext.Current.CancellationToken);

        calls.ShouldBe(1);
    }
}
