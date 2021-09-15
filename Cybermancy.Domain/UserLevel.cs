using System;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class UserLevel : Identifiable, IXpIgnore
    {
        public ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; }
        public ulong UserId { get; set; }
        public virtual User User { get; set; }
        public int Xp { get; set; }
        public DateTime TimeOut { get; set; }
        public bool IsXpIgnored { get; set; }
    }
}