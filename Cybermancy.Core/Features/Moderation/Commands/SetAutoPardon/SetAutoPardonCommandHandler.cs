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

namespace Cybermancy.Core.Features.Moderation.Commands.SetAutoPardon
{
    public class SetAutoPardonCommandHandler : ICommandHandler<SetAutoPardonCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public SetAutoPardonCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<BaseResponse> Handle(SetAutoPardonCommand request, CancellationToken cancellationToken)
        {
            if(request.DurationAmount > int.MaxValue)
                return new BaseResponse { Success = false, Message = "The number provided is too large" };
            if (request.DurationAmount < 0)
                return new BaseResponse { Success = false, Message = "Only positive numbers are allowed." };


            var guildModerationSettings = await this._cybermancyDbContext.GuildModerationSettings
                .FirstOrDefaultAsync(x => x.GuildId.Equals(request.GuildId), cancellationToken: cancellationToken);
            if (guildModerationSettings is null) return new BaseResponse { Success = false, Message = "Could not find the Servers settings." };

            guildModerationSettings.DurationType = request.DurationType;
            guildModerationSettings.Duration = (int)request.DurationAmount;
            this._cybermancyDbContext.GuildModerationSettings.Update(guildModerationSettings);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse { Success = true };
        }
    }
}
