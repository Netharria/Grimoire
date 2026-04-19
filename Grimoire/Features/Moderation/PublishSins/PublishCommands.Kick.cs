// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.PublishSins;

public sealed partial class PublishCommands
{
    [Command("Kick")]
    [Description("Publish a kick reason to the public ban log.")]
    public async Task PublishKickAsync(
        CommandContext ctx,
        [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be published.")]
        SinId sinId)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var result = await dbContext.Sins
            .AsNoTracking()
            .Where(sin => sin.SinType == SinType.Kick)
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
                Reason = dbContext.SinReasonHistory
                    .Where(r => r.SinId == sin.Id)
                    .OrderByDescending(r => r.SetAt)
                    .Select(r => r.Reason)
                    .FirstOrDefault() ?? string.Empty,
                KickMessageId = sin.PublishMessages
                    .Where(x => x.PublishType == PublishType.Kick)
                    .Select(x => (MessageId?)x.MessageId)
                    .FirstOrDefault()
                // ReSharper restore AccessToDisposedClosure
            })
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not find a kick with that Sin Id.");
            return;
        }

        var kickLogMessage = await SendPublicLogMessage(ctx, result.UserId, result.Username, result.Reason,
            result.KickMessageId, result.SinOn, PublishType.Kick);

        if (kickLogMessage is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Red,
                $"Failed to publish kick reason. Verify {ctx.Guild?.CurrentMember.Mention} has access to send messages in the public ban log channel.");
            return;
        }

        if (result.KickMessageId is null)
        {
            await dbContext.PublishedMessages.AddAsync(
                new PublishedMessage
                {
                    MessageId = kickLogMessage.GetMessageId(), SinId = sinId, PublishType = PublishType.Kick
                });
            await dbContext.SaveChangesAsync();
        }

        await ctx.ReplyAsync(GrimoireColor.Green, $"Successfully published kick : {sinId}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} published kick reason of sin {sinId}"
        });
    }
}
