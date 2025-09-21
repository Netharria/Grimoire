// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

internal sealed class RoleDeleted
{
    internal sealed class EventHandler(IMediator mediator) : IEventHandler<GuildRoleDeletedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, GuildRoleDeletedEventArgs eventArgs)
            => await this._mediator.Send(new Request { RoleId = eventArgs.Role.Id });
    }

    public sealed record Request : IRequest
    {
        public RoleId RoleId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Request request, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.Roles.Remove(dbContext.Roles.First(x => x.Id == request.RoleId));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
