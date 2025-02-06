// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Commands;

public sealed record AddChannelCommand : IRequest
{
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
}

public sealed class AddChannelCommandHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<AddChannelCommand>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task Handle(AddChannelCommand command, CancellationToken cancellationToken)
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
