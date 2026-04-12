// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.PublishSins;

public sealed partial class PublishCommands
{
    [Command("Unban")]
    [Description("Publish an unban reason to the public ban log.")]
    public async Task PublishUnbanAsync(
        CommandContext ctx,
        [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be published.")]
        SinId sinId)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var result = await dbContext.Sins
            .AsNoTracking()
            .Where(x => x.SinType == SinType.Ban)
            .Where(x => x.Id == sinId)
            .Where(x => x.GuildId == guild.GetGuildId())
            .Select(x => new
            {
                // ReSharper disable AccessToDisposedClosure
                x.UserId,
                dbContext.UsernameHistory
                    .Where(history => history.UserId == x.UserId)
                    .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                    .First().Username,
                x.Pardon,
                UnbanMessageId = (MessageId?)x.PublishMessages
                    .First(publishedMessage => publishedMessage.PublishType == PublishType.Unban)
                    .MessageId
                // ReSharper restore AccessToDisposedClosure
            })
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, $"Could not find a ban with Sin Id: {sinId}");
            return;
        }

        if (result.Pardon is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow,
                "The ban must be pardoned first before the unban can be published.");
            return;
        }

        var banLogMessage = await SendPublicLogMessage(ctx, result.UserId, result.Username, result.Pardon.Reason,
            result.UnbanMessageId, result.Pardon.PardonDate, PublishType.Unban);

        if (banLogMessage is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Red,
                $"Failed to publish unban reason. Verify {ctx.Guild?.CurrentMember.Mention} has access to send messages in the public ban log channel.");
            return;
        }

        if (result.UnbanMessageId is null)
        {
            await dbContext.PublishedMessages.AddAsync(
                new PublishedMessage
                {
                    MessageId = banLogMessage.GetMessageId(), SinId = sinId, PublishType = PublishType.Unban
                });
            await dbContext.SaveChangesAsync();
        }


        await ctx.ReplyAsync(GrimoireColor.Green, $"Successfully published unban : {sinId}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} published unban reason of sin {sinId}"
        });
    }
}
