using System;

namespace Cybermancy.Domain
{
    public class Mute
    {
        public ulong SinId { get; set; }
        public Sin Sin { get; set; }
        public DateTime EndTime { get; set; }
        public User User { get; set; }
        public Guild Guild { get; set; }
    }
}