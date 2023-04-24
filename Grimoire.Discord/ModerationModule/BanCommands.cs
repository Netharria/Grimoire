// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Moderation.Commands.BanCommands.AddBan;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.ModerationModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageMessages)]
    [SlashRequireBotPermissions(Permissions.BanMembers)]
    public class BanCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public BanCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Ban", "Bans a user from the server.")]
        public async Task BanAsync(
            InteractionContext ctx,
            [Option("User", "The user to ban")] DiscordUser user,
            [MaximumLength(1000)]
            [Option("Reason", "The reason for the ban. This can be updated later with the 'Reason' command.")] string reason = "",
            [Option("DeleteMessages", "Deletes the messages of the user of the last few days. Default is true.")] bool deleteMessages = true,
            [Maximum(7)]
            [Minimum(0)]
            [Option("DeleteDays", "Number of days of messages to delete. Default is 7")] long deleteDays = 7)
        {
            if (!CheckIfCanBan(ctx, user))
            {
                await ctx.ReplyAsync(GrimoireColor.Orange, "I do not have permissions to ban that user.");
                return;
            }
            if (ctx.User.Id == user.Id)
            {
                await ctx.ReplyAsync(GrimoireColor.Orange, "You can't ban yourself.");
                return;
            }

            var response = await this._mediator.Send(new AddBanCommand
            {
                GuildId = ctx.Guild.Id,
                UserId = user.Id,
                ModeratorId = ctx.User.Id,
                Reason = reason
            });

            try
            {
                if (user is DiscordMember member)
                {
                    await member.SendMessageAsync(new DiscordEmbedBuilder()
                        .WithTitle($"Ban ID {response.SinId}")
                        .WithDescription($"You have been banned from {ctx.Guild.Name} "
                        + (!string.IsNullOrWhiteSpace(reason) ? $"for {reason}" : ""))
                        .WithColor(GrimoireColor.Orange));
                }
            }
            catch (Exception ex)
            {
                if (ex is not UnauthorizedException)
                    ctx.Client.Logger.LogWarning(ex, "Was not able to send a direct message to user.");
            }

            await ctx.Guild.BanMemberAsync(
                user.Id,
                deleteMessages ? (int)deleteDays : 0,
                reason);

            await ctx.ReplyAsync(
                GrimoireColor.Orange,
                title: "Banned",
                message: $"**Reason:** {reason}\n" +
                $"{user.GetUsernameWithDiscriminator()}: Ban Id {response.SinId}");
        }

        [SlashCommand("Unban", "Bans a user from the server.")]
        public async Task UnbanAsync(
            InteractionContext ctx,
            [Option("User", "The user to unban")] DiscordUser user)
        {
            try
            {
                await ctx.Guild.UnbanMemberAsync(user.Id);
                await ctx.ReplyAsync(
                GrimoireColor.Orange,
                title: "Unbanned",
                message: $"{user.GetUsernameWithDiscriminator()} was unbanned.");
            }
            catch (Exception ex) when (ex is NotFoundException || ex is ServerErrorException)
            {
                var errorMessage = ex is NotFoundException
                                    ? "user could not be found."
                                    : "error when communicating with discord. Try again before asking for help.";
                await ctx.ReplyAsync(
                GrimoireColor.Orange,
                title: "Error",
                message: $"{user.GetUsernameWithDiscriminator()} was not unbanned because {errorMessage}");
            }
        }

        private static bool CheckIfCanBan(InteractionContext ctx, DiscordUser user)
        {
            if (user is not DiscordMember member)
                return true;
            return ctx.Guild.CurrentMember.Hierarchy > member.Hierarchy;
        }
    }
}
