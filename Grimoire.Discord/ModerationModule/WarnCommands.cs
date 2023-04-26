// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Moderation.Commands.WarnCommands;

namespace Grimoire.Discord.ModerationModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageMessages)]
    public class WarnCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public WarnCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Warn", "Issue a warning to the user.")]
        public async Task WarnAsync(InteractionContext ctx,
            [Option("User", "The user to warn.")] DiscordUser user,
            [Option("Reason", "The reason for the warn.")] string reason)
        {
            if (user is not DiscordMember member)
                throw new AnticipatedException("The user supplied is not part of this server.");
            if (ctx.User == user)
                throw new AnticipatedException("You cannot warn yourself.");
            var response = await this._mediator.Send(new WarnUserCommand
            {
                UserId = user.Id,
                GuildId = ctx.Guild.Id,
                ModeratorId = ctx.User.Id,
                Reason = reason
            });

            await ctx.ReplyAsync(title: "Warning",
                message: $"**Reason:** {reason}\n" +
                         $"{user.Mention}: Warning **ID {response.SinId}**",
                ephemeral: false);
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                title: "Warning",
                message: $"**Mod:** {ctx.User.Mention}" +
                         $"**Reason:** {reason}\n" +
                         $"{user.Mention}: Warning **ID {response.SinId}**");
            try
            {
                await member.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithTitle($"Warning Id {response.SinId}")
                .WithDescription($"You have been warned by {ctx.User.Mention} for {reason}"));
            }
            catch (UnauthorizedException)
            {
                await ctx.SendLogAsync(response, GrimoireColor.Red,
                    message: $"Was not able to send a direct message with the warn details to {user.Mention}");
            }
        }
    }
}
