// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.SharedDtos;

public sealed record UserDto
{
    public ulong Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? Nickname { get; init; }
    public string AvatarUrl { get; init; } = string.Empty;
}
