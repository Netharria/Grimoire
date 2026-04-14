// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

internal abstract record GuildSetting
{
    internal required GuildSettingType Type { get; init; }
    internal required GuildId GuildId { get; init; }
    internal required ModeratorId SetBy { get; init; }
    internal required DateTimeOffset SetAt { get; init; }
}

internal sealed record GuildSettingDefault : GuildSetting;

internal sealed record GuildSettingDisabled : GuildSetting;

internal sealed record GuildSettingCustomValue : GuildSetting
{
    internal required string Value { get; init; }
}
