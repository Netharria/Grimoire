// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Cybermancy.Core.Features.Leveling.Commands.SetLevelSettings
{
    public class SetLevelSettingsCommandHandler : ICommandHandler<SetLevelSettingsCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public SetLevelSettingsCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(SetLevelSettingsCommand command, CancellationToken cancellationToken)
        {
            var guild = await this._cybermancyDbContext.GuildLevelSettings.FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken: cancellationToken);
            if (guild == null) throw new AnticipatedException("Could not find guild level settings.");
            switch (command.LevelSettings)
            {
                case LevelSettings.TextTime:
                    if (uint.TryParse(command.Value, out var textTime))
                        guild.TextTime = TimeSpan.FromMinutes(textTime);
                    else
                        throw new AnticipatedException("Please give a valid number for TextTime.");
                    break;
                case LevelSettings.Base:
                    if (int.TryParse(command.Value, out var baseXp))
                        guild.Base = baseXp;
                    else
                        throw new AnticipatedException("Please give a valid number for base XP.");
                    break;
                case LevelSettings.Modifier:
                    if (int.TryParse(command.Value, out var modifier))
                        guild.Modifier = modifier;
                    else
                        throw new AnticipatedException("Please give a valid number for Modifier.");
                    break;
                case LevelSettings.Amount:
                    if (int.TryParse(command.Value, out var amout))
                        guild.Amount = amout;
                    else
                        throw new AnticipatedException("Please give a valid number for Amount.");
                    break;
                case LevelSettings.LogChannel:
                    var parsedValue = Regex.Match(command.Value, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1)).Value;
                    var success = ulong.TryParse(parsedValue, out var value);
                    if (success || command.Value.Equals("0", StringComparison.OrdinalIgnoreCase))
                        guild.LevelChannelLogId = success ? value : null;
                    else
                        throw new AnticipatedException("Please give a valid channel for Log Channel.");
                    break;
            }
            this._cybermancyDbContext.GuildLevelSettings.Update(guild);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new Unit();
        }
    }
}
