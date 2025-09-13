// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Exceptions;
using Grimoire.Features.Moderation.Ban.Shared;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Ban.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[RequirePermissions([DiscordPermission.BanMembers], [])]
public sealed partial class AddBanCommand(IMediator mediator, ILogger<AddBanCommand> logger)
{
    private readonly ILogger<AddBanCommand> _logger = logger;
    private readonly IMediator _mediator = mediator;

    [Command("Ban")]
    [Description("Bans a user from the server.")]
    public async Task BanAsync(
        SlashCommandContext ctx,
        [Parameter("User")]
        [Description("The user to ban.")]
        DiscordUser user,
        [MinMaxLength(maxLength:1000)]
        [Parameter("Reason")]
        [Description("The reason for the ban. This can be updated later with the 'Reason' command.")]
        string reason = "",
        [Parameter("DeleteMessages")]
        [Description("Deletes the messages of the user of the last few days. Default is false.")]
        bool deleteMessages = false,
        [MinMaxValue(0,7)]
        [Parameter("DeleteDays")]
        [Description("The number of days of messages to delete. Default is 7.")]
        int deleteDays = 7)
    {
        await ctx.DeferResponseAsync();

        if(ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        if (!CheckIfCanBan(ctx.Guild.CurrentMember, user))
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
            GuildId = ctx.Guild.Id, UserId = user.Id, ModeratorId = ctx.User.Id, Reason = reason
        });

        try
        {
            if (user is DiscordMember member)
                await member.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor($"Ban ID {response.SinId}")
                    .WithDescription($"You have been banned from {ctx.Guild.Name} "
                                     + (!string.IsNullOrWhiteSpace(reason) ? $"for {reason}" : ""))
                    .WithColor(GrimoireColor.Red));
        }
        catch (Exception ex)
        {
            if (ex is not UnauthorizedException)
                LogFailedDirectMessage(this._logger, ex);
        }

        await ctx.Guild.BanMemberAsync(
            user,
            deleteMessages ? TimeSpan.FromDays(deleteDays) : TimeSpan.Zero,
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
    static partial void LogFailedDirectMessage(ILogger<AddBanCommand> logger, Exception ex);

    private static bool CheckIfCanBan(DiscordMember botMember, DiscordUser user)
    {
        if (user is not DiscordMember member)
            return true;
        return botMember.Hierarchy > member.Hierarchy;
    }
}
