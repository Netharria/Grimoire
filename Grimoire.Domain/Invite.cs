// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public sealed record Invite
{
    public string Code { get; init; } = string.Empty;
    public string Inviter { get; init; } = string.Empty;
    public int Uses { get; init; }
    public int MaxUses { get; init; }
    public string Url { get; init; } = string.Empty;
}
