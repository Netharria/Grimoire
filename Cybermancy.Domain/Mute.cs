using System;

namespace Cybermancy.Domain
{
    public class Mute
    {
        public ulong SinId { get; set; }
        public virtual Sin Sin { get; set; }
        public DateTime EndTime { get; set; }
        public ulong UserId { get; set; }
        public virtual User User { get; set; }
        public ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; }
    }
}