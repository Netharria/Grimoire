// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Grimoire.Core.Features.Shared.PipelineBehaviors;

public sealed partial class ErrorLoggingBehavior<TMessage, TResponse>(ILogger<ErrorLoggingBehavior<TMessage, TResponse>> logger) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<ErrorLoggingBehavior<TMessage, TResponse>> _logger = logger;

    public async ValueTask<TResponse> Handle(TMessage message, CancellationToken cancellationToken, MessageHandlerDelegate<TMessage, TResponse> next)
    {
        try
        {
            return await next(message, cancellationToken);
        }
        catch (Exception e) when (e is not AnticipatedException)
        {
            LogHandlerError(_logger, e, typeof(TMessage).Name);
            throw;
        }
    }

    [LoggerMessage(LogLevel.Error, "Exception Thrown on {RequestType}")]
    public static partial void LogHandlerError(ILogger logger, Exception ex, string requestType);
}
