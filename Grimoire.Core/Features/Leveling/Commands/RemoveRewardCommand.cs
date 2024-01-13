// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Commands;

public sealed class RemoveReward
{
    public sealed record Command : ICommand<BaseResponse>
    {
        public ulong RoleId { get; init; }
    }

    public sealed class Handler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.Rewards
            .Include(x => x.Guild)
            .FirstOrDefaultAsync(x => x.RoleId == command.RoleId, cancellationToken);
            if (result is not Reward reward)
                throw new AnticipatedException($"Did not find a saved reward for role <@&{command.RoleId}>");
            this._grimoireDbContext.Rewards.Remove(reward);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse
            {
                Message = $"Removed {reward.Mention()} reward",
                LogChannelId = result.Guild.ModChannelLog
            };
        }
    }

}
