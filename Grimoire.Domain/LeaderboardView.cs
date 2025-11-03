// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public class LeaderboardView
{
    public required GuildId GuildId { get; init; }
    public required UserId UserId { get; init; }
    public required long TotalXp { get; init; }
    public required long Rank { get; init; }
}
