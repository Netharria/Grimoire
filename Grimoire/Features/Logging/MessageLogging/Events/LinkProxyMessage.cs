// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Logging.MessageLogging.Events;

public partial class LinkProxyMessage
{
    public record Command : IRequest
    {
        public required ulong ProxyMessageId { get; init; }
        public required ulong OriginalMessageId { get; init; }
        public required ulong GuildId { get; init; }
        public string? SystemId { get; init; }
        public string? MemberId { get; init; }
    }

    public partial class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory, ILogger<Handler> logger)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
        private readonly ILogger<Handler> _logger = logger;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var moduleEnabled = await dbContext.GuildMessageLogSettings
                .Where(x => x.GuildId == command.GuildId)
                .Select(x => x.ModuleEnabled)
                .FirstOrDefaultAsync(cancellationToken);

            if (!moduleEnabled) return;

            try
            {
                await dbContext.AddAsync(
                    new ProxiedMessageLink
                    {
                        ProxyMessageId = command.ProxyMessageId,
                        OriginalMessageId = command.OriginalMessageId,
                        SystemId = command.SystemId,
                        MemberId = command.MemberId
                    }, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                LogProxiedMessageFailure(this._logger, ex.Message, ex);
            }
        }

        [LoggerMessage(LogLevel.Error, "Was not able to save Proxied Message for the following reason. {message}")]
        static partial void LogProxiedMessageFailure(ILogger<Handler> logger, string message, Exception ex);
    }
}
