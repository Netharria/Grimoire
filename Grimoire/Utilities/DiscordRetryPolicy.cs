// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Polly;
using Polly.Retry;

namespace Grimoire.Utilities;

/// <summary>
///     The retry for unported code's Discord calls that read or are idempotent: fetches, edits, deletes, overwrites
///     (SH-15). A call that creates something (a new message, a follow-up, a DM, an initial interaction response) is
///     never retried, because a 502 may come after Discord already did it (Q25). Ported code uses
///     <c>Discord&lt;RT&gt;</c>, whose retry is shorter (spec 1.4, section 3.6).
/// </summary>
public static class DiscordRetryPolicy
{
    private static readonly RetryStrategyOptions _retryStrategyOptions
        = new()
        {
            ShouldHandle = new PredicateBuilder().Handle<ServerErrorException>(),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 4,
            Delay = TimeSpan.FromSeconds(3)
        };

    private static readonly ResiliencePipeline _resiliencePipeline =
        new ResiliencePipelineBuilder().AddRetry(_retryStrategyOptions).Build();

    public static ValueTask RetryDiscordRead(Func<CancellationToken, ValueTask> function,
        CancellationToken cancellationToken = default)
        => _resiliencePipeline.ExecuteAsync(function, cancellationToken);

    public static ValueTask<T> RetryDiscordRead<T>(Func<CancellationToken, ValueTask<T>> function,
        CancellationToken cancellationToken = default)
        => _resiliencePipeline.ExecuteAsync(function, cancellationToken);

    /// <returns>The call's result, or <paramref name="defaultValue" /> if Discord refuses it or retries run out.</returns>
    public static async ValueTask<T> RetryDiscordRead<T>(Func<CancellationToken, ValueTask<T>> function, T defaultValue,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _resiliencePipeline.ExecuteAsync(function, cancellationToken);
        }
        catch (DiscordException)
        {
            return defaultValue;
        }
    }
}
