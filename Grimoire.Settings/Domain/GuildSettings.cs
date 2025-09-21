// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Grimoire.Settings.Domain;

public sealed class GuildSettings
{
    public required ulong Id { get; init; }
    public ulong? ModLogChannelId { get; set; }
    public ulong? UserCommandChannelId { get; set; }
    public ISet<Tracker> Trackers { get; } = new ObservableHashSet<Tracker>();
    public ISet<Reward> Rewards { get; } = new ObservableHashSet<Reward>();

    public ISet<Mute> ActiveMutes { get; } = new ObservableHashSet<Mute>();
    public ISet<Lock> LockedChannels { get; } = new ObservableHashSet<Lock>();
    public ISet<IgnoredChannel> IgnoredChannels { get; } = new ObservableHashSet<IgnoredChannel>();
    public ISet<IgnoredMember> IgnoredMembers { get; } = new ObservableHashSet<IgnoredMember>();
    public ISet<IgnoredRole> IgnoredRoles { get; } = new ObservableHashSet<IgnoredRole>();
    public ISet<MessageLogChannelOverride> MessageLogChannelOverrides { get; } = new ObservableHashSet<MessageLogChannelOverride>();
    public ISet<SpamFilterOverride> SpamFilterOverrides { get; } = new ObservableHashSet<SpamFilterOverride>();
    public GuildUserLogSettings UserLogSettings { get; } = new ();
    public GuildLevelSettings LevelSettings { get; } = new ();
    public GuildModerationSettings ModerationSettings { get; } = new ();
    public GuildMessageLogSettings MessageLogSettings { get; } = new ();
    public GuildCommandsSettings CommandsSettings { get; } = new ();

}
