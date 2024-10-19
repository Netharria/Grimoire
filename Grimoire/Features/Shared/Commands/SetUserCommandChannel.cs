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
    public sealed record Command : ICommand<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public ulong? ChannelId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var guild = await this._grimoireDbContext.Guilds
            .WhereIdIs(command.GuildId)
            .FirstOrDefaultAsync(cancellationToken);
            if (guild is null)
                throw new AnticipatedException("Could not find the settings for this server.");
            guild.UserCommandChannelId = command.ChannelId;

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse
            {
                LogChannelId = guild.ModChannelLog,
            };
        }
    }
}

