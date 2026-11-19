// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public sealed record LeaderboardView
{
    public required GuildId GuildId { get; init; }
    public required UserId UserId { get; init; }
    public required long TotalXp { get; init; }
    public required long Rank { get; init; }

    /// <summary>The member's latest server display name in the guild, if any (TD-9). Rendered from 3.15 on.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The member's latest username, if any (TD-9). Rendered from 3.15 on.</summary>
    public string? Username { get; init; }
}
