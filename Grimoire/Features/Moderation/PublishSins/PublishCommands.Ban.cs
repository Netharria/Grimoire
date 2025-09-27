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
        SlashCommandContext ctx,
        [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be published.")]
        int sinId)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new GetBanForPublish.Query { SinId = sinId, GuildId = ctx.Guild.Id });

        var banLogMessage = await SendPublicLogMessage(ctx, response, PublishType.Ban, this._logger);
        if (response.PublishedMessage is null)
            await this._mediator.Send(new PublishBan.Command
            {
                SinId = sinId, MessageId = banLogMessage.Id, PublishType = PublishType.Ban
            });

        await ctx.EditReplyAsync(GrimoireColor.Green, $"Successfully published ban : {sinId}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} published ban reason of sin {sinId}"
        });
    }
}

public sealed class GetBanForPublish
{
    public sealed record Query : IRequest<Response>
    {
        public long SinId { get; init; }
        public GuildId GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Sins
                .AsNoTracking()
                .Where(sin => sin.SinType == SinType.Ban)
                .Where(sin => sin.Id == request.SinId)
                .Where(sin => sin.GuildId == request.GuildId)
                .Select(sin => new
                {
                    sin.UserId,
                    UsernameHistory = sin.Member.User.UsernameHistories
                        .OrderByDescending(x => x.Timestamp)
                        .FirstOrDefault(),
                    sin.Guild.ModerationSettings.PublicBanLog,
                    sin.SinOn,
                    sin.Reason,
                    PublishedBan = sin.PublishMessages.FirstOrDefault(x => x.PublishType == PublishType.Ban)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null)
                throw new AnticipatedException("Could not find a ban with that Sin Id");
            if (result.PublicBanLog is null)
                throw new AnticipatedException("No Public Ban Log is configured.");

            return new Response
            {
                UserId = result.UserId,
                Username = result.UsernameHistory?.Username,
                BanLogId = result.PublicBanLog.Value,
                Date = result.SinOn,
                Reason = result.Reason,
                PublishedMessage = result.PublishedBan?.MessageId
            };
        }
    }

    public sealed record Response
    {
        public ulong BanLogId { get; init; }
        public DateTimeOffset Date { get; init; }
        public string? Username { get; init; }
        public ulong UserId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public ulong? PublishedMessage { get; init; }
    }
}
