// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.PipelineBehaviors;

public sealed partial class RequestTimingBehavior<TMessage, TResponse>(
    ILogger<RequestTimingBehavior<TMessage, TResponse>> logger) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IRequest
{
    private readonly ILogger<RequestTimingBehavior<TMessage, TResponse>> _logger = logger;

    public async Task<TResponse> Handle(TMessage message, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.GetTimestamp();

        try
        {
            return await next();
        }
        finally
        {
            var delta = Stopwatch.GetElapsedTime(stopwatch);
            if (delta.TotalMilliseconds > 1000)
                LogHandlerDurationWarning(this._logger, message.GetType(), delta.TotalMilliseconds);
        }
    }

    [LoggerMessage(LogLevel.Warning, "{RequestType}; Execution time={ElapsedTime}ms")]
    static partial void LogHandlerDurationWarning(ILogger logger, Type requestType, double elapsedTime);
}
