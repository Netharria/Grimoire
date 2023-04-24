// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Leveling.Commands.SetLevelSettings
{
    public class SetLevelSettingsCommandHandler : ICommandHandler<SetLevelSettingsCommand, BaseResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public SetLevelSettingsCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<BaseResponse> Handle(SetLevelSettingsCommand command, CancellationToken cancellationToken)
        {
            var levelSettings = await this._grimoireDbContext.GuildLevelSettings
                .Where(x => x.GuildId == command.GuildId)
                .Select(x => new
                {
                    LevelSettings = x,
                    x.Guild.ModChannelLog
                })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (levelSettings == null) throw new AnticipatedException("Could not find guild level settings.");
            switch (command.LevelSettings)
            {
                case LevelSettings.TextTime:
                    if (!uint.TryParse(command.Value, out var textTime))
                        throw new AnticipatedException("Please give a valid number for TextTime.");
                    levelSettings.LevelSettings.TextTime = TimeSpan.FromMinutes(textTime);
                    break;
                case LevelSettings.Base:
                    if (!int.TryParse(command.Value, out var baseXp))
                        throw new AnticipatedException("Please give a valid number for base XP.");
                    levelSettings.LevelSettings.Base = baseXp;
                    break;
                case LevelSettings.Modifier:
                    if (!int.TryParse(command.Value, out var modifier))
                        throw new AnticipatedException("Please give a valid number for Modifier.");
                    levelSettings.LevelSettings.Modifier = modifier;
                    break;
                case LevelSettings.Amount:
                    if (!int.TryParse(command.Value, out var amount))
                        throw new AnticipatedException("Please give a valid number for Amount.");
                    levelSettings.LevelSettings.Amount = amount;
                    break;
                case LevelSettings.LogChannel:
                    if(!ulong.TryParse(command.Value, out var value))
                        throw new AnticipatedException("Please give a valid channel for Log Channel.");
                    levelSettings.LevelSettings.LevelChannelLogId = value == 0 ? null : value;
                    break;
            }
            this._grimoireDbContext.GuildLevelSettings.Update(levelSettings.LevelSettings);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse
            {
                LogChannelId = levelSettings.ModChannelLog
            };
        }
    }
}
