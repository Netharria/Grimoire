// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class Pardon
{
    public SinId SinId { get; init; }

    public Sin? Sin { get; init; }

    public required ModeratorId ModeratorId { get; init; }

    public required GuildId GuildId { get; init; }
    public DateTimeOffset PardonDate { get; init; }

    public required string Reason { get; set; }
}

