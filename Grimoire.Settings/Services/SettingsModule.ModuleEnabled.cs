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
            _ => Result<GuildSettingType>.Fail(new Error("module.type.unrecognized",
                "Was not able to parse the module type."))
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
                true => await GuildSettingCustomValue.Create(settingType, guildId, moderatorId,
                        DateTimeOffset.UtcNow, bool.TrueString)
                    .ToResult()
                    .BindAsync(
                        setting => SetGuildSetting(setting, cancellationToken)),
                false => await GuildSettingDisabled.Create(settingType, guildId, moderatorId, DateTimeOffset.UtcNow)
                    .ToResult()
                    .BindAsync(
                        setting => SetGuildSetting(setting, cancellationToken))
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


    public Task<Result<GuildModuleState>> GetAllModuleState(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        return Result.WhenAll(
            GetSetting(GuildSettingType.LevelingModuleEnabled),
            GetSetting(GuildSettingType.UserLogModuleEnabled),
            GetSetting(GuildSettingType.ModerationModuleEnabled),
            GetSetting(GuildSettingType.MessageLogModuleEnabled),
            GetSetting(GuildSettingType.CustomCommandsModuleEnabled),
            GetSetting(GuildSettingType.AntiSpamModuleEnabled)
        ).Map(r => new GuildModuleState(
            ParseEnabled(r.Item1),
            ParseEnabled(r.Item2),
            ParseEnabled(r.Item3),
            ParseEnabled(r.Item4),
            ParseEnabled(r.Item5),
            ParseEnabled(r.Item6)
        ));

        Task<Result<CachedSetting>> GetSetting(GuildSettingType t) =>
            GetGuildSetting(t, guildId, cancellationToken)
                .AsTask();
    }

}
