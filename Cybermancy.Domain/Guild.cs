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

        public virtual ICollection<Channel> Channels { get; set; } = new List<Channel>();

        public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

        public virtual ICollection<MessageHistory> MessageHistory { get; set; } = new List<MessageHistory>();

        public virtual ICollection<OldLogMessage> OldLogMessages { get; set; } = new List<OldLogMessage>();

        public virtual ICollection<Tracker> Trackers { get; set; } = new List<Tracker>();

        public virtual ICollection<Reward> Rewards { get; set; } = new List<Reward>();

        public virtual ICollection<GuildUser> GuildUsers { get; set; } = new List<GuildUser>();

        public virtual ICollection<Sin> Sins { get; set; } = new List<Sin>();

        public virtual ICollection<Mute> ActiveMutes { get; set; } = new List<Mute>();

        public virtual ICollection<Lock> LockedChannels { get; set; } = new List<Lock>();

        public virtual GuildLogSettings LogSettings { get; set; } = null!;

        public virtual GuildLevelSettings LevelSettings { get; set; } = null!;

        public virtual GuildModerationSettings ModerationSettings { get; set; } = null!;
    }
}
