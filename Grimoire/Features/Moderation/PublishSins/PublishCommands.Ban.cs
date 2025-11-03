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
    [Command("Ban")]
    [Description("Publish a ban reason to the public ban log.")]
    public async Task PublishBanAsync(
        CommandContext ctx,
        [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be published.")]
        SinId sinId)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var result = await dbContext.Sins
            .AsNoTracking()
            .Where(sin => sin.SinType == SinType.Ban)
            .Where(sin => sin.Id == sinId)
            .Where(sin => sin.GuildId == guild.GetGuildId())
            .Select(sin => new
            {
                // ReSharper disable AccessToDisposedClosure
                sin.UserId,
                dbContext.UsernameHistory
                    .Where(history => history.UserId == sin.UserId)
                    .OrderByDescending(x => x.Timestamp)
                    .First().Username,
                sin.SinOn,
                sin.Reason,
                PublishedBanId = (MessageId?)sin.PublishMessages.First(x => x.PublishType == PublishType.Ban).MessageId
                // ReSharper restore AccessToDisposedClosure
            })
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Could not find a ban with that Sin Id.");
            return;
        }

        var banLogMessage = await SendPublicLogMessage(ctx, result.UserId, result.Username, result.Reason,
            result.PublishedBanId, result.SinOn, PublishType.Ban);

        await banLogMessage.Match(
            async err => await ctx.EditReplyAsync(GrimoireColor.Red, $"Failed to publish ban reason: {err.Message}"),
            async msg =>
            {
                if (result.PublishedBanId is null)
                {
                    await dbContext.PublishedMessages.AddAsync(
                        new PublishedMessage { MessageId = msg.GetMessageId(), SinId = sinId, PublishType = PublishType.Ban });
                    await dbContext.SaveChangesAsync();
                }

                await ctx.EditReplyAsync(GrimoireColor.Green, $"Successfully published ban : {sinId}");
                await this._guildLog.SendLogMessageAsync(new GuildLogMessage
                {
                    GuildId = guild.GetGuildId(),
                    GuildLogType = GuildLogType.Moderation,
                    Color = GrimoireColor.Purple,
                    Description = $"{ctx.User.Mention} published ban reason of sin {sinId}"
                });
            }

        );


    }
}
