// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Queries.GetMessageAuthor;
public class GetMessageAuthorQueryHandler : IQueryHandler<GetMessageAuthorQuery, ulong?>
{
    private readonly IGrimoireDbContext _grimoire;

    public GetMessageAuthorQueryHandler(IGrimoireDbContext grimoire)
    {
        this._grimoire = grimoire;
    }

    public async ValueTask<ulong?> Handle(GetMessageAuthorQuery query, CancellationToken cancellationToken)
    {
        var message = await this._grimoire.Messages
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == query.MessageId, cancellationToken);
        return message?.UserId;
    }
}
