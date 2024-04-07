// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Moderation.Commands;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.ModerationModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserGuildPermissions(Permissions.ManageMessages)]
[SlashRequireBotPermissions(Permissions.BanMembers)]
public sealed partial class BanCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("Ban", "Bans a user from the server.")]
    public async Task BanAsync(
        InteractionContext ctx,
        [Option("User", "The user to ban")] DiscordUser user,
        [MaximumLength(1000)]
        [Option("Reason", "The reason for the ban. This can be updated later with the 'Reason' command.")] string reason = "",
        [Option("DeleteMessages", "Deletes the messages of the user of the last few days. Default is false.")] bool deleteMessages = false,
        [Maximum(7)]
        [Minimum(0)]
        [Option("DeleteDays", "Number of days of messages to delete. Default is 7")] long deleteDays = 7)
    {
        await ctx.DeferAsync();
        if (!CheckIfCanBan(ctx, user))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "I do not have permissions to ban that user.");
            return;
        }
        if (ctx.User.Id == user.Id)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "You can't ban yourself.");
            return;
        }

        var response = await this._mediator.Send(new AddBan.Command
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
                    .WithAuthor($"Ban ID {response.SinId}")
                    .WithDescription($"You have been banned from {ctx.Guild.Name} "
                    + (!string.IsNullOrWhiteSpace(reason) ? $"for {reason}" : ""))
                    .WithColor(GrimoireColor.Red));
            }
        }
        catch (Exception ex)
        {
            if (ex is not UnauthorizedException)
                LogFailedDirectMessage(ctx.Client.Logger, ex);
        }

        await ctx.Guild.BanMemberAsync(
            user.Id,
            deleteMessages ? (int)deleteDays : 0,
            reason);

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Banned")
            .AddField("User", user.Mention, true)
            .AddField("Sin Id", $"**{response.SinId}**", true)
            .AddField("Moderator", ctx.User.Mention, true)
            .AddField("Reason", string.IsNullOrWhiteSpace(reason) ? "None" : reason)
            .WithColor(GrimoireColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.EditReplyAsync(embed: embed);
    }

    [LoggerMessage(LogLevel.Warning, "Was not able to send a direct message to user.")]
    private static partial void LogFailedDirectMessage(ILogger<BaseDiscordClient> logger, Exception ex);

    [SlashCommand("Unban", "Bans a user from the server.")]
    public static async Task UnbanAsync(
        InteractionContext ctx,
        [Option("User", "The user to unban")] DiscordUser user)
    {
        await ctx.DeferAsync();
        try
        {
            await ctx.Guild.UnbanMemberAsync(user.Id);
            await ctx.EditReplyAsync(embed: new DiscordEmbedBuilder()
            .WithAuthor("Unbanned")
            .AddField("User", user.Mention, true)
            .AddField("Moderator", ctx.User.Mention, true)
            .WithColor(GrimoireColor.Green));
        }
        catch (Exception ex) when (ex is NotFoundException || ex is ServerErrorException)
        {
            var errorMessage = ex is NotFoundException
                                ? "user could not be found."
                                : "error when communicating with discord. Try again before asking for help.";
            await ctx.EditReplyAsync(
            GrimoireColor.Yellow,
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
