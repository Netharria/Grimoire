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

    internal static GuildSetting Create(
        GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        => new GuildSettingDefault(type, guildId, setBy, setAt);
}

internal sealed record GuildSettingDisabled : GuildSetting
{
    private GuildSettingDisabled(GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        : base(type, guildId, setBy, setAt)
    {
    }

    internal static GuildSetting Create(
        GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        => new GuildSettingDisabled(type, guildId, setBy, setAt);
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

    internal static Validation<Error, GuildSetting> Create(
        GuildSettingType type, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt, string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 200)
            return new Invalid("Value must be between 1 and 200 non-whitespace characters.");
        return Success<Error, GuildSetting>(
            new GuildSettingCustomValue(type, guildId, setBy, setAt, trimmed));
    }
}
