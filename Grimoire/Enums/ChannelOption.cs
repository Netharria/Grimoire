// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace Grimoire.Enums;

public enum ChannelOption
{
    [ChoiceDisplayName("Off")]Off,
    [ChoiceDisplayName("Current Channel")]CurrentChannel,
    [ChoiceDisplayName("Select Channel")]SelectChannel
}
