// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [SlashCommand("View", "Displays all rewards on this server.")]
    public async Task ViewAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new GetRewards.Request { GuildId = ctx.Guild.Id });
        await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
            title: "Rewards",
            message: response.Message);
    }
}

public sealed class GetRewards
{
    public sealed record Request : IRequest<BaseResponse>
    {
        public required ulong GuildId { get; init; }
    }


    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rewards = await dbContext.Rewards
                .AsNoTracking()
                .Where(x => x.GuildId == request.GuildId)
                .Select(x =>
                    $"Level:{x.RewardLevel} Role:{x.Mention()} {(x.RewardMessage == null ? "" : $"Reward Message: {x.RewardMessage}")}")
                .ToListAsync(cancellationToken);
            if (rewards.Count == 0)
                throw new AnticipatedException("This guild does not have any rewards.");
            return new BaseResponse { Message = string.Join('\n', rewards) };
        }
    }
}
