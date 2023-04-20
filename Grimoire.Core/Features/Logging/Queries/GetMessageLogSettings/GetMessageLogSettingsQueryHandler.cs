// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Queries.GetMessageLogSettings
{
    public class GetMessageLogSettingsQueryHandler : IRequestHandler<GetMessageLogSettingsQuery, GetMessageLogSettingsQueryResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext;

        public GetMessageLogSettingsQueryHandler(GrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetMessageLogSettingsQueryResponse> Handle(GetMessageLogSettingsQuery request, CancellationToken cancellationToken)
        {
            return await this._grimoireDbContext.GuildMessageLogSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new GetMessageLogSettingsQueryResponse
                {
                    EditChannelLog = x.EditChannelLogId,
                    DeleteChannelLog = x.DeleteChannelLogId,
                    BulkDeleteChannelLog = x.BulkDeleteChannelLogId,
                    IsLoggingEnabled = x.ModuleEnabled
                }).FirstAsync(cancellationToken: cancellationToken);
        }
    }
}
