// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Grimoire.Core.Features.Shared.PipelineBehaviors;

public partial class RequestTimingBehavior<TMessage, TResponse>(ILogger<RequestTimingBehavior<TMessage, TResponse>> logger) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<RequestTimingBehavior<TMessage, TResponse>> _logger = logger;

    public async ValueTask<TResponse> Handle(TMessage message, CancellationToken cancellationToken, MessageHandlerDelegate<TMessage, TResponse> next)
    {
        var stopwatch = Stopwatch.GetTimestamp();

        try
        {
            return await next(message, cancellationToken);
        }
        finally
        {
            var delta = Stopwatch.GetElapsedTime(stopwatch);
            if (delta.TotalMilliseconds > 150)
                LogHandlerDurationWarning(_logger, message.GetType(), delta.TotalMilliseconds);
        }
    }

    [LoggerMessage(LogLevel.Warning, "{RequestType}; Execution time={ElapsedTime}ms")]
    public static partial void LogHandlerDurationWarning(ILogger logger, Type requestType, double elapsedTime);
}
