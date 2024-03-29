// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class Guild : IIdentifiable<ulong>
{
    public ulong Id { get; set; }

    public ulong? ModChannelLog { get; set; }

    public virtual Channel? ModLogChannel { get; set; }

    public ulong? UserCommandChannelId { get; set; }

    public virtual Channel? UserCommandChannel { get; set; }

    public virtual ICollection<Channel> Channels { get; set; } = new List<Channel>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<MessageHistory> MessageHistory { get; set; } = new List<MessageHistory>();

    public virtual ICollection<OldLogMessage> OldLogMessages { get; set; } = new List<OldLogMessage>();

    public virtual ICollection<Tracker> Trackers { get; set; } = new List<Tracker>();

    public virtual ICollection<Reward> Rewards { get; set; } = new List<Reward>();

    public virtual ICollection<Member> Members { get; set; } = new List<Member>();

    public virtual ICollection<Sin> Sins { get; set; } = new List<Sin>();

    public virtual ICollection<Mute> ActiveMutes { get; set; } = new List<Mute>();

    public virtual ICollection<Lock> LockedChannels { get; set; } = new List<Lock>();

    public virtual ICollection<XpHistory> XpHistory { get; set; } = new List<XpHistory>();

    public virtual ICollection<IgnoredChannel> IgnoredChannels { get; set; } = new List<IgnoredChannel>();

    public virtual ICollection<IgnoredMember> IgnoredMembers { get; set; } = new List<IgnoredMember>();

    public virtual ICollection<IgnoredRole> IgnoredRoles { get; set; } = new List<IgnoredRole>();

    public virtual ICollection<MessageLogChannelOverride> MessageLogChannelOverrides { get; set; } = new List<MessageLogChannelOverride>();

    public virtual GuildUserLogSettings UserLogSettings { get; set; } = null!;

    public virtual GuildLevelSettings LevelSettings { get; set; } = null!;

    public virtual GuildModerationSettings ModerationSettings { get; set; } = null!;

    public virtual GuildMessageLogSettings MessageLogSettings { get; set; } = null!;
}
