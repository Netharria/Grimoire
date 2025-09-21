// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

internal sealed class ChannelAdded
{
    public sealed class EventHandler(IMediator mediator) : IEventHandler<ChannelCreatedEventArgs>,
        IEventHandler<ThreadCreatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public Task HandleEventAsync(DiscordClient sender, ChannelCreatedEventArgs eventArgs)
            => this._mediator.Send(
                new Command { ChannelId = eventArgs.Channel.Id, GuildId = eventArgs.Guild.Id });

        public Task HandleEventAsync(DiscordClient sender, ThreadCreatedEventArgs eventArgs)
            => this._mediator.Send(
                new Command { ChannelId = eventArgs.Thread.Id, GuildId = eventArgs.Guild.Id });
    }

    public sealed record Command : IRequest
    {
        public GuildId GuildId { get; init; }
        public ChannelId ChannelId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            if (await dbContext.Channels
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == command.ChannelId, cancellationToken))
                return;
            await dbContext.Channels.AddAsync(
                new Channel { Id = command.ChannelId, GuildId = command.GuildId }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
