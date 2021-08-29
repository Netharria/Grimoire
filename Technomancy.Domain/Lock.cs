using System;

namespace Technomancy.Domain
{
    public class Lock
    {
        public Channel Channel { get; set; }
        public bool? PreviousSetting { get; set; }
        public User Moderator { get; set; }
        public Guild Guild { get; set; }
        public string reason { get; set; }
        public DateTime EndTime { get; set; }
    }
}
