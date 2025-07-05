// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.PipelineBehaviors;

public sealed partial class ErrorLoggingBehavior<TRequest, TException>(
    ILogger<ErrorLoggingBehavior<TRequest, TException>> logger) : IRequestExceptionAction<TRequest, TException>
    where TRequest : IRequest
    where TException : Exception
{
    private readonly ILogger<ErrorLoggingBehavior<TRequest, TException>> _logger = logger;

    public Task Execute(TRequest request, TException exception, CancellationToken cancellationToken)
    {
        LogHandlerError(this._logger, exception, typeof(TRequest).Name);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Error, "Exception Thrown on {RequestType}")]
    static partial void LogHandlerError(ILogger logger, Exception ex, string requestType);
}
