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
    private static Result<GuildSettingType> ToSettingType(Module moduleType) =>
        moduleType switch
        {
            Module.General => Result<GuildSettingType>.Fail(
                new Error("module.general.immutable", "Cannot disable the general module.")),
            _ when moduleType.ToGuildSettingType() is { } settingType => Result<GuildSettingType>.Ok(settingType),
            _ => Result<GuildSettingType>.Fail(new Error("module.type.unrecognized", "Was not able to parse the module type."))
        };

    public Task<Result<bool>> SetModuleState(
        Module moduleType,
        GuildId guildId,
        ModeratorId moderatorId,
        bool enableModule,
        CancellationToken cancellationToken = default)
    => ToSettingType(moduleType)
        .BindAsync(async settingType => enableModule switch
        {
            true => await SetGuildSetting(
                new GuildSettingCustomValue(settingType, guildId, moderatorId, DateTimeOffset.UtcNow, bool.TrueString),
                cancellationToken),
            false => await SetGuildSetting(
                new GuildSettingDisabled(settingType, guildId, moderatorId, DateTimeOffset.UtcNow),
                cancellationToken)
        }).Map(_ => enableModule);

    private static bool ParseEnabled(CachedSetting? setting) =>
        setting is CachedCustomSetting { Value: var v }
        && bool.TryParse(v, out var enabled)
        && enabled;

    public Task<Result<bool>> IsModuleEnabled(
        Module moduleType,
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => moduleType == Module.General
            ? Task.FromResult(Result<bool>.Ok(true))
            : ToSettingType(moduleType)
                .BindAsync(settingType => GetGuildSetting(settingType, guildId, cancellationToken).AsTask())
                .Map(ParseEnabled);

    public async Task<Result<GuildModuleState>> GetAllModuleState(GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var levelingSettingType = GetGuildSetting(GuildSettingType.LevelingModuleEnabled, guildId, cancellationToken).AsTask();
        var userLogSetting = GetGuildSetting(GuildSettingType.UserLogModuleEnabled, guildId, cancellationToken).AsTask();
        var moderationSetting =
            GetGuildSetting(GuildSettingType.ModerationModuleEnabled, guildId, cancellationToken).AsTask();
        var messageLogSetting =
            GetGuildSetting(GuildSettingType.MessageLogModuleEnabled, guildId, cancellationToken).AsTask();
        var commandsSetting =
            GetGuildSetting(GuildSettingType.CustomCommandsModuleEnabled, guildId, cancellationToken).AsTask();
        var antiSpamSetting = GetGuildSetting(GuildSettingType.AntiSpamModuleEnabled, guildId, cancellationToken).AsTask();

        await Task.WhenAll(levelingSettingType, userLogSetting, moderationSetting, messageLogSetting, commandsSetting, antiSpamSetting);



        return Result<GuildModuleState>.Ok(new GuildModuleState
        (
            levelingSettingType.Result.Map(ParseEnabled).OrElse(false),
            userLogSetting.Result.Map(ParseEnabled).OrElse(false),
            moderationSetting.Result.Map(ParseEnabled).OrElse(false),
            messageLogSetting.Result.Map(ParseEnabled).OrElse(false),
            commandsSetting.Result.Map(ParseEnabled).OrElse(false),
            antiSpamSetting.Result.Map(ParseEnabled).OrElse(false)
        ));
    }

    public record GuildModuleState(
        bool LevelingEnabled,
        bool UserLogEnabled,
        bool ModerationEnabled,
        bool MessageLogEnabled,
        bool CommandsEnabled,
        bool AntiSpamEnabled);
}
