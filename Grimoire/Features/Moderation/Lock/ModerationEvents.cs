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
            if (args.Channel.PermissionsFor(member).HasPermission(DiscordPermissions.ManageMessages))
                return;
            if (await this._mediator.Send(new GetLockQuery { ChannelId = args.Channel.Id, GuildId = args.Guild.Id }))
                await args.Message.DeleteAsync();
        }

        public async Task HandleEventAsync(DiscordClient sender, MessageReactionAddedEventArgs args)
        {
            if (!args.Channel.IsThread)
                return;
            if (args.User is not DiscordMember member)
                return;
            if (args.Channel.PermissionsFor(member).HasPermission(DiscordPermissions.ManageMessages))
                return;
            if (await this._mediator.Send(new GetLockQuery { ChannelId = args.Channel.Id, GuildId = args.Guild.Id }))
                await args.Message.DeleteReactionAsync(args.Emoji, args.User, "Thread is locked.");
        }
    }

    public sealed record GetLockQuery : IRequest<bool>
    {
        public ulong ChannelId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<GetLockQuery, bool>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<bool> Handle(GetLockQuery query, CancellationToken cancellationToken)
            => await this._grimoireDbContext.Locks
                .AsNoTracking()
                .AnyAsync(x => x.ChannelId == query.ChannelId && x.GuildId == query.GuildId, cancellationToken);
    }
}