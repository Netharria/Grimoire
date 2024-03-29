// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.Commands;

public sealed record DeleteChannelCommand : ICommand
{
    public ulong ChannelId { get; init; }
}

public sealed class DeleteChannelCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<DeleteChannelCommand>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<Unit> Handle(DeleteChannelCommand command, CancellationToken cancellationToken)
    {
        this._grimoireDbContext.Channels.Remove(new Channel { Id = command.ChannelId });
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
