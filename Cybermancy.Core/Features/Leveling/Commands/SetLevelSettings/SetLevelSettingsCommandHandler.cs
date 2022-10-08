// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Commands.SetLevelSettings
{
    public class SetLevelSettingsCommandHandler : IRequestHandler<SetLevelSettingsCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public SetLevelSettingsCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<BaseResponse> Handle(SetLevelSettingsCommand request, CancellationToken cancellationToken)
        {
            var guild = await this._cybermancyDbContext.GuildLevelSettings.FirstOrDefaultAsync(x => x.GuildId == request.GuildId, cancellationToken: cancellationToken);
            if (guild == null) return new BaseResponse { Success = false, Message = "Could not find guild level settings." };
            switch (request.LevelSettings)
            {
                case LevelSettings.TextTime:
                    if (uint.TryParse(request.Value, out var textTime))
                        guild.TextTime = TimeSpan.FromMinutes(textTime);
                    else
                        return new BaseResponse { Success = false, Message = "Please give a valid number for TextTime." };
                    break;
                case LevelSettings.Base:
                    if (int.TryParse(request.Value, out var baseXp))
                        guild.Base = baseXp;
                    else
                        return new BaseResponse { Success = false, Message = "Please give a valid number for base XP." };
                    break;
                case LevelSettings.Modifier:
                    if (int.TryParse(request.Value, out var modifier))
                        guild.Modifier = modifier;
                    else
                        return new BaseResponse { Success = false, Message = "Please give a valid number for Modifier." };
                    break;
                case LevelSettings.Amount:
                    if (int.TryParse(request.Value, out var amout))
                        guild.Amount = amout;
                    else
                        return new BaseResponse { Success = false, Message = "Please give a valid number for Amount." };
                    break;
                case LevelSettings.LogChannel:
                    var parsedValue = Regex.Match(request.Value, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1)).Value;
                    var success = ulong.TryParse(parsedValue, out var value);
                    if (success || request.Value.Equals("0", StringComparison.OrdinalIgnoreCase))
                        guild.LevelChannelLogId = success ? value : null;
                    else
                        return new BaseResponse { Success = false, Message = "Please give a valid channel for Log Channel." };
                    break;
            }
            this._cybermancyDbContext.GuildLevelSettings.Update(guild);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { Success = true };
        }
    }
}
