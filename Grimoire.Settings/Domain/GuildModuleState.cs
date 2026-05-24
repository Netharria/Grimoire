// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public sealed record GuildModuleState(
    bool LevelingEnabled,
    bool UserLogEnabled,
    bool ModerationEnabled,
    bool MessageLogEnabled,
    bool CommandsEnabled,
    bool AntiSpamEnabled);
