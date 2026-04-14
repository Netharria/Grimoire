// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<SettingsResult> SetModuleState(
        Module moduleType,
        GuildId guildId,
        ModeratorId moderatorId,
        bool enableModule,
        CancellationToken cancellationToken = default)
    {
        if (moduleType == Module.General)
            return SettingsResult.Invalid("Cannot disable the general module.");
        if (moduleType.ToGuildSettingType() is not { } settingType)
            return SettingsResult.Invalid("Was not able to parse the module type.");
        if (enableModule)
            return await SetGuildSetting(
                new GuildSettingCustomValue
                {
                    Type = settingType,
                    GuildId = guildId,
                    SetBy = moderatorId,
                    SetAt = DateTimeOffset.UtcNow,
                    Value = bool.TrueString
                }, cancellationToken);
        return await SetGuildSetting(
            new GuildSettingDisabled
            {
                Type = settingType, GuildId = guildId, SetBy = moderatorId, SetAt = DateTimeOffset.UtcNow
            },
            cancellationToken);
    }

    private static bool ParseEnabled(CachedSetting? setting) =>
        setting is CachedCustomSetting { Value: var v }
        && bool.TryParse(v, out var enabled)
        && enabled;

    public async Task<bool> IsModuleEnabled(Module moduleType, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (moduleType == Module.General)
            return true;
        if (moduleType.ToGuildSettingType() is not { } settingType)
            return false;
        var result = await GetGuildSetting(settingType, guildId, cancellationToken);

        return ParseEnabled(result);
    }

    public async Task<GuildModuleState> GetAllModuleState(GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var levelingSettingType =
            await GetGuildSetting(GuildSettingType.LevelingModuleEnabled, guildId, cancellationToken);
        var userLogSetting = await GetGuildSetting(GuildSettingType.UserLogModuleEnabled, guildId, cancellationToken);
        var moderationSetting =
            await GetGuildSetting(GuildSettingType.ModerationModuleEnabled, guildId, cancellationToken);
        var messageLogSetting =
            await GetGuildSetting(GuildSettingType.MessageLogModuleEnabled, guildId, cancellationToken);
        var commandsSetting =
            await GetGuildSetting(GuildSettingType.CustomCommandsModuleEnabled, guildId, cancellationToken);
        var antiSpamSetting = await GetGuildSetting(GuildSettingType.AntiSpamModuleEnabled, guildId, cancellationToken);

        return new GuildModuleState
        {
            LevelingEnabled = ParseEnabled(levelingSettingType),
            UserLogEnabled = ParseEnabled(userLogSetting),
            ModerationEnabled = ParseEnabled(moderationSetting),
            MessageLogEnabled = ParseEnabled(messageLogSetting),
            CommandsEnabled = ParseEnabled(commandsSetting),
            AntiSpamEnabled = ParseEnabled(antiSpamSetting)
        };
    }

    public record GuildModuleState
    {
        public required bool LevelingEnabled { get; init; }
        public required bool UserLogEnabled { get; init; }
        public required bool ModerationEnabled { get; init; }
        public required bool MessageLogEnabled { get; init; }
        public required bool CommandsEnabled { get; init; }
        public required bool AntiSpamEnabled { get; init; }
    }
}
