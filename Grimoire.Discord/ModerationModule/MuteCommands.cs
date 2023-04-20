// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Moderation.Commands.MuteCommands.MuteUserCommand;
using Grimoire.Core.Features.Moderation.Commands.MuteCommands.UnmuteUserCommand;

namespace Grimoire.Discord.ModerationModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageMessages)]
    public class MuteCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public MuteCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Mute", "Prevents the user from being able to speak.")]
        public async Task MuteUserAsync(
            InteractionContext ctx,
            [Option("User", "The User to mute.")] DiscordUser user,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")] DurationType durationType,
            [Minimum(0)]
            [Option("DurationAmount", "Select the amount of time the mute will last.")] long durationAmount,
            [Option("Reason", "The reason why the user is getting muted.")] string? reason = null
            )
        {
            if (user is not DiscordMember member) throw new AnticipatedException("That user is not on the server.");
            if (ctx.Guild.Id == member.Id) throw new AnticipatedException("That user is not on the server.");
            var response = await _mediator.Send(new MuteUserCommand
            {
                UserId = member.Id,
                GuildId = ctx.Guild.Id,
                DurationAmount = durationAmount,
                DurationType = durationType,
                ModeratorId = ctx.User.Id,
                Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason
            });
            var muteRole = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole);
            if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
            await member.GrantRoleAsync(muteRole, reason);
            await ctx.ReplyAsync(message: $"{member.Mention} has been muted for {durationAmount} {durationType.GetName()}", ephemeral: false); ;
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{member.Mention} has been muted for {durationAmount} {durationType.GetName()} by {ctx.User.Mention}");
        }

        [SlashCommand("Unmute", "Removes the mute on the user allowing them to speak.")]
        public async Task UnmuteUserAsync(
            InteractionContext ctx,
            [Option("User", "The User to unmute.")] DiscordUser user)
        {
            if (user is not DiscordMember member) throw new AnticipatedException("That user is not on the server.");
            if (ctx.Guild.Id == member.Id) throw new AnticipatedException("That user is not on the server.");
            var response = await _mediator.Send(new UnmuteUserCommand
            {
                UserId = member.Id,
                GuildId = ctx.Guild.Id
            });
            var muteRole = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole);
            if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
            await member.RevokeRoleAsync(muteRole, $"Unmuted by {ctx.Member.Mention}");
            await ctx.ReplyAsync(message: $"{member.Mention} has been unmuted", ephemeral: false); ;
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{member.Mention} has been unmuted by {ctx.User.Mention}");

        }
    }
}
