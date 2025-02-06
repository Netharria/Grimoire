// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Shared.Commands;

public sealed class SetUserCommandChannel
{
    public sealed record Command : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public ulong? ChannelId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Command, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var guild = await dbContext.Guilds
                .WhereIdIs(command.GuildId)
                .FirstOrDefaultAsync(cancellationToken);
            if (guild is null)
                throw new AnticipatedException("Could not find the settings for this server.");
            guild.UserCommandChannelId = command.ChannelId;

            await dbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { LogChannelId = guild.ModChannelLog };
        }
    }
}
