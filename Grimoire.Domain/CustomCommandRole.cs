// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;
public class CustomCommandRole
{
    public ulong RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string CustomCommandName { get; set; } = string.Empty;
    public CustomCommand CustomCommand { get; set; } = null!;
    public ulong GuildId { get; set; }
    public Guild Guild { get; set; } = null!;
}
