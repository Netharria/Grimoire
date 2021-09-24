// -----------------------------------------------------------------------
// <copyright file="User.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Domain
{
    using System.Collections.Generic;
    using Cybermancy.Domain.Shared;

    public class User : Identifiable
    {
        public string UserName { get; set; }

        public string DisplayName { get; set; }

        public string AvatarUrl { get; set; }

        public virtual ICollection<Guild> Guilds { get; set; } = new List<Guild>();

        public virtual ICollection<Message> Messages { get; set; }

        public virtual ICollection<Tracker> Trackers { get; set; }

        public virtual ICollection<Tracker> TrackedUsers { get; set; }

        public virtual ICollection<Sin> UserSins { get; set; }

        public virtual ICollection<Sin> ModeratedSins { get; set; }

        public virtual ICollection<Mute> ActiveMutes { get; set; }

        public virtual ICollection<Lock> ChannelsLocked { get; set; }

        public virtual ICollection<Pardon> SinsPardoned { get; set; }

        public virtual ICollection<UserLevel> UserLevels { get; set; }
    }
}