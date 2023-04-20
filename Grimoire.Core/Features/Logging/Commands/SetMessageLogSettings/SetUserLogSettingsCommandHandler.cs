// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.SetMessageLogSettings
{
    public class SetMessageLogSettingsCommandHandler : ICommandHandler<SetMessageLogSettingsCommand, Unit>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public SetMessageLogSettingsCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<Unit> Handle(SetMessageLogSettingsCommand command, CancellationToken cancellationToken)
        {
            
            var guild = await this._grimoireDbContext.GuildMessageLogSettings.FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
            if (guild == null) throw new AnticipatedException("Could not find guild log settings..");
            switch(command.MessageLogSetting)
            {
                case MessageLogSetting.DeleteLog:
                    guild.DeleteChannelLogId = command.ChannelId;
                    break;
                case MessageLogSetting.BulkDeleteLog:
                    guild.BulkDeleteChannelLogId = command.ChannelId;
                    break;
                case MessageLogSetting.EditLog:
                    guild.EditChannelLogId = command.ChannelId;
                    break;
            }
            this._grimoireDbContext.GuildMessageLogSettings.Update(guild);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new Unit();
        }
    }
}
