using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public enum PublishType
    {
        Ban,
        Unban
    }

    public class PublishedMessage
    {
        public ulong MessageId { get; set; }
        public ulong SinId { get; set; }
        public virtual Sin Sin { get; set; }
        public PublishType PublishType { get; set; }
    }
}