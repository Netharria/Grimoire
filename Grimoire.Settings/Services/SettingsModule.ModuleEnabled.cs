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
    private static Fin<GuildSettingType> ToSettingType(Module moduleType) =>
        // Only General has no setting of its own.
        moduleType.ToGuildSettingType() is { } settingType
            ? Fin.Succ<GuildSettingType>(settingType)
            : new Invalid("Cannot disable the general module.");

    public Task<Fin<Write<bool>>> SetModuleState(
        Module moduleType,
        GuildId guildId,
        ModeratorId moderatorId,
        bool enableModule,
        CancellationToken cancellationToken = default)
        => ToSettingType(moduleType)
            .BindAsync(settingType => ApplyGuildSetting(enableModule
                    ? GuildSettingCustomValue.Create(settingType, guildId, moderatorId, DateTimeOffset.UtcNow,
                        bool.TrueString)
                    : Success<Error, GuildSetting>(
                        GuildSettingDisabled.Create(settingType, guildId, moderatorId, DateTimeOffset.UtcNow)),
                cancellationToken))
            .MapFin(write => write.Map(_ => enableModule));

    private static bool ParseEnabled(CachedSetting? setting) =>
        setting is CachedCustomSetting { Value: var v }
        && bool.TryParse(v, out var enabled)
        && enabled;

    public Task<Fin<bool>> IsModuleEnabled(
        Module moduleType,
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => moduleType == Module.General
            ? Task.FromResult(Fin.Succ<bool>(true))
            : ToSettingType(moduleType)
                .BindAsync(settingType => GetGuildSetting(settingType, guildId, cancellationToken))
                .MapFin(ParseEnabled);


    /// <summary>
    ///     Reads the six module settings together. A failure is the first one: they're all reads of the same cache and
    ///     database, so their failures are the same outage (spec 1.1).
    /// </summary>
    public async Task<Fin<GuildModuleState>> GetAllModuleState(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var leveling = GetSetting(GuildSettingType.LevelingModuleEnabled);
        var userLog = GetSetting(GuildSettingType.UserLogModuleEnabled);
        var moderation = GetSetting(GuildSettingType.ModerationModuleEnabled);
        var messageLog = GetSetting(GuildSettingType.MessageLogModuleEnabled);
        var commands = GetSetting(GuildSettingType.CustomCommandsModuleEnabled);
        var antiSpam = GetSetting(GuildSettingType.AntiSpamModuleEnabled);
        await Task.WhenAll(leveling, userLog, moderation, messageLog, commands, antiSpam);

        return from l in leveling.Result
            from u in userLog.Result
            from m in moderation.Result
            from ml in messageLog.Result
            from c in commands.Result
            from a in antiSpam.Result
            select new GuildModuleState(
                ParseEnabled(l), ParseEnabled(u), ParseEnabled(m), ParseEnabled(ml), ParseEnabled(c), ParseEnabled(a));

        Task<Fin<CachedSetting>> GetSetting(GuildSettingType t) =>
            GetGuildSetting(t, guildId, cancellationToken);
    }
}
