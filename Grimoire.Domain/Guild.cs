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
public class Guild : IIdentifiable<ulong>
{
    public ulong? ModChannelLog { get; set; }

    public virtual Channel? ModLogChannel { get; init; }

    public ulong? UserCommandChannelId { get; set; }

    public virtual Channel? UserCommandChannel { get; init; }

    public virtual ICollection<Channel> Channels { get; init; } = [];

    public virtual ICollection<Role> Roles { get; init; } = [];

    public virtual ICollection<Message> Messages { get; init; } = [];

    public virtual ICollection<MessageHistory> MessageHistory { get; init; } = [];

    public virtual ICollection<OldLogMessage> OldLogMessages { get; init; } = [];

    public virtual ICollection<Tracker> Trackers { get; init; } = [];

    public virtual ICollection<Reward> Rewards { get; init; } = [];

    public virtual ICollection<Member> Members { get; init; } = [];

    public virtual ICollection<Sin> Sins { get; init; } = [];

    public virtual ICollection<Mute> ActiveMutes { get; init; } = [];

    public virtual ICollection<Lock> LockedChannels { get; init; } = [];

    public virtual ICollection<XpHistory> XpHistory { get; init; } = [];

    public virtual ICollection<IgnoredChannel> IgnoredChannels { get; init; } = [];

    public virtual ICollection<IgnoredMember> IgnoredMembers { get; init; } = [];

    public virtual ICollection<IgnoredRole> IgnoredRoles { get; init; } = [];

    public virtual ICollection<MessageLogChannelOverride> MessageLogChannelOverrides { get; init; } = [];

    public virtual ICollection<SpamFilterOverride> SpamFilterOverrides { get; init; } = [];

    public virtual ICollection<CustomCommand> CustomCommands { get; init; } = [];

    public virtual ICollection<CustomCommandRole> CustomCommandRoles { get; init; } = [];

    public virtual GuildUserLogSettings UserLogSettings { get; init; } = null!;

    public virtual GuildLevelSettings LevelSettings { get; init; } = null!;

    public virtual GuildModerationSettings ModerationSettings { get; init; } = null!;

    public virtual GuildMessageLogSettings MessageLogSettings { get; init; } = null!;

    public virtual GuildCommandsSettings CommandsSettings { get; init; } = null!;
    public ulong Id { get; set; }
}
