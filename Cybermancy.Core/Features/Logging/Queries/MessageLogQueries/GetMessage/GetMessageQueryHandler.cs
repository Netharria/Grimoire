// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Queries.MessageLogQueries.GetMessage
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
            var result = await _cybermancyDbContext.Messages
                .Where(x => x.Id == request.MessageId)
                .Select(x => new GetMessageQueryResponse
                {
                    AttachmentUrls = x.Attachments.Select(x => x.AttachmentUrl).ToArray(),
                    AuthorAvatarUrl = x.Author.GuildAvatarUrl ?? x.Author.User.AvatarUrl,
                    AuthorId = x.Author.Id,
                    AuthorName = x.Author.User.UserName,
                    ChannelId = x.ChannelId,
                    ChannelName = x.Channel.Name,
                    MessageContent = x.Content,
                    MessageId = x.Id,
                    Success = true

                }).SingleOrDefaultAsync(cancellationToken);
            if(result is null)
                return new GetMessageQueryResponse { Success = false };
            return result;

        }
    }
}
