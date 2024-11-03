// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.PublishSins;

public sealed record PublishBanCommand : IRequest
{
    public long SinId { get; init; }
    public ulong GuildId { get; init; }
    public ulong MessageId { get; init; }
    public PublishType PublishType { get; init; }
}

public sealed class PublishBanCommandHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<PublishBanCommand>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task Handle(PublishBanCommand command, CancellationToken cancellationToken)
    {
        await this._grimoireDbContext.PublishedMessages.AddAsync(
            new PublishedMessage
            {
                MessageId = command.MessageId, SinId = command.SinId, PublishType = command.PublishType
            }, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
    }
}
