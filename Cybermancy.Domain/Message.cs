using System;
using System.Collections.Generic;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Message: Identifiable
    {
        public User User { get; set; }
        public Channel Channel { get; set; }
        public Guild Guild { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICollection<Attachment> Attachments { get; set; }
    }
}
