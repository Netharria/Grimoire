// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cybermancy.Core.Contracts.Persistance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Commands.SetLevelSettings
{
    public class SetLevelSettingsCommandHandler : IRequestHandler<SetLevelSettingsCommand, SetLevelSettingsCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public SetLevelSettingsCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<SetLevelSettingsCommandResponse> Handle(SetLevelSettingsCommand request, CancellationToken cancellationToken)
        {
            var guild = await this._cybermancyDbContext.GuildLevelSettings.FirstOrDefaultAsync(x => x.GuildId == request.GuildId, cancellationToken: cancellationToken);
            if (guild == null) return new SetLevelSettingsCommandResponse { Success = false, Message = "Could not find guild level settings.." };
            switch (request.LevelSettings)
            {
                case LevelSettings.TextTime:
                    if (uint.TryParse(request.Value, out var textTime))
                        guild.TextTime = textTime;
                    else
                        return new SetLevelSettingsCommandResponse { Success = false, Message = "Please give a valid number for TextTime." };
                    break;
                case LevelSettings.Base:
                    if (uint.TryParse(request.Value, out var baseXp))
                        guild.Base = baseXp;
                    else
                        return new SetLevelSettingsCommandResponse { Success = false, Message = "Please give a valid number for base XP." };
                    break;
                case LevelSettings.Modifier:
                    if (uint.TryParse(request.Value, out var modifier))
                        guild.Modifier = modifier;
                    else
                        return new SetLevelSettingsCommandResponse { Success = false, Message = "Please give a valid number for Modifier." };
                    break;
                case LevelSettings.Amount:
                    if (uint.TryParse(request.Value, out var amout))
                        guild.Amount = amout;
                    else
                        return new SetLevelSettingsCommandResponse { Success = false, Message = "Please give a valid number for Amount." };
                    break;
                case LevelSettings.LogChannel:
                    var parsedValue = Regex.Match(request.Value, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1)).Value;
                    guild.LevelChannelLogId = ulong.Parse(parsedValue);
                    break;
            }
            this._cybermancyDbContext.GuildLevelSettings.Update(guild);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new SetLevelSettingsCommandResponse { Success = true };
        }
    }
}
