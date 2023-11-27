// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Commands;

public sealed record AddRewardCommand : ICommand<BaseResponse>
{
    public ulong RoleId { get; init; }
    public ulong GuildId { get; init; }
    public int RewardLevel { get; init; }
    public string? Message { get; init; }
}

public class AddRewardCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<AddRewardCommand, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<BaseResponse> Handle(AddRewardCommand command, CancellationToken cancellationToken)
    {
        var reward = await this._grimoireDbContext.Rewards
            .Include(x => x.Guild)
            .FirstOrDefaultAsync(x => x.RoleId == command.RoleId, cancellationToken: cancellationToken);
        if (reward is null)
        {
            reward = new Reward
            {
                GuildId = command.GuildId,
                RoleId = command.RoleId,
                RewardLevel = command.RewardLevel,
                RewardMessage = command.Message
            };
            await this._grimoireDbContext.Rewards.AddAsync(reward, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            var modChannelLog = await this._grimoireDbContext.Guilds
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog)
                .FirstOrDefaultAsync(cancellationToken);
            return new BaseResponse
            {
                Message = $"Added {reward.Mention()} reward at level {command.RewardLevel}",
                LogChannelId = modChannelLog
            };
        }

        reward.RewardLevel = command.RewardLevel;
        reward.RewardMessage = command.Message;
        this._grimoireDbContext.Rewards.Update(reward);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new BaseResponse
        {
            Message = $"Updated {reward.Mention()} reward to level {command.RewardLevel}",
            LogChannelId = reward.Guild.ModChannelLog
        };
    }
}
