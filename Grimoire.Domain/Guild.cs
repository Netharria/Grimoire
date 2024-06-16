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

    public virtual ICollection<Channel> Channels { get; set; } = [];

    public virtual ICollection<Role> Roles { get; set; } = [];

    public virtual ICollection<Message> Messages { get; set; } = [];

    public virtual ICollection<MessageHistory> MessageHistory { get; set; } = [];

    public virtual ICollection<OldLogMessage> OldLogMessages { get; set; } = [];

    public virtual ICollection<Tracker> Trackers { get; set; } = [];

    public virtual ICollection<Reward> Rewards { get; set; } = [];

    public virtual ICollection<Member> Members { get; set; } = [];

    public virtual ICollection<Sin> Sins { get; set; } = [];

    public virtual ICollection<Mute> ActiveMutes { get; set; } = [];

    public virtual ICollection<Lock> LockedChannels { get; set; } = [];

    public virtual ICollection<XpHistory> XpHistory { get; set; } = [];

    public virtual ICollection<IgnoredChannel> IgnoredChannels { get; set; } = [];

    public virtual ICollection<IgnoredMember> IgnoredMembers { get; set; } = [];

    public virtual ICollection<IgnoredRole> IgnoredRoles { get; set; } = [];

    public virtual ICollection<MessageLogChannelOverride> MessageLogChannelOverrides { get; set; } = [];

    public virtual ICollection<CustomCommand> CustomCommands { get; set; } = [];

    public virtual ICollection<CustomCommandRole> CustomCommandRoles { get; set; } = [];

    public virtual GuildUserLogSettings UserLogSettings { get; set; } = null!;

    public virtual GuildLevelSettings LevelSettings { get; set; } = null!;

    public virtual GuildModerationSettings ModerationSettings { get; set; } = null!;

    public virtual GuildMessageLogSettings MessageLogSettings { get; set; } = null!;

    public virtual GuildCommandsSettings CommandsSettings { get; set; } = null!;
}
