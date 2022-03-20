// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Queries.GetLoggingChannels
{
    public class GetLoggingChannelsQueryHandler : IRequestHandler<GetLoggingChannelsQuery, GetLoggingChannelsQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetLoggingChannelsQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public Task<GetLoggingChannelsQueryResponse> Handle(GetLoggingChannelsQuery request, CancellationToken cancellationToken)
            => this._cybermancyDbContext.GuildLogSettings
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => new GetLoggingChannelsQueryResponse
            {
                AvatarChannelLogId = x.AvatarChannelLogId,
                BulkDeleteChannelLogId = x.BulkDeleteChannelLogId,
                DeleteChannelLogId = x.DeleteChannelLogId,
                EditChannelLogId = x.EditChannelLogId,
                JoinChannelLogId = x.JoinChannelLogId,
                LeaveChannelLogId = x.LeaveChannelLogId,
                NicknameChannelLogId = x.NicknameChannelLogId,
                UsernameChannelLogId = x.UsernameChannelLogId,
                Success = true
            }).SingleAsync(cancellationToken);
    }
}
