// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.Common;
using Microsoft.Extensions.Logging;

namespace Grimoire.Core.Features.Shared.PipelineBehaviors;

public sealed partial class IgnoreDuplicateKeyError<TMessage, TResponse>(ILogger<IgnoreDuplicateKeyError<TMessage, TResponse>> logger) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<IgnoreDuplicateKeyError<TMessage, TResponse>> _logger = logger;

    public async ValueTask<TResponse> Handle(TMessage message, CancellationToken cancellationToken, MessageHandlerDelegate<TMessage, TResponse> next)
    {
        try
        {
            return await next(message, cancellationToken);
        }
        catch (UniqueConstraintException e)
        {
            LogFirstAttemptWarning(_logger, e.ConstraintName);
        }
        try
        {
            return await next(message, cancellationToken);
        }
        catch (UniqueConstraintException e)
        {
            LogSecondAttemptError(_logger, e,  e.ConstraintName);
            throw;
        }
    }

    [LoggerMessage(LogLevel.Warning, "Unique Constraint violated on {constraintName}. Trying again in case of race condition.")]
    public static partial void LogFirstAttemptWarning(ILogger logger, string constraintName);

    [LoggerMessage(LogLevel.Error, "Unique Constraint violated second time on {constraintName}.")]
    public static partial void LogSecondAttemptError(ILogger logger, Exception e, string constraintName);
}
