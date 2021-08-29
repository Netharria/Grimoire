using System;

namespace Technomancy.Domain
{
    public class Tracker
    {
        public ulong UserId { get; set; }
        public User User { get; set; }
        public Guild Guild { get; set; }
        public Channel LogChannel { get; set; }
        public DateTime EndTime { get; set; }
        public ulong ModeratorId { get; set; }
        public User Moderator { get; set; }
    }
}
