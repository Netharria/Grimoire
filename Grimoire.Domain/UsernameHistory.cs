// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public class UsernameHistory
{
    public ulong UserId { get; set; }
    public virtual User User { get; set; } = null!;
    public string Username { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
