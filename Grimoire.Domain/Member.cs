// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class Member : IMember
{
    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public ulong UserId { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual IgnoredMember? IsIgnoredMember { get; set; }

    public virtual Mute? ActiveMute { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = [];

    public virtual ICollection<Reaction> Reactions { get; set; } = [];

    public virtual ICollection<Tracker> Trackers { get; set; } = [];

    public virtual ICollection<Tracker> TrackedUsers { get; set; } = [];

    public virtual ICollection<Sin> UserSins { get; set; } = [];

    public virtual ICollection<Sin> ModeratedSins { get; set; } = [];

    public virtual ICollection<Lock> ChannelsLocked { get; set; } = [];

    public virtual ICollection<Pardon> SinsPardoned { get; set; } = [];

    public virtual ICollection<NicknameHistory> NicknamesHistory { get; set; } = [];
    public virtual ICollection<Avatar> AvatarHistory { get; set; } = [];
    public virtual ICollection<MessageHistory> MessagesDeletedAsModerator { get; set; } = [];
    public virtual ICollection<XpHistory> XpHistory { get; set; } = [];
    public virtual ICollection<XpHistory> AwardRecipients { get; set; } = [];
}
