// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Rewards;



public sealed partial class RewardCommandGroup
{

    [SlashCommand("Add", "Adds or updates rewards for the server.")]
    public async Task AddAsync(InteractionContext ctx,
        [Option("Role", "The role to be added as a reward")] DiscordRole role,
        [Minimum(1)]
        [Maximum(int.MaxValue)]
        [Option("Level", "The level the reward is awarded at.")] long level,
        [MaximumLength(4096)]
        [Option("Message", "The message to send to users when they earn a reward. Discord Markdown applies.")] string message = "")
    {
        await ctx.DeferAsync();
        if (ctx.Guild.CurrentMember.Hierarchy < role.Position)
            throw new AnticipatedException($"{ctx.Guild.CurrentMember.DisplayName} will not be able to apply this " +
                $"reward role because the role has a higher rank than it does.");

        var response = await this._mediator.Send(
            new AddReward.Request
            {
                RoleId = role.Id,
                GuildId = ctx.Guild.Id,
                RewardLevel = (int)level,
                Message = string.IsNullOrWhiteSpace(message) ? null : message
            });
        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, message: response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
    }
}


public sealed class AddReward
{

    public sealed record Request : IRequest<BaseResponse>
    {
        public required ulong RoleId { get; init; }
        public required ulong GuildId { get; init; }
        public required int RewardLevel { get; init; }
        public string? Message { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Request, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
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
