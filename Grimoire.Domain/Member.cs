// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class Member : IMember, IMentionable
{
    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public ulong UserId { get; set; }

    public virtual User User { get; set; } = null!;

    [Obsolete("This property has moved to the IgnoredMember Table.")]
    public bool IsXpIgnored { get; set; }

    public virtual IgnoredMember? IsIgnoredMember { get; set; }

    public virtual Mute? ActiveMute { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();

    public virtual ICollection<Tracker> Trackers { get; set; } = new List<Tracker>();

    public virtual ICollection<Tracker> TrackedUsers { get; set; } = new List<Tracker>();

    public virtual ICollection<Sin> UserSins { get; set; } = new List<Sin>();

    public virtual ICollection<Sin> ModeratedSins { get; set; } = new List<Sin>();

    public virtual ICollection<Lock> ChannelsLocked { get; set; } = new List<Lock>();

    public virtual ICollection<Pardon> SinsPardoned { get; set; } = new List<Pardon>();

    public virtual ICollection<NicknameHistory> NicknamesHistory { get; set; } = new List<NicknameHistory>();
    public virtual ICollection<Avatar> AvatarHistory { get; set; } = new List<Avatar>();
    public virtual ICollection<MessageHistory> MessagesDeletedAsModerator { get; set; } = new List<MessageHistory>();
    public virtual ICollection<XpHistory> XpHistory { get; set; } = new List<XpHistory>();
    public virtual ICollection<XpHistory> AwardRecipients { get; set; } = new List<XpHistory>();
}
