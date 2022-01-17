// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class User : Identifiable
    {
        public string UserName { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

        public virtual ICollection<Tracker> Trackers { get; set; } = new List<Tracker>();

        public virtual ICollection<Tracker> TrackedUsers { get; set; } = new List<Tracker>();

        public virtual ICollection<Sin> UserSins { get; set; } = new List<Sin>();

        public virtual ICollection<Sin> ModeratedSins { get; set; } = new List<Sin>();

        public virtual ICollection<Mute> ActiveMutes { get; set; } = new List<Mute>();

        public virtual ICollection<Lock> ChannelsLocked { get; set; } = new List<Lock>();

        public virtual ICollection<Pardon> SinsPardoned { get; set; } = new List<Pardon>();

        public virtual ICollection<GuildUser> GuildMembers { get; set; } = new List<GuildUser>();
    }
}
