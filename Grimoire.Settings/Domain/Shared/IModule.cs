// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;

namespace Grimoire.Settings.Domain.Shared;

public interface IModule
{
    GuildId GuildId { get; init; }
    bool ModuleEnabled { get; set; }
}
