// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Commands;

public sealed record PublishBanCommand : ICommand
{
    public long SinId { get; init; }
    public ulong GuildId { get; init; }
    public ulong MessageId { get; init; }
    public PublishType PublishType { get; init; }
}

public class PublishBanCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<PublishBanCommand>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<Unit> Handle(PublishBanCommand command, CancellationToken cancellationToken)
    {
        await this._grimoireDbContext.PublishedMessages.AddAsync(new PublishedMessage
        {
            MessageId = command.MessageId,
            SinId = command.SinId,
            PublishType = command.PublishType,
        }, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new Unit();
    }
}
