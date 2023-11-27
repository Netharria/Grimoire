// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Commands;

namespace Grimoire.Discord.LevelingModule;

[SlashRequireGuild]
[SlashRequireUserGuildPermissions(Permissions.ManageMessages)]
[SlashRequireModuleEnabled(Module.Leveling)]
public class LevelingAdminCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("Award", "Awards a user some xp.")]
    public async Task AwardAsync(InteractionContext ctx,
        [Option("User", "User to award xp.")] DiscordUser user,
        [Minimum(0)]
        [Option("XP", "The amount of xp to grant.")] long xpToAward)
    {
        var response = await this._mediator.Send(
            new AwardUserXpCommand
            {
                UserId = user.Id,
                GuildId = ctx.Guild.Id,
                XpToAward = xpToAward,
                AwarderId = ctx.User.Id
            });

        await ctx.ReplyAsync(GrimoireColor.DarkPurple, message: $"{user.Mention} has been awarded {xpToAward} xp.", ephemeral: false);
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{user.Mention} has been awarded {xpToAward} xp by {ctx.Member.Mention}.");
    }


    [SlashCommand("Reclaim", "Takes away xp from user.")]
    public async Task ReclaimAsync(InteractionContext ctx,
        [Option("User", "User to take xp away from.")] DiscordUser user,
        [Option("Option", "Select either to take all of their xp or a specific amount.")]
        [Choice("Take all their xp.", 0)]
        [Choice("Take a specific amount.", 1)] long option,
        [Minimum(0)]
        [Option("Amount", "The amount of xp to Take.")] long amount = 0)
    {
        var xpOption = (XpOption)option;
        if (xpOption == XpOption.Amount && amount == 0)
            throw new AnticipatedException("Specify an amount greater than 0");
        var response = await this._mediator.Send(
            new ReclaimUserXpCommand
            {
                UserId = user.Id,
                GuildId = ctx.Guild.Id,
                XpToTake = amount,
                XpOption = xpOption,
                ReclaimerId = ctx.User.Id
            });

        await ctx.ReplyAsync(GrimoireColor.DarkPurple, message: $"{response.XpTaken} xp has been taken from {user.Mention}.", ephemeral: false);
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{response.XpTaken} xp has been taken from {user.Mention} by {ctx.Member.Mention}.");
    }
}
