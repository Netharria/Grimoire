// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Responses;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Moderation.Commands.MuteCommands.SetMuteRole
{
    public class SetMuteRoleCommandHandler : ICommandHandler<SetMuteRoleCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public SetMuteRoleCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<BaseResponse> Handle(SetMuteRoleCommand request, CancellationToken cancellationToken)
        {
            var guildModerationSettings = await this._cybermancyDbContext.GuildModerationSettings
                .FirstOrDefaultAsync(x => x.GuildId == request.GuildId, cancellationToken);
            if (guildModerationSettings is null) return new BaseResponse { Success = false, Message = "Could not find the Servers settings." };

            guildModerationSettings.MuteRole = request.Role;
            this._cybermancyDbContext.GuildModerationSettings.Update(guildModerationSettings);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse { Success = true };
        }
    }
}
