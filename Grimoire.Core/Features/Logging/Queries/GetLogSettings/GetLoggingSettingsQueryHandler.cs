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
            return await this._grimoireDbContext.GuildLogSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new GetLoggingSettingsQueryResponse
                {
                    JoinChannelLog = x.JoinChannelLogId,
                    LeaveChannelLog = x.LeaveChannelLogId,
                    DeleteChannelLog = x.DeleteChannelLogId,
                    BulkDeleteChannelLog = x.BulkDeleteChannelLogId,
                    EditChannelLog = x.EditChannelLogId,
                    UsernameChannelLog = x.UsernameChannelLogId,
                    NicknameChannelLog = x.NicknameChannelLogId,
                    AvatarChannelLog = x.AvatarChannelLogId,
                    IsLoggingEnabled = x.ModuleEnabled
                }).FirstAsync(cancellationToken: cancellationToken);
        }
    }
}
