// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

internal sealed class RollAdded
{
    internal sealed class EventHandler(IMediator mediator) : IEventHandler<GuildRoleCreatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, GuildRoleCreatedEventArgs eventArgs)
            => await this._mediator.Send(new Request { RoleId = eventArgs.Role.Id, GuildId = eventArgs.Guild.Id });
    }

    public sealed record Request : IRequest
    {
        public ulong RoleId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Request command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Roles.AddAsync(new Role { Id = command.RoleId, GuildId = command.GuildId },
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
