// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Guild : Identifiable
    {
        public ulong? ModChannelLog { get; set; }

        public virtual ICollection<Channel> Channels { get; set; }

        public virtual ICollection<Role> Roles { get; set; }

        public virtual ICollection<Message> Messages { get; set; }

        public virtual ICollection<OldLogMessage> OldLogMessages { get; set; }

        public virtual ICollection<Tracker> Trackers { get; set; }

        public virtual ICollection<Reward> Rewards { get; set; }

        public virtual ICollection<GuildUser> GuildUsers { get; set; }

        public virtual ICollection<Sin> Sins { get; set; }

        public virtual ICollection<Mute> ActiveMutes { get; set; }

        public virtual ICollection<Lock> LockedChannels { get; set; }

        public virtual GuildLogSettings LogSettings { get; set; }

        public virtual GuildLevelSettings LevelSettings { get; set; }

        public virtual GuildModerationSettings ModerationSettings { get; set; }
    }
}
