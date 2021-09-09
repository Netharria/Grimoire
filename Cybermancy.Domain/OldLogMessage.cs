using System;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class OldLogMessage : Identifiable
    {
        public ulong ChannelId { get; set; }
        public Guild Guild { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}