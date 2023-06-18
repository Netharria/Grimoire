// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.SetUserLogSettings;

public class SetUserLogSettingsCommandHandler : ICommandHandler<SetUserLogSettingsCommand, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public SetUserLogSettingsCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<BaseResponse> Handle(SetUserLogSettingsCommand command, CancellationToken cancellationToken)
    {
        var userSettings = await this._grimoireDbContext.GuildUserLogSettings
            .Where(x => x.GuildId == command.GuildId)
            .Select(x => new
            {
                UserSettings = x,
                x.Guild.ModChannelLog
            }).FirstOrDefaultAsync(cancellationToken);
        if (userSettings == null) throw new AnticipatedException("Could not find user log settings.");
        switch (command.UserLogSetting)
        {
            case UserLogSetting.JoinLog:
                userSettings.UserSettings.JoinChannelLogId = command.ChannelId;
                break;
            case UserLogSetting.LeaveLog:
                userSettings.UserSettings.LeaveChannelLogId = command.ChannelId;
                break;
            case UserLogSetting.UsernameLog:
                userSettings.UserSettings.UsernameChannelLogId = command.ChannelId;
                break;
            case UserLogSetting.NicknameLog:
                userSettings.UserSettings.NicknameChannelLogId = command.ChannelId;
                break;
            case UserLogSetting.AvatarLog:
                userSettings.UserSettings.AvatarChannelLogId = command.ChannelId;
                break;
        }

        this._grimoireDbContext.GuildUserLogSettings.Update(userSettings.UserSettings);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new BaseResponse
        {
            LogChannelId = userSettings.ModChannelLog
        };
    }
}
