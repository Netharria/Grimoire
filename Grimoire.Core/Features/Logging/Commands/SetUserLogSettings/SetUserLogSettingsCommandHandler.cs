// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.SetUserLogSettings
{
    public class SetUserLogSettingsCommandHandler : ICommandHandler<SetUserLogSettingsCommand, Unit>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public SetUserLogSettingsCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<Unit> Handle(SetUserLogSettingsCommand command, CancellationToken cancellationToken)
        {
            var guild = await this._grimoireDbContext.GuildUserLogSettings.FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
            if (guild == null) throw new AnticipatedException("Could not find server log settings..");
            switch (command.UserLogSetting)
            {
                case UserLogSetting.JoinLog:
                    guild.JoinChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.LeaveLog:
                    guild.LeaveChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.UsernameLog:
                    guild.UsernameChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.NicknameLog:
                    guild.NicknameChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.AvatarLog:
                    guild.AvatarChannelLogId = command.ChannelId;
                    break;
            }

            this._grimoireDbContext.GuildUserLogSettings.Update(guild);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new Unit();
        }
    }
}
