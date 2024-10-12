// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.UserLogging.Commands;

public sealed record SetUserLogSettingsCommand : ICommand<BaseResponse>
{
    public ulong GuildId { get; init; }
    public UserLogSetting UserLogSetting { get; init; }
    public ulong? ChannelId { get; init; }
}
public enum UserLogSetting
{
    JoinLog,
    LeaveLog,
    UsernameLog,
    NicknameLog,
    AvatarLog
}


public sealed class SetUserLogSettingsCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<SetUserLogSettingsCommand, BaseResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

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

        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new BaseResponse
        {
            LogChannelId = userSettings.ModChannelLog
        };
    }
}
