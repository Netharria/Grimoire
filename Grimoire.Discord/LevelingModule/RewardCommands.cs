// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Commands;
using Grimoire.Core.Features.Leveling.Queries;

namespace Grimoire.Discord.LevelingModule;

[SlashCommandGroup("Rewards", "Commands for updating and viewing the server rewards")]
[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Leveling)]
[SlashRequireUserGuildPermissions(Permissions.ManageGuild)]
internal sealed class RewardCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

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
            new AddReward.Command
            {
                RoleId = role.Id,
                GuildId = ctx.Guild.Id,
                RewardLevel = (int)level,
                Message = string.IsNullOrWhiteSpace(message) ? null : message
            });
        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, message: response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
    }

    [SlashCommand("Remove", "Removes a reward from the server.")]
    public async Task RemoveAsync(InteractionContext ctx,
        [Option("Role", "The role to be awarded")] DiscordRole role)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(
            new RemoveReward.Command
            {
                RoleId = role.Id
            });

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, message: response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
    }

    [SlashCommand("View", "Displays all rewards on this server.")]
    public async Task ViewAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new GetRewards.Query{ GuildId = ctx.Guild.Id});
        await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
            title: "Rewards",
            message: response.Message);
    }
}
