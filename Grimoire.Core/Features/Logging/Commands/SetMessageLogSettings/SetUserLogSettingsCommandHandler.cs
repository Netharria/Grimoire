// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.SetMessageLogSettings;

public class SetMessageLogSettingsCommandHandler : ICommandHandler<SetMessageLogSettingsCommand, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public SetMessageLogSettingsCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<BaseResponse> Handle(SetMessageLogSettingsCommand command, CancellationToken cancellationToken)
    {

        var messageSettings = await this._grimoireDbContext.GuildMessageLogSettings
            .Where(x => x.GuildId == command.GuildId)
            .Select(x => new
            {
                LogSettings = x,
                x.Guild.ModChannelLog
            }).FirstOrDefaultAsync(cancellationToken);
        if (messageSettings == null) throw new AnticipatedException("Could not find message log settings.");
        switch (command.MessageLogSetting)
        {
            case MessageLogSetting.DeleteLog:
                messageSettings.LogSettings.DeleteChannelLogId = command.ChannelId;
                break;
            case MessageLogSetting.BulkDeleteLog:
                messageSettings.LogSettings.BulkDeleteChannelLogId = command.ChannelId;
                break;
            case MessageLogSetting.EditLog:
                messageSettings.LogSettings.EditChannelLogId = command.ChannelId;
                break;
        }
        this._grimoireDbContext.GuildMessageLogSettings.Update(messageSettings.LogSettings);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new BaseResponse { LogChannelId = messageSettings.ModChannelLog };
    }
}
