// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public sealed class UserLogSettings : IModule
{
    public ChannelId? JoinChannelLogId { get; set; }
    public ChannelId? LeaveChannelLogId { get; set; }
    public ChannelId? UsernameChannelLogId { get; set; }
    public ChannelId? NicknameChannelLogId { get; set; }
    public ChannelId? AvatarChannelLogId { get; set; }
    public GuildId GuildId { get; init; }
    public bool ModuleEnabled { get; set; }
}
