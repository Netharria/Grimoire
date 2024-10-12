// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.CustomCommands.Commands;
public sealed class RemoveCustomCommand
{
    public sealed record Command : ICommand<BaseResponse>
    {
        public required string CommandName { get; init; }
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.CustomCommands
                .Include(x => x.CustomCommandRoles)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Name == command.CommandName && x.GuildId == command.GuildId, cancellationToken);
            if (result is null)
                throw new AnticipatedException($"Did not find a saved command with name {command.CommandName}");

            this._grimoireDbContext.CustomCommands.Remove(result);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            var modChannelLog = await this._grimoireDbContext.Guilds
                    .AsNoTracking()
                    .WhereIdIs(command.GuildId)
                    .Select(x => x.ModChannelLog)
                    .FirstOrDefaultAsync(cancellationToken);
            return new BaseResponse
            {
                Message = $"Removed command {command.CommandName}",
                LogChannelId = modChannelLog
            };
        }
    }
}
