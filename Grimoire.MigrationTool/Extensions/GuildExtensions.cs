// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Grimoire.MigrationTool.Domain.Anubis;
using Grimoire.MigrationTool.Domain.Fuzzy;
using Grimoire.MigrationTool.Domain.Lumberjack;

namespace Grimoire.MigrationTool.Extensions;

public static class GuildExtensions
{
    public static void UpdateGuildLogSettings(this Guild guild, LogChannelSettings logChannelSettings)
    {
        guild.UserLogSettings.JoinChannelLogId =
            logChannelSettings.JoinLogId != 0
            ? logChannelSettings.JoinLogId
            : null;
        guild.AddChannel(logChannelSettings.JoinLogId);
        guild.UserLogSettings.LeaveChannelLogId =
            logChannelSettings.LeaveLogId != 0
            ? logChannelSettings.LeaveLogId
            : null;
        guild.AddChannel(logChannelSettings.LeaveLogId);
        guild.UserLogSettings.UsernameChannelLogId =
            logChannelSettings.UsernameLogId != 0
            ? logChannelSettings.UsernameLogId
            : null;
        guild.AddChannel(logChannelSettings.UsernameLogId);
        guild.UserLogSettings.NicknameChannelLogId =
            logChannelSettings.NicknameLogId != 0
            ? logChannelSettings.NicknameLogId
            : null;
        guild.AddChannel(logChannelSettings.NicknameLogId);
        guild.UserLogSettings.AvatarChannelLogId =
            logChannelSettings.AvatarLogId != 0
            ? logChannelSettings.AvatarLogId
            : null;
        guild.AddChannel(logChannelSettings.AvatarLogId);
        guild.UserLogSettings.ModuleEnabled = true;

        guild.MessageLogSettings.DeleteChannelLogId =
            logChannelSettings.DeleteLogId != 0
            ? logChannelSettings.DeleteLogId
            : null;
        guild.AddChannel(logChannelSettings.DeleteLogId);
        guild.MessageLogSettings.BulkDeleteChannelLogId =
            logChannelSettings.BulkDeleteLogId != 0
            ? logChannelSettings.BulkDeleteLogId
            : null;
        guild.AddChannel(logChannelSettings.BulkDeleteLogId);
        guild.MessageLogSettings.EditChannelLogId =
            logChannelSettings.EditLogId != 0
            ? logChannelSettings.EditLogId
            : null;
        guild.AddChannel(logChannelSettings.EditLogId);
        guild.MessageLogSettings.ModuleEnabled = true;

        guild.ModChannelLog = logChannelSettings.ModLogId;
        guild.AddChannel(logChannelSettings.ModLogId);
    }

    private static void AddChannel(this Guild guild, ulong? channelId)
    {
        if (channelId is not null && channelId != 0 && !guild.Channels.Any(x => x.Id == channelId))
        {
            guild.Channels.Add(new Channel
            {
                Id = channelId.Value,
                GuildId = guild.Id
            });
        }
    }

    private static void AddRole(this Guild guild, ulong? roleId)
    {
        if (roleId is not null && roleId != 0 && !guild.Roles.Any(x => x.Id == roleId))
        {
            guild.Roles.Add(new Role
            {
                Id = roleId.Value,
                GuildId = guild.Id
            });
        }
    }

    public static void UpdateGuildLevelSettings(this Guild guild, LevelSettings levelSettings)
    {
        guild.LevelSettings.LevelChannelLogId =
            levelSettings.LevelLog != 0
            ? levelSettings.LevelLog
            : null;
        guild.AddChannel(levelSettings.LevelLog);

        guild.LevelSettings.Amount = levelSettings.Amount;
        guild.LevelSettings.Base = levelSettings.Base;
        guild.LevelSettings.Modifier = levelSettings.Modifier;
        guild.LevelSettings.TextTime = TimeSpan.FromMinutes(levelSettings.TextTime);

        guild.LevelSettings.ModuleEnabled = true;
    }

    public static void UpdateGuildModerationSettings(this Guild guild, ModerationSettings moderationSettings)
    {
        guild.ModChannelLog = moderationSettings.ModerationLog;
        guild.AddChannel(moderationSettings.ModerationLog);

        guild.ModerationSettings.PublicBanLog = moderationSettings.PublicBanLog;
        guild.AddChannel(moderationSettings.PublicBanLog);

        guild.ModerationSettings.DurationType = moderationSettings.DurationType switch
        {
            1 => Duration.Days,
            2 => Duration.Months,
            3 => Duration.Years,
            _ => Duration.Years
        };

        guild.ModerationSettings.Duration = moderationSettings.Duration;
        guild.ModerationSettings.MuteRole = moderationSettings.MuteRole;
        guild.AddRole(moderationSettings.MuteRole);

        guild.ModerationSettings.ModuleEnabled = true;
    }
}
