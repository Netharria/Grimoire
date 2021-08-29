using System;
using System.Collections.Generic;
using Technomancy.Domain.Shared;

namespace Technomancy.Domain
{
    public enum SinType
    {
        Warn,
        Mute,
        Ban
    }
    public class Sin: Identifiable
    {
        public ulong UserId { get; set; }
        public User User { get; set; }
        public ulong ModeratorId { get; set; }
        public User Moderator { get; set; }
        public Guild Guild { get; set; }
        public string Reason { get; set; }
        public DateTime InfractionOn { get; set; }
        public SinType SinType { get; set; }
        public Mute Mute { get; set; }
        public Pardon Pardon { get; set; }
        public ICollection<PublishedMessage> PublishMessages { get; set; }

    }
}
