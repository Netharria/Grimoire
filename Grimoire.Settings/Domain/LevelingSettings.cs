// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public sealed class LevelingSettings : IModule
{
    public TimeSpan TextTime { get; set; }
    public LevelScalingBase Base { get; set; }
    public LevelScalingModifier Modifier { get; set; }
    public XpGainAmount Amount { get; set; }
    public ChannelId? LevelChannelLogId { get; set; }
    public GuildId GuildId { get; init; }
    public bool ModuleEnabled { get; set; }
}

public readonly record struct LevelScalingBase(int Value)
{
    public static implicit operator int(LevelScalingBase value) => value.Value;
}
public readonly record struct LevelScalingModifier(int Value)
{
    public static implicit operator int(LevelScalingModifier value) => value.Value;
}
public readonly record struct XpGainAmount(int Value)
{
    public static implicit operator int(XpGainAmount value) => value.Value;
}
