// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Shared.SharedDtos;
using Cybermancy.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Queries.MightDeleteMessageLogQueries.GetMessage
{
    public class GetMessageQueryHandler : IRequestHandler<GetMessageQuery, GetMessageQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetMessageQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GetMessageQueryResponse> Handle(GetMessageQuery request, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Messages
            .WhereIdIs(request.MessageId)
            .Select(x => new GetMessageQueryResponse
            {
                Attachments = x.Attachments
                .Select(x => new AttachmentDto
                {
                    Id = x.Id,
                    FileName = x.FileName,
                }).ToArray(),
                AuthorId = x.Member.UserId,
                ChannelId = x.ChannelId,
                MessageId = x.Id,
                MessageContent = x.MessageHistory
                        .Where(x => x.Action != MessageAction.Deleted)
                        .OrderByDescending(x => x.TimeStamp)
                        .First()
                        .MessageContent,
                Success = true
            }).SingleOrDefaultAsync(cancellationToken);
            if (result is null)
                return new GetMessageQueryResponse { Success = false };
            return result;
        }
    }
}
