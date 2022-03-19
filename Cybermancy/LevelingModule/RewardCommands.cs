// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Leveling.Commands.AddReward;
using Cybermancy.Core.Features.Leveling.Commands.MangeRewardsCommands.RemoveReward;
using Cybermancy.Core.Features.Leveling.Queries.GetRewards;
using Cybermancy.Enums;
using Cybermancy.Extensions;
using Cybermancy.SlashCommandAttributes;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using MediatR;

namespace Cybermancy.LevelingModule
{
    [SlashCommandGroup("Rewards", "Commands for updating and viewing the server rewards")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Leveling)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class RewardCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public RewardCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Add", "Adds or updates rewards for the server.")]
        public async Task AddAsync(InteractionContext ctx,
            [Option("Role", "The role to be added as a reward")] DiscordRole role,
            [Option("Level", "The level the reward is awarded at.")] long level)
        {
            var response = await this._mediator.Send(
                new AddRewardCommand
                {
                    RoleId = role.Id,
                    GuildId = ctx.Guild.Id,
                    RewardLevel = (uint)level,
                });

            if(!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }
            await ctx.ReplyAsync(CybermancyColor.Gold, message: response.Message, ephemeral: false);
        }

        [SlashCommand("Remove", "Removes a reward from the server.")]
        public async Task RemoveAsync(InteractionContext ctx,
            [Option("Role", "The role to be awarded")] DiscordRole role)
        {
            var response = await this._mediator.Send(
                new RemoveRewardCommand
                {
                    RoleId = role.Id
                });

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }
            await ctx.ReplyAsync(CybermancyColor.Gold, message: response.Message, ephemeral: false);
        }

        [SlashCommand("View", "Displays all rewards on this server.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            var response = await this._mediator.Send(new GetRewardsQuery{ GuildId = ctx.Guild.Id});
            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }
            await ctx.ReplyAsync(CybermancyColor.Gold,
                title: "Rewards",
                message: response.Message,
                ephemeral: false);
        }
    }
}
