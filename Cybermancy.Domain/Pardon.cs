using System;

namespace Cybermancy.Domain
{
    public class Pardon
    {
        public ulong SinId { get; set; }
        public virtual Sin Sin { get; set; }
        public ulong ModeratorId { get; set; }
        public virtual User Moderator { get; set; }
        public DateTime PardonDate { get; set; }
        public string Reason { get; set; }
    }
}