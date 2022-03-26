// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Queries.GetLogSettings
{
    public class GetLoggingSettingsQueryHandler : IRequestHandler<GetLoggingSettingsQuery, GetLoggingSettingsQueryResponse>
    {
        private readonly CybermancyDbContext _cybermancyDbContext;

        public GetLoggingSettingsQueryHandler(CybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GetLoggingSettingsQueryResponse> Handle(GetLoggingSettingsQuery request, CancellationToken cancellationToken)
        {
            var guildLevelSettings = await this._cybermancyDbContext.GuildLogSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new
                {
                    x.IsLoggingEnabled,
                    x.JoinChannelLogId,
                    x.LeaveChannelLogId,
                    x.DeleteChannelLogId,
                    x.BulkDeleteChannelLogId,
                    x.EditChannelLogId,
                    x.UsernameChannelLogId,
                    x.NicknameChannelLogId,
                    x.AvatarChannelLogId
                }).FirstAsync(cancellationToken: cancellationToken);
            return new GetLoggingSettingsQueryResponse
            {
                JoinChannelLog = guildLevelSettings.JoinChannelLogId,
                LeaveChannelLog = guildLevelSettings.LeaveChannelLogId,
                DeleteChannelLog = guildLevelSettings.DeleteChannelLogId,
                BulkDeleteChannelLog = guildLevelSettings.BulkDeleteChannelLogId,
                EditChannelLog = guildLevelSettings.EditChannelLogId,
                UsernameChannelLog = guildLevelSettings.UsernameChannelLogId,
                NicknameChannelLog = guildLevelSettings.NicknameChannelLogId,
                AvatarChannelLog = guildLevelSettings.AvatarChannelLogId,
                IsLoggingEnabled = guildLevelSettings.IsLoggingEnabled,
                Success = true
            };
        }
    }
}
