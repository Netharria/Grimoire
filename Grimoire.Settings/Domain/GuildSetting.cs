// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

internal abstract record GuildSetting
{
    protected GuildSetting(GuildSetting original)
    {
        Type = original.Type;
        GuildId = original.GuildId;
        SetBy = original.SetBy;
        SetAt = DateTimeOffset.UtcNow;
    }

    internal required GuildSettingType Type { get; init; }
    internal required GuildId GuildId { get; init; }
    internal required ModeratorId SetBy { get; init; }
    internal DateTimeOffset SetAt { get; } = DateTimeOffset.UtcNow;
}

internal record GuildSettingDefault : GuildSetting;

internal record GuildSettingDisabled : GuildSetting;

internal record GuildSettingCustomValue : GuildSetting
{
    public GuildSettingCustomValue(GuildSettingCustomValue original) : base(original)
    {
        Value = original.Value;
    }

    internal required string Value { get; init; }
}

internal enum GuildSettingState
{
    Default,
    Disabled,
    CustomValue
}
