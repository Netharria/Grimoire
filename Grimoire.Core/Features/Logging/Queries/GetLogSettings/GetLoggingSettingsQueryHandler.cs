// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Queries.GetLogSettings
{
    public class GetLoggingSettingsQueryHandler : IRequestHandler<GetLoggingSettingsQuery, GetLoggingSettingsQueryResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext;

        public GetLoggingSettingsQueryHandler(GrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetLoggingSettingsQueryResponse> Handle(GetLoggingSettingsQuery request, CancellationToken cancellationToken)
        {
            return await this._grimoireDbContext.Guilds
                .Where(x => x.Id == request.GuildId)
                .Select(x => new GetLoggingSettingsQueryResponse
                {
                    JoinChannelLog = x.UserLogSettings.JoinChannelLogId,
                    LeaveChannelLog = x.UserLogSettings.LeaveChannelLogId,
                    DeleteChannelLog = x.MessageLogSettings.DeleteChannelLogId,
                    BulkDeleteChannelLog = x.MessageLogSettings.BulkDeleteChannelLogId,
                    EditChannelLog = x.MessageLogSettings.EditChannelLogId,
                    UsernameChannelLog = x.UserLogSettings.UsernameChannelLogId,
                    NicknameChannelLog = x.UserLogSettings.NicknameChannelLogId,
                    AvatarChannelLog = x.UserLogSettings.AvatarChannelLogId,
                    IsLoggingEnabled = x.UserLogSettings.ModuleEnabled
                }).FirstAsync(cancellationToken: cancellationToken);
        }
    }
}
