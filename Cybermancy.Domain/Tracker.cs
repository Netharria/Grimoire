using System;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Tracker: Identifiable
    {
        public ulong UserId { get; set; }
        public virtual User User { get; set; }
        public ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; }
        public ulong LogChannelId { get; set; }
        public virtual Channel LogChannel { get; set; }
        public DateTime EndTime { get; set; }
        public ulong ModeratorId { get; set; }
        public virtual User Moderator { get; set; }
    }
}