// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Shared.SharedDtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Queries.MightDeleteMessageLogQueries.GetMessages
{
    public class GetMessagesQueryHandler : IRequestHandler<GetMessagesQuery, GetMessagesQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetMessagesQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GetMessagesQueryResponse> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
        {
            var results = await this._cybermancyDbContext.Messages
                .WhereIdsAre(request.MesssageIds)
                .Select(x => new MessageDto
                {
                    Attachments = x.Attachments
                    .Select(x => new AttachmentDto
                    {
                        Id = x.Id,
                        FileName = x.FileName,
                    }).ToArray(),
                    UserId = x.Member.UserId,
                    ChannelId = x.ChannelId,
                    MessageContent = x.MessageHistory
                        .Where(x => x.Action != Domain.MessageAction.Deleted)
                        .OrderByDescending(x => x.TimeStamp)
                        .First()
                        .MessageContent,
                    MessageId = x.Id
                }).ToArrayAsync(cancellationToken);
            if (!results.Any())
                return new GetMessagesQueryResponse { Success = false };
            return new GetMessagesQueryResponse
            {
                Messages = results,
                Success = true
            };
        }
    }
}
