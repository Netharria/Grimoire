// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Domain;

internal abstract record GuildSetting(
    GuildSettingType Type,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt);

internal sealed record GuildSettingDefault : GuildSetting
{
    private GuildSettingDefault(GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        : base(type, guildId, setBy, setAt)
    {
    }

    internal static Validation<GuildSetting> Create(
        GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (guildId.Value == 0)
            return Validation<GuildSetting>.Fail(
                new Error("guild-setting.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<GuildSetting>.Fail(
                new Error("guild-setting.set-by.invalid", "ModeratorId must be specified."));
        return Validation<GuildSetting>.Succeed(new GuildSettingDefault(type, guildId, setBy, setAt));
    }
}

internal sealed record GuildSettingDisabled : GuildSetting
{
    private GuildSettingDisabled(GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        : base(type, guildId, setBy, setAt)
    {
    }

    internal static Validation<GuildSetting> Create(
        GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (guildId.Value == 0)
            return Validation<GuildSetting>.Fail(
                new Error("guild-setting.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<GuildSetting>.Fail(
                new Error("guild-setting.set-by.invalid", "ModeratorId must be specified."));
        return Validation<GuildSetting>.Succeed(new GuildSettingDisabled(type, guildId, setBy, setAt));
    }
}

internal sealed record GuildSettingCustomValue : GuildSetting
{
    private GuildSettingCustomValue(
        GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt, string value)
        : base(type, guildId, setBy, setAt)
    {
        Value = value;
    }

    public string Value { get; }

    internal static Validation<GuildSetting> Create(
        GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt, string? value)
    {
        if (guildId.Value == 0)
            return Validation<GuildSetting>.Fail(
                new Error("guild-setting.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<GuildSetting>.Fail(
                new Error("guild-setting.set-by.invalid", "ModeratorId must be specified."));
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 200)
            return Validation<GuildSetting>.Fail(
                new Error("guild-setting.value.invalid", "Value must be between 1 and 200 non-whitespace characters."));
        return Validation<GuildSetting>.Succeed(
            new GuildSettingCustomValue(type, guildId, setBy, setAt, trimmed));
    }
}
