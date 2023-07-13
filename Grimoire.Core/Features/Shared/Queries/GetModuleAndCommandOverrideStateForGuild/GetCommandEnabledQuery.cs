// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Enums;
using Grimoire.Domain.Enums;

namespace Grimoire.Core.Features.Shared.Queries.GetModuleAndCommandOverrideStateForGuild;
public sealed record GetCommandEnabledQuery : IQuery<bool>
{
    public required ulong GuildId { get; init; }
    public required ulong ChannelId { get; init; }
    public required ulong[] RoleIds { get; init; }
    public required CommandPermissions Permissions { get; init; }
    public required ulong UserId { get; init; }
}
