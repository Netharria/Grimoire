// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [SlashCommand("Remove", "Removes a reward from the server.")]
    public async Task RemoveAsync(InteractionContext ctx,
        [Option("Role", "The role to be awarded")]
        DiscordRole role)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(
            new RemoveReward.Request { RoleId = role.Id });

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
    }
}

public sealed class RemoveReward
{
    public sealed record Request : IRequest<BaseResponse>
    {
        public required ulong RoleId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Rewards
                .Include(x => x.Guild)
                .Where(x => x.RoleId == command.RoleId)
                .Select(x => new { Reward = x, x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);
            if (result is null || result.Reward is null)
                throw new AnticipatedException(
                    $"Did not find a saved reward for role {RoleExtensions.Mention(command.RoleId)}");
            dbContext.Rewards.Remove(result.Reward);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse
            {
                Message = $"Removed {result.Reward.Mention()} reward", LogChannelId = result.ModChannelLog
            };
        }
    }
}
