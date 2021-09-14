using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Role : Identifiable, IXpIgnore
    {
        public ulong GuildId { get; set; }
        public Guild Guild { get; set; }
        public Reward Reward { get; set; }
        public bool IsXpIgnored { get; set; }
    }
}