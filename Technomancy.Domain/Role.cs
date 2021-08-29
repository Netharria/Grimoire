using Technomancy.Domain.Shared;

namespace Technomancy.Domain
{
    public class Role : Identifiable, IXpIgnore
    {
        public Guild Guild { get; set; }
        public bool IsXpIgnored { get; set; }
        public Reward Reward { get; set; }
    }
}
