// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.Commands;

public sealed record AddChannelCommand : ICommand
{
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
}

public sealed class AddChannelCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<AddChannelCommand>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<Unit> Handle(AddChannelCommand command, CancellationToken cancellationToken)
    {
        if (await this._grimoireDbContext.Channels.AnyAsync(x => x.Id == command.ChannelId, cancellationToken))
            return Unit.Value;
        await this._grimoireDbContext.Channels.AddAsync(new Channel
        {
            Id = command.ChannelId,
            GuildId = command.GuildId
        }, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
