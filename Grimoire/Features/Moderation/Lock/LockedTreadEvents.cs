// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Lock;

public sealed class LockedTreadEvents
{
    public sealed class EventHandler(IMediator mediator)
        : IEventHandler<MessageCreatedEventArgs>
            , IEventHandler<MessageReactionAddedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
        {
            if (!args.Channel.IsThread)
                return;
            if (args.Author is not DiscordMember member)
                return;
            if (args.Channel.PermissionsFor(member).HasPermission(DiscordPermission.ManageMessages))
                return;
            if (await this._mediator.Send(new GetLockQuery { ChannelId = args.Channel.Id, GuildId = args.Guild.Id }))
                await args.Message.DeleteAsync("Thread is locked.");
        }

        public async Task HandleEventAsync(DiscordClient sender, MessageReactionAddedEventArgs args)
        {
            if (!args.Channel.IsThread)
                return;
            if (args.User is not DiscordMember member)
                return;
            if (args.Channel.PermissionsFor(member).HasPermission(DiscordPermission.ManageMessages))
                return;
            if (await this._mediator.Send(new GetLockQuery { ChannelId = args.Channel.Id, GuildId = args.Guild.Id }))
                await args.Message.DeleteReactionAsync(args.Emoji, args.User, "Thread is locked.");
        }
    }

    public sealed record GetLockQuery : IRequest<bool>
    {
        public ChannelId ChannelId { get; init; }
        public GuildId GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<GetLockQuery, bool>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<bool> Handle(GetLockQuery query, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Locks
                .AsNoTracking()
                .AnyAsync(x => x.ChannelId == query.ChannelId && x.GuildId == query.GuildId, cancellationToken);
        }
    }
}
