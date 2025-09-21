// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.TrackerLog;

namespace Grimoire.Features.Logging.Trackers.Events;

public class TrackerMessageUpdateEvent
{
    public class EventHandler(IMediator mediator, TrackerLog trackerLog) : IEventHandler<MessageUpdatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;
        private readonly TrackerLog _trackerLog = trackerLog;

        public async Task HandleEventAsync(DiscordClient sender, MessageUpdatedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Message.Content))
                return;

            var response = await this._mediator.Send(
                new Request { UserId = args.Author.Id, GuildId = args.Guild.Id, MessageId = args.Message.Id });

            if (response is null)
                return;

            await this._trackerLog.SendTrackerMessageAsync(new TrackerMessageCustomEmbed
            {
                TrackerId = args.Author.Id,
                GuildId = args.Guild.Id,
                TrackerIdType = TrackerIdType.UserId,
                Embed = new DiscordEmbedBuilder()
                    .AddField("User", args.Author.Mention, true)
                    .AddField("Channel", args.Channel.Mention, true)
                    .AddField("Link", $"**[Jump URL]({args.Message.JumpLink})**", true)
                    .WithFooter("Message Sent", args.Author.GetAvatarUrl(MediaFormat.Auto))
                    .WithTimestamp(DateTime.UtcNow)
                    .AddMessageTextToFields("Before", response.OldMessageContent)
                    .AddMessageTextToFields("After", args.Message.Content)
            });
        }
    }

    public sealed record Request : IRequest<Response?>
    {
        public ulong UserId { get; init; }
        public GuildId GuildId { get; init; }
        public ulong MessageId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Request request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Trackers
                .AsNoTracking()
                .WhereMemberHasId(request.UserId, request.GuildId)
                .Select(tracker => new Response
                {
                    OldMessageContent = tracker.Member.Messages
                        .Where(message => message.Id == request.MessageId)
                        .Select(message => message.MessageHistory
                            .Where(messageHistory => messageHistory.Action != MessageAction.Deleted
                                                     && messageHistory.TimeStamp < DateTime.UtcNow.AddSeconds(-1))
                            .OrderByDescending(x => x.TimeStamp)
                            .First().MessageContent)
                        .First()
                }).FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response
    {
        public string OldMessageContent { get; init; } = string.Empty;
    }
}
