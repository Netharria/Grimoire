// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.MessageLogging.Queries;

public sealed record GetMessageAuthorQuery : IQuery<ulong?>
{
    public ulong MessageId { get; init; }
}

public sealed class GetMessageAuthorQueryHandler(GrimoireDbContext grimoire) : IQueryHandler<GetMessageAuthorQuery, ulong?>
{
    private readonly GrimoireDbContext _grimoire = grimoire;

    public async ValueTask<ulong?> Handle(GetMessageAuthorQuery query, CancellationToken cancellationToken)
    {
        var message = await this._grimoire.Messages
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == query.MessageId, cancellationToken);
        return message?.UserId;
    }
}
