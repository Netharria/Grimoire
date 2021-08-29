using System;
using Technomancy.Domain.Shared;

namespace Technomancy.Domain
{
    public class OldLogMessage : Identifiable
    {
        public ulong ChannelId { get; set; }
        public Guild Guild { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
