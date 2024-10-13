// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


namespace Grimoire.Features.Leveling.Rewards;

public sealed class RemoveReward
{
    [SlashCommandGroup("Rewards", "Commands for updating and viewing the server rewards")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Leveling)]
    [SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Remove", "Removes a reward from the server.")]
        public async Task RemoveAsync(InteractionContext ctx,
            [Option("Role", "The role to be awarded")] DiscordRole role)
        {
            await ctx.DeferAsync();
            var response = await this._mediator.Send(
            new Request
            {
                RoleId = role.Id
            });

            await ctx.EditReplyAsync(GrimoireColor.DarkPurple, message: response.Message);
            await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
        }
    }
    public sealed record Request : ICommand<BaseResponse>
    {
        public required ulong RoleId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Request, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
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
