using System;

namespace Cybermancy.Domain
{
    public class Lock
    {
        public ulong ChannelId { get; set; }
        public virtual Channel Channel { get; set; }
        public bool? PreviousSetting { get; set; }
        public ulong ModeratorId { get; set; }
        public virtual User Moderator { get; set; }
        public ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; }
        public string reason { get; set; }
        public DateTime EndTime { get; set; }
    }
}