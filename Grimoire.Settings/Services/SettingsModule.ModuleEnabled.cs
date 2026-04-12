// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private static readonly List<GuildSettingType> _moduleSettingKeys =
    [
        GuildSettingType.CustomCommandsModuleEnabled,
        GuildSettingType.LevelingModuleEnabled,
        GuildSettingType.MessageLogModuleEnabled,
        GuildSettingType.ModerationModuleEnabled,
        GuildSettingType.UserLogModuleEnabled
    ];

    public async Task SetModuleState(
        Module moduleType,
        GuildId guildId,
        ModeratorId moderatorId,
        bool enableModule,
        CancellationToken cancellationToken = default)
    {
        if (moduleType == Module.General)
            return;
        var guildSettingType = moduleType.ToGuildSettingType();
        if (guildSettingType is not { } settingType)
            return;
        if (enableModule)
        {
            await SetGuildSetting(
                new GuildSettingCustomValue
                {
                    Type = settingType, GuildId = guildId, SetBy = moderatorId, Value = bool.TrueString
                }, cancellationToken);
            return;
        }

        await SetGuildSetting(
            new GuildSettingDisabled { Type = settingType, GuildId = guildId, SetBy = moderatorId },
            cancellationToken);
    }

    public async Task<bool> IsModuleEnabled(Module moduleType, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (moduleType == Module.General)
            return true;
        var guildSettingType = moduleType.ToGuildSettingType();
        if (guildSettingType is not { } settingType)
            return false;
        var result = await GetGuildSetting(settingType, guildId, cancellationToken);

        if (result is not CachedCustomSetting customValue)
            return false;
        return bool.TryParse(customValue.Value, out var isEnabled) && isEnabled;
    }

    public async Task<GuildModuleState> GetAllModuleState(GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var latestByKey =
            await GetGuildSettings(guildId, _moduleSettingKeys, cancellationToken)
                .ToDictionaryAsync(x => x.Type, x => x, cancellationToken: cancellationToken);

        var levelingSettingType = latestByKey.GetValueOrDefault(GuildSettingType.LevelingModuleEnabled);
        var userLogSetting = latestByKey.GetValueOrDefault(GuildSettingType.UserLogModuleEnabled);
        var moderationSetting = latestByKey.GetValueOrDefault(GuildSettingType.ModerationModuleEnabled);
        var messageLogSetting = latestByKey.GetValueOrDefault(GuildSettingType.MessageLogModuleEnabled);
        var commandsSetting = latestByKey.GetValueOrDefault(GuildSettingType.CustomCommandsModuleEnabled);

        return new GuildModuleState
        {
            LevelingEnabled = levelingSettingType is GuildSettingCustomValue levelingCustomValue
                              && bool.TryParse(levelingCustomValue.Value, out var levelingEnabled)
                              && levelingEnabled,
            LevelingModuleSetBy = levelingSettingType?.SetBy,
            UserLogEnabled = userLogSetting is GuildSettingCustomValue userLogCustomValue
                             && bool.TryParse(userLogCustomValue.Value, out var userLogEnabled)
                             && userLogEnabled,
            UserLogModuleSetBy = userLogSetting?.SetBy,
            ModerationEnabled = moderationSetting is GuildSettingCustomValue moderationCustomValue
                                && bool.TryParse(moderationCustomValue.Value, out var moderationEnabled)
                                && moderationEnabled,
            ModerationModuleSetBy = moderationSetting?.SetBy,
            MessageLogEnabled = messageLogSetting is GuildSettingCustomValue messageLogCustomValue
                                && bool.TryParse(messageLogCustomValue.Value, out var messageLogEnabled)
                                && messageLogEnabled,
            MessageLogModuleSetBy = messageLogSetting?.SetBy,
            CommandsEnabled = commandsSetting is GuildSettingCustomValue commandsCustomValue
                              && bool.TryParse(commandsCustomValue.Value, out var commandsEnabled)
                              && commandsEnabled,
            CommandsModuleSetBy = commandsSetting?.SetBy
        };
    }

    public record GuildModuleState
    {
        public required bool LevelingEnabled { get; init; }
        public ModeratorId? LevelingModuleSetBy { get; init; }
        public required bool UserLogEnabled { get; init; }
        public ModeratorId? UserLogModuleSetBy { get; init; }
        public required bool ModerationEnabled { get; init; }
        public ModeratorId? ModerationModuleSetBy { get; init; }
        public required bool MessageLogEnabled { get; init; }
        public ModeratorId? MessageLogModuleSetBy { get; init; }
        public required bool CommandsEnabled { get; init; }
        public ModeratorId? CommandsModuleSetBy { get; init; }
    }
}
