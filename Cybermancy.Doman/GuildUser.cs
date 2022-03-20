// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class GuildUser : IXpIgnore
    {
        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; } = null!;

        public ulong UserId { get; set; }

        public virtual User User { get; set; } = null!;

        private string? _displayName;

        public string DisplayName
        {
            get => this._displayName ?? this.User.UserName;
            set => this._displayName = value;
        }

        private string? _guildAvatarUrl;
        public string GuildAvatarUrl
        {
            get => this._guildAvatarUrl ?? this.User.AvatarUrl;
            set => this._guildAvatarUrl = value;
        }

        public ulong Xp { get; set; }

        public DateTime TimeOut { get; set; } = DateTime.UtcNow;

        public bool IsXpIgnored { get; set; }

        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

        public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();

        public virtual ICollection<Tracker> Trackers { get; set; } = new List<Tracker>();

        public virtual ICollection<Tracker> TrackedUsers { get; set; } = new List<Tracker>();

        public virtual ICollection<Sin> UserSins { get; set; } = new List<Sin>();

        public virtual ICollection<Sin> ModeratedSins { get; set; } = new List<Sin>();

        public virtual ICollection<Mute> ActiveMutes { get; set; } = new List<Mute>();

        public virtual ICollection<Lock> ChannelsLocked { get; set; } = new List<Lock>();

        public virtual ICollection<Pardon> SinsPardoned { get; set; } = new List<Pardon>();

        public virtual ICollection<NicknameHistory> NicknamesHistory { get; set;} = new List<NicknameHistory>();
        public virtual ICollection<Message> MessagesDeletedAsModerator { get; set; } = new List<Message>();
    }
}
