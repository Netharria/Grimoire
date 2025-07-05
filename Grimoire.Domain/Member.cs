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
public class Member : IMember
{
    public virtual Guild Guild { get; init; } = null!;

    public virtual User User { get; init; } = null!;

    public virtual IgnoredMember? IsIgnoredMember { get; init; }

    public virtual Mute? ActiveMute { get; init; }

    public virtual ICollection<Message> Messages { get; init; } = [];

    public virtual ICollection<Reaction> Reactions { get; init; } = [];

    public virtual ICollection<Tracker> Trackers { get; init; } = [];

    public virtual ICollection<Tracker> TrackedUsers { get; init; } = [];

    public virtual ICollection<Sin> UserSins { get; init; } = [];

    public virtual ICollection<Sin> ModeratedSins { get; init; } = [];

    public virtual ICollection<Lock> ChannelsLocked { get; init; } = [];

    public virtual ICollection<Pardon> SinsPardoned { get; init; } = [];

    public virtual ICollection<NicknameHistory> NicknamesHistory { get; init; } = [];
    public virtual ICollection<Avatar> AvatarHistory { get; init; } = [];
    public virtual ICollection<MessageHistory> MessagesDeletedAsModerator { get; init; } = [];
    public virtual ICollection<XpHistory> XpHistory { get; init; } = [];
    public virtual ICollection<XpHistory> AwardRecipients { get; init; } = [];
    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }
}
