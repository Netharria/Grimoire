// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Channels;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [Command("Add")]
    [Description("Adds or updates rewards for the server.")]
    public async Task AddAsync(CommandContext ctx,
        [Parameter("Role")]
        [Description("The role to be added as a reward.")]
        DiscordRole role,
        [Parameter("Level")]
        [Description("The level the reward is awarded at.")]
        int level,
        [MinMaxLength(maxLength:4096)]
        [Parameter("Message")]
        [Description("The message to send to users when they earn a reward. Discord Markdown applies.")]
        string message = "")
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        if (ctx.Guild.CurrentMember.Hierarchy < role.Position)
            throw new AnticipatedException($"{ctx.Guild.CurrentMember.DisplayName} will not be able to apply this " +
                                           $"reward role because the role has a higher rank than it does.");

        var response = await this._mediator.Send(
            new AddReward.Request
            {
                RoleId = role.Id,
                GuildId = ctx.Guild.Id,
                RewardLevel = level,
                Message = string.IsNullOrWhiteSpace(message) ? null : message
            });
        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, response.Message);
        await this._channel.Writer.WriteAsync(new PublishToGuildLog
        {
            LogChannelId = response.LogChannelId,
            Color = GrimoireColor.DarkPurple,
            Description = response.Message
        });
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

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var reward = await dbContext.Rewards
                .Include(x => x.Guild)
                .FirstOrDefaultAsync(x => x.RoleId == command.RoleId, cancellationToken);
            if (reward is null)
            {
                reward = new Reward
                {
                    GuildId = command.GuildId,
                    RoleId = command.RoleId,
                    RewardLevel = command.RewardLevel,
                    RewardMessage = command.Message
                };
                await dbContext.Rewards.AddAsync(reward, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                var modChannelLog = await dbContext.Guilds
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

            await dbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse
            {
                Message = $"Updated {reward.Mention()} reward to level {command.RewardLevel}",
                LogChannelId = reward.Guild.ModChannelLog
            };
        }
    }
}
