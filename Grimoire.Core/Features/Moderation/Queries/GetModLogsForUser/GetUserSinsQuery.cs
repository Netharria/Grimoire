// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetModLogsForUser;

public enum SinQueryType
{
    Warn,
    Mute,
    Ban,
    All,
    Mod
}
public record GetUserSinsQuery : IQuery<GetUserSinsQueryResponse>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public SinQueryType SinQueryType { get; init; }
}
