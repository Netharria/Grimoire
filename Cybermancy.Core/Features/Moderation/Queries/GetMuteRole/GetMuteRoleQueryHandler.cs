// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Moderation.Queries.GetMuteRole
{
    public class GetMuteRoleQueryHandler : IRequestHandler<GetMuteRoleQuery, GetMuteRoleQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetMuteRoleQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GetMuteRoleQueryResponse> Handle(GetMuteRoleQuery request, CancellationToken cancellationToken)
        {
            var muteRoleId = await this._cybermancyDbContext.GuildModerationSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => x.MuteRole)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (muteRoleId is null) throw new AnticipatedException("No mute role is configured.");
            return new GetMuteRoleQueryResponse { RoleId = muteRoleId.Value };
        }
    }
}
