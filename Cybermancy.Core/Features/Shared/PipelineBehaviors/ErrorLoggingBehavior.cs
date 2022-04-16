// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR;
using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace Cybermancy.Core.Features.Shared.PipelineBehaviors
{
    public class ErrorLoggingBehavior<TRequest, TResponse, TException> : IRequestExceptionHandler<TRequest, TResponse, TException>
        where TRequest : IRequest<TResponse>
        where TException : Exception
    {
        private readonly ILogger<ErrorLoggingBehavior<TRequest, TResponse, TException>> _logger;

        public ErrorLoggingBehavior(ILogger<ErrorLoggingBehavior<TRequest, TResponse, TException>> logger)
        {
            this._logger = logger;
        }

        public Task Handle(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken)
        {
            this._logger.LogError("Exception Thrown on {RequestType} - {ErrorMessage} - {ErrorStackTrace}",
                typeof(TRequest).Name, exception.Message, exception.StackTrace);
            return Task.CompletedTask;
        }
    }
}
