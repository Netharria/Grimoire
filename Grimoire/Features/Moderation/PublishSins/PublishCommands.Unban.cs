// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands;

namespace Grimoire.Features.Moderation.PublishSins;

public sealed partial class PublishCommands
{
    [Command("Unban")]
    [Description("Publish an unban reason to the public ban log.")]
    public async Task PublishUnbanAsync(
        SlashCommandContext ctx,
        [MinMaxValue(0)]
        [Parameter("SinId")]
        [Description("The id of the sin to be published.")]
        long sinId)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response =
            await this._mediator.Send(new GetUnbanForPublish.Query { SinId = sinId, GuildId = ctx.Guild.Id });

        var banLogMessage = await SendPublicLogMessage(ctx, response, PublishType.Unban, this._logger);
        if (response.PublishedMessage is null)
            await this._mediator.Send(new PublishBan.Command
            {
                SinId = sinId, MessageId = banLogMessage.Id, PublishType = PublishType.Unban
            });

        await ctx.EditReplyAsync(GrimoireColor.Green, $"Successfully published unban : {sinId}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.User.GetUsernameWithDiscriminator()} published unban reason of sin {sinId}");
    }
}

public sealed class GetUnbanForPublish
{
    public sealed record Query : IRequest<GetBanForPublish.Response>
    {
        public long SinId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class GetUnbanQueryHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, GetBanForPublish.Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<GetBanForPublish.Response> Handle(Query request, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Sins
                .AsNoTracking()
                .Where(x => x.SinType == SinType.Ban)
                .Where(x => x.Id == request.SinId)
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new
                {
                    x.UserId,
                    UsernameHistory = x.Member.User.UsernameHistories
                        .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                        .First(),
                    x.Guild.ModerationSettings.PublicBanLog,
                    x.Guild.ModChannelLog,
                    x.Pardon,
                    PublishedUnban = x.PublishMessages
                        .FirstOrDefault(publishedMessage => publishedMessage.PublishType == PublishType.Unban)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null)
                throw new AnticipatedException("Could not find a ban with that Sin Id");
            if (result.PublicBanLog is null)
                throw new AnticipatedException("No Public Ban Log is configured.");
            if (result.Pardon is null)
                throw new AnticipatedException("The ban must be pardoned first before the unban can be published.");

            return new GetBanForPublish.Response
            {
                UserId = result.UserId,
                Username = result.UsernameHistory.Username,
                BanLogId = result.PublicBanLog.Value,
                Date = result.Pardon.PardonDate,
                LogChannelId = result.ModChannelLog,
                Reason = result.Pardon.Reason,
                PublishedMessage = result.PublishedUnban?.MessageId
            };
        }
    }
}
