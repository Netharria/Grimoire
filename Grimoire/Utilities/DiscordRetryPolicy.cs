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
public static class DiscordRetryPolicy
{
    private static readonly RetryStrategyOptions _retryStrategyOptions
        = new ()
        {
            ShouldHandle = new PredicateBuilder().Handle<ServerErrorException>(),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 4,
            Delay = TimeSpan.FromSeconds(3)
        };
    private static readonly ResiliencePipeline _resiliencePipeline = new ResiliencePipelineBuilder().AddRetry(_retryStrategyOptions).Build();

    public static ValueTask<T> RetryDiscordCall<T>(Func<CancellationToken, ValueTask<T>> function, CancellationToken cancellationToken = default)
        => _resiliencePipeline.ExecuteAsync(function, cancellationToken);

    public static ValueTask<T> RetryDiscordCall<T>(Func<CancellationToken, ValueTask<T>> function, T defaultValue, CancellationToken cancellationToken = default)
    {
        try
        {
            return _resiliencePipeline.ExecuteAsync(function, cancellationToken);
        }
        catch (ServerErrorException)
        {
            return ValueTask.FromResult(defaultValue);
        }
    }
}
