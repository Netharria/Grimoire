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
        public required ulong RoleId { get; init; }
    }

    public sealed class Handler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.Rewards
            .Include(x => x.Guild)
            .Where(x => x.RoleId == command.RoleId)
            .Select(x => new
            {
                Reward = x,
                x.Guild.ModChannelLog
            })
            .FirstOrDefaultAsync(cancellationToken);
            if (result is null || result.Reward is null)
                throw new AnticipatedException($"Did not find a saved reward for role {RoleExtensions.Mention(command.RoleId)}");
            this._grimoireDbContext.Rewards.Remove(result.Reward);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse
            {
                Message = $"Removed {result.Reward.Mention()} reward",
                LogChannelId = result.ModChannelLog
            };
        }
    }

}
