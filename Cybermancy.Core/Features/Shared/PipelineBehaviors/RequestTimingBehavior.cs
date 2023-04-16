// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cybermancy.Core.Features.Shared.PipelineBehaviors
{
    public class RequestTimingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
        where TMessage : IMessage
    {
        private readonly ILogger<RequestTimingBehavior<TMessage, TResponse>> _logger;

        public RequestTimingBehavior(ILogger<RequestTimingBehavior<TMessage, TResponse>> logger)
        {
            this._logger = logger;
        }

        public async ValueTask<TResponse> Handle(TMessage message, CancellationToken cancellationToken, MessageHandlerDelegate<TMessage, TResponse> next)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                return await next(message, cancellationToken);
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 100)
                    this._logger.LogWarning(
                    "{ReqestType}; Execution time={ElapsedTime}ms", message.GetType(), stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
