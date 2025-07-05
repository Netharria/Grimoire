// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class GuildUserLogSettings : IModule
{
    public ulong GuildId { get; init; }

    public ulong? JoinChannelLogId { get; set; }

    public virtual Channel? JoinChannelLog { get; init; }

    public ulong? LeaveChannelLogId { get; set; }

    public virtual Channel? LeaveChannelLog { get; init; }

    public ulong? UsernameChannelLogId { get; set; }

    public virtual Channel? UsernameChannelLog { get; init; }

    public ulong? NicknameChannelLogId { get; set; }

    public virtual Channel? NicknameChannelLog { get; init; }

    public ulong? AvatarChannelLogId { get; set; }

    public virtual Channel? AvatarChannelLog { get; init; }

    public virtual Guild Guild { get; set; } = null!;
    public bool ModuleEnabled { get; set; }
}
