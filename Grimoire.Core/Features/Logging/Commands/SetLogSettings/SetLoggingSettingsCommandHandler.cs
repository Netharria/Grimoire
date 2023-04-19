// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.SetLogSettings
{
    public class SetLoggingSettingsCommandHandler : ICommandHandler<SetLoggingSettingsCommand, Unit>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public SetLoggingSettingsCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<Unit> Handle(SetLoggingSettingsCommand command, CancellationToken cancellationToken)
        {
            if(command.LogSetting == LoggingSetting.DeleteLog
                && command.LogSetting == LoggingSetting.BulkDeleteLog
                && command.LogSetting == LoggingSetting.EditLog)
            {
                var guild = await this._grimoireDbContext.GuildMessageLogSettings.FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
                if (guild == null) throw new AnticipatedException("Could not find guild log settings..");
                switch(command.LogSetting)
                {
                    case LoggingSetting.DeleteLog:
                        guild.DeleteChannelLogId = command.ChannelId;
                        break;
                    case LoggingSetting.BulkDeleteLog:
                        guild.BulkDeleteChannelLogId = command.ChannelId;
                        break;
                    case LoggingSetting.EditLog:
                        guild.EditChannelLogId = command.ChannelId;
                        break;
                }
                this._grimoireDbContext.GuildMessageLogSettings.Update(guild);
            }
            else
            {
                var guild = await this._grimoireDbContext.GuildUserLogSettings.FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
                if (guild == null) throw new AnticipatedException("Could not find guild log settings..");
                switch (command.LogSetting)
                {
                    case LoggingSetting.JoinLog:
                        guild.JoinChannelLogId = command.ChannelId;
                        break;
                    case LoggingSetting.LeaveLog:
                        guild.LeaveChannelLogId = command.ChannelId;
                        break;
                    case LoggingSetting.DeleteLog:
                    case LoggingSetting.UsernameLog:
                        guild.UsernameChannelLogId = command.ChannelId;
                        break;
                    case LoggingSetting.NicknameLog:
                        guild.NicknameChannelLogId = command.ChannelId;
                        break;
                    case LoggingSetting.AvatarLog:
                        guild.AvatarChannelLogId = command.ChannelId;
                        break;
                }
                this._grimoireDbContext.GuildUserLogSettings.Update(guild);
            }
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new Unit();
        }
    }
}
