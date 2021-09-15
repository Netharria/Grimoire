using System;
using System.Collections.Generic;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Message : Identifiable
    {
        public ulong UserId { get; set; }
        public virtual User User { get; set; }
        public virtual ulong ChannelId { get; set; }
        public virtual Channel Channel { get; set; }
        public ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual ICollection<Attachment> Attachments { get; set; }
    }
}