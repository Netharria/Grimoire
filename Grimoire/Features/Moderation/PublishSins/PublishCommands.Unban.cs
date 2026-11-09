// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;

namespace Grimoire.Features.Moderation.PublishSins;

public sealed partial class PublishCommands
{
    [Command("Unban")]
    [Description("Publish an unban reason to the public ban log.")]
    public async Task PublishUnbanAsync(
        CommandContext ctx,
        [MinMaxValue(1)] [Parameter("sin_id")] [Description("The id of the sin to be published.")]
        SinId sinId)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

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
                LatestPardon = dbContext.Pardons
                    .Where(p => p.SinId == x.Id)
                    .OrderByDescending(p => p.SetAt)
                    .Select(p => new
                    {
                        p.Reason,
                        PardonDate = dbContext.Pardons
                            .Where(p2 => p2.SinId == x.Id)
                            .OrderBy(p2 => p2.SetAt)
                            .Select(p2 => p2.SetAt)
                            .First()
                    })
                    .FirstOrDefault(),
                UnbanMessageId = x.PublishMessages
                    .Where(m => m.PublishType == PublishType.Unban)
                    .Select(m => (MessageId?)m.MessageId)
                    .FirstOrDefault()
                // ReSharper restore AccessToDisposedClosure
            })
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, $"Could not find a ban with Sin Id: {sinId}");
            return;
        }

        if (result.LatestPardon is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow,
                "The ban must be pardoned first before the unban can be published.");
            return;
        }

        await this.PublishAsync(ctx, dbContext, sinId, PublishType.Unban, result.UserId, result.LatestPardon.Reason.Value,
            result.UnbanMessageId, result.LatestPardon.PardonDate);
    }
}
