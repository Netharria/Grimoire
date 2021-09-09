using System.Collections.Generic;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class User : Identifiable
    {
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
        public ICollection<Guild> Guilds { get; set; }
        public ICollection<Message> Messages { get; set; }
        public ICollection<Tracker> Trackers { get; set; }
        public ICollection<Tracker> TrackedUsers { get; set; }
        public ICollection<Sin> UserSins { get; set; }
        public ICollection<Sin> ModeratedSins { get; set; }
        public ICollection<Mute> ActiveMutes { get; set; }
        public ICollection<Lock> ChannelsLocked { get; set; }
        public ICollection<Pardon> SinsPardoned { get; set; }
    }
}