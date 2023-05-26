// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.AwardUserXp;
using Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.ReclaimUserXp;

namespace Grimoire.Discord.LevelingModule
{
    [SlashRequireGuild]
    [SlashRequireUserGuildPermissions(Permissions.ManageMessages)]
    [SlashRequireModuleEnabled(Module.Leveling)]
    public class LevelingAdminCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public LevelingAdminCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Award", "Awards a user some xp.")]
        public async Task AwardAsync(InteractionContext ctx,
            [Option("User", "User to award xp.")] DiscordUser user,
            [Minimum(0)]
            [Option("XP", "The amount of xp to grant.")] long xpToAward)
        {
            await this._mediator.Send(new AwardUserXpCommand { UserId = user.Id, GuildId = ctx.Guild.Id, XpToAward = xpToAward, AwarderId = ctx.User.Id });

            await ctx.ReplyAsync(GrimoireColor.DarkPurple, message: $"{user.Mention} has been awarded {xpToAward} xp.", ephemeral: false);
        }


        [SlashCommand("Reclaim", "Takes away xp from user.")]
        public async Task ReclaimAsync(InteractionContext ctx,
            [Option("User", "User to take xp away from.")] DiscordUser user,
            [Option("XP", "The amount of xp to Take. Enter 'all' to take all xp.")] string amount)
        {
            await this._mediator.Send(
                new ReclaimUserXpCommand
                {
                    UserId = user.Id,
                    GuildId = ctx.Guild.Id,
                    XpToTake = amount,
                    ReclaimerId = ctx.User.Id
                });

            await ctx.ReplyAsync(GrimoireColor.DarkPurple, message: $"{amount} xp has been taken from {user.Mention}.", ephemeral: false);
        }
    }
}
