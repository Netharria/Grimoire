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

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public ulong? ModChannelLog { get; set; }

    public virtual Channel? ModLogChannel { get; init; }


    [Obsolete("Temporary to help identify where configurations are changed.")]
    public ulong? UserCommandChannelId { get; set; }

    public virtual Channel? UserCommandChannel { get; init; }

    public virtual ICollection<Channel> Channels { get; init; } = [];

    public virtual ICollection<Role> Roles { get; init; } = [];

    public virtual ICollection<Message> Messages { get; init; } = [];

    public virtual ICollection<MessageHistory> MessageHistory { get; init; } = [];

    public virtual ICollection<OldLogMessage> OldLogMessages { get; init; } = [];

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual ISet<Tracker> Trackers { get; init; } = new HashSet<Tracker>();

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual ISet<Reward> Rewards { get; init; } = new HashSet<Reward>();

    public virtual ICollection<Member> Members { get; init; } = [];

    public virtual ICollection<Sin> Sins { get; init; } = [];

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual ISet<Mute> ActiveMutes { get; init; } = new HashSet<Mute>();

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual ISet<Lock> LockedChannels { get; init; } = new HashSet<Lock>();

    public virtual ICollection<XpHistory> XpHistory { get; init; } = [];

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual ISet<IgnoredChannel> IgnoredChannels { get; init; } = new HashSet<IgnoredChannel>();

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual ISet<IgnoredMember> IgnoredMembers { get; init; } = new HashSet<IgnoredMember>();

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual ISet<IgnoredRole> IgnoredRoles { get; init; } = new HashSet<IgnoredRole>();

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual ISet<MessageLogChannelOverride> MessageLogChannelOverrides { get; init; } = new HashSet<MessageLogChannelOverride>();

    public virtual ICollection<SpamFilterOverride> SpamFilterOverrides { get; init; } = [];

    public virtual ICollection<CustomCommand> CustomCommands { get; init; } = [];

    public virtual ICollection<CustomCommandRole> CustomCommandRoles { get; init; } = [];

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual GuildUserLogSettings UserLogSettings { get; set; } = null!;

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual GuildLevelSettings LevelSettings { get; set; } = null!;

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual GuildModerationSettings ModerationSettings { get; set; } = null!;

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual GuildMessageLogSettings MessageLogSettings { get; set; } = null!;

    [Obsolete("Temporary to help identify where configurations are changed.")]
    public virtual GuildCommandsSettings CommandsSettings { get; set; } = null!;

    public ulong Id { get; init; }
}
