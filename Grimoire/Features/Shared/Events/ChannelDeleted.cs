// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

public sealed record ChannelDeleted
{
    internal sealed class EventHandler(IMediator mediator)
        : IEventHandler<ChannelDeletedEventArgs>,
            IEventHandler<ThreadDeletedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public Task HandleEventAsync(DiscordClient sender, ChannelDeletedEventArgs eventArgs)
            => this._mediator.Send(new Command { ChannelId = eventArgs.Channel.Id });

        public Task HandleEventAsync(DiscordClient sender, ThreadDeletedEventArgs eventArgs) =>
            this._mediator.Send(new Command { ChannelId = eventArgs.Thread.Id });
    }

    public sealed record Command : IRequest
    {
        public ulong ChannelId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.Channels.Remove(new Channel { Id = command.ChannelId });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
