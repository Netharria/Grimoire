// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.SetLogSettings
{
    public class SetLoggingSettingsCommandHandler : IRequestHandler<SetLoggingSettingsCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public SetLoggingSettingsCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<BaseResponse> Handle(SetLoggingSettingsCommand request, CancellationToken cancellationToken)
        {
            var guild = await this._cybermancyDbContext.GuildLogSettings.FirstOrDefaultAsync(x => x.GuildId == request.GuildId, cancellationToken);
            if (guild == null) return new BaseResponse { Success = false, Message = "Could not find guild log settings.." };
            switch (request.LogSetting)
            {
                case LoggingSetting.JoinLog:
                    guild.JoinChannelLogId = request.ChannelId;
                    break;
                case LoggingSetting.LeaveLog:
                    guild.LeaveChannelLogId = request.ChannelId;
                    break;
                case LoggingSetting.DeleteLog:
                    guild.DeleteChannelLogId = request.ChannelId;
                    break;
                case LoggingSetting.BulkDeleteLog:
                    guild.BulkDeleteChannelLogId = request.ChannelId;
                    break;
                case LoggingSetting.EditLog:
                    guild.EditChannelLogId = request.ChannelId;
                    break;
                case LoggingSetting.UsernameLog:
                    guild.UsernameChannelLogId = request.ChannelId;
                    break;
                case LoggingSetting.NicknameLog:
                    guild.NicknameChannelLogId = request.ChannelId;
                    break;
                case LoggingSetting.AvatarLog:
                    guild.AvatarChannelLogId = request.ChannelId;
                    break;
            }
            this._cybermancyDbContext.GuildLogSettings.Update(guild);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { Success = true };
        }
    }
}
