// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Commands;

public sealed class AddReward
{
    public sealed record Command : ICommand<BaseResponse>
    {
        public required ulong RoleId { get; init; }
        public required ulong GuildId { get; init; }
        public required int RewardLevel { get; init; }
        public string? Message { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
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
                    .AsNoTracking()
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

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse
            {
                Message = $"Updated {reward.Mention()} reward to level {command.RewardLevel}",
                LogChannelId = reward.Guild.ModChannelLog
            };
        }
    }
}
