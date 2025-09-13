// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [Command("View")]
    [Description("Displays all rewards on this server.")]
    public async Task ViewAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();
        if(ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");
        var response = await this._mediator.CreateStream(new GetRewards.Request { GuildId = ctx.Guild.Id })
            .SelectAwait(async x=>
                {
                    var role = await ctx.Guild.GetRoleOrDefaultAsync(x.RoleId);
                    return $"Level:{x.RewardLevel} Role:{role?.Mention} {(x.RewardMessage == null ? "" : $"Reward Message: {x.RewardMessage}")}";
                })
            .ToArrayAsync();
        await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
            title: "Rewards",
            message: string.Join('\n', response));
    }
}

public sealed class GetRewards
{
    public sealed record Request : IStreamRequest<Response>
    {
        public required ulong GuildId { get; init; }
    }


    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IStreamRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async IAsyncEnumerable<Response> Handle(Request request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rewards = dbContext.Rewards
                .AsNoTracking()
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new Response
                {
                    RoleId = x.RoleId,
                    RewardLevel = x.RewardLevel,
                    RewardMessage = x.RewardMessage
                })
                .AsAsyncEnumerable();
            await foreach (var reward in rewards.WithCancellation(cancellationToken))
            {
                yield return reward;
            }
        }
    }

    public sealed record Response
    {
        public required ulong RoleId { get; init; }
        public required int RewardLevel { get; init; }
        public string? RewardMessage { get; init; }
    }
}
