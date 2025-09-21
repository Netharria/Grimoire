// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.SharedDtos;

public sealed record MemberDto
{
    public required ulong UserId { get; init; }
    public required ulong GuildId { get; init; }
    public string? Nickname { get; init; }
    public required string AvatarUrl { get; init; }
}
