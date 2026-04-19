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

internal sealed record GuildSettingDefault(
    GuildSettingType Type,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt)
    : GuildSetting(Type, GuildId, SetBy, SetAt);

internal sealed record GuildSettingDisabled(
    GuildSettingType Type,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt)
    : GuildSetting(Type, GuildId, SetBy, SetAt);

internal sealed record GuildSettingCustomValue(
    GuildSettingType Type,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt,
    string Value)
    : GuildSetting(Type, GuildId, SetBy, SetAt);
