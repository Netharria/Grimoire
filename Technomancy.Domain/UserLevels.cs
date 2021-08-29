using System;
using Technomancy.Domain.Shared;

namespace Technomancy.Domain
{
    public class UserLevels : IXpIgnore
    {
        public Guild Guild { get; set; }
        public User User { get; set; }
        public int Xp { get; set; }
        public DateTime TimeOut { get; set; }
        public bool IsXpIgnored { get; set; }
    }
}
