// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public sealed class IgnoredRole: IRole, IIgnored
{
    public ulong Id
    {
        get => RoleId;
        init => RoleId = value;
    }
    public ulong RoleId { get; init; }
    public ulong GuildId { get; init; }
    public GuildSettings? GuildSettings { get; init; }
}
