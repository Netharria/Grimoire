using System.Collections.Generic;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Guild : Identifiable
    {
        public ulong? ModChannelLog { get; set; }
        public ICollection<Channel> Channels { get; set; }
        public ICollection<Role> Roles { get; set; }
        public ICollection<Message> Messages { get; set; }
        public ICollection<User> Users { get; set; }
        public ICollection<OldLogMessage> OldLogMessages { get; set; }
        public ICollection<Tracker> Trackers { get; set; }
        public ICollection<Reward> Rewards { get; set; }
        public ICollection<UserLevels> UserLevels { get; set; }
        public ICollection<Sin> Sins { get; set; }
        public ICollection<Mute> ActiveMutes { get; set; }
        public ICollection<Lock> LockedChannels { get; set; }
        public GuildLogSettings LogSettings { get; set; }
        public GuildLevelSettings LevelSettings { get; set; }
        public GuildModerationSettings ModerationSettings { get; set; }
    }
}