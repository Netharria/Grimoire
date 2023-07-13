// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Enums;

namespace Grimoire.Domain;
public class RoleCommandOverride
{
    public long Id { get; set; }
    public required ulong RoleId { get; set; }
    public virtual Role Role { get; set; } = null!;
    public required ulong GuildId { get; set; }
    public virtual Guild Guild { get; set; } = null!;
    public required CommandPermissions CommandPermissions { get; set; }
    public ulong? ChannelId { get; set; }
    public virtual Channel? Channel { get; set; }
}
