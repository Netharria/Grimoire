// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cybermancy.Core.Features.Shared.PipelineBehaviors
{
    public class RequestTimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<RequestTimingBehavior<TRequest, TResponse>> _logger;

        public RequestTimingBehavior(ILogger<RequestTimingBehavior<TRequest, TResponse>> logger)
        {
            this._logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            TResponse response;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                response = await next();
            }
            finally
            {
                stopwatch.Stop();
                if(stopwatch.ElapsedMilliseconds > 100)
                    this._logger.LogWarning(
                    "{ReqestType}; Execution time={ElapsedTime}ms", request.GetType(), stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
    }
}
