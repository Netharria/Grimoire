using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public enum PublishType
    {
        Ban,
        Unban
    }
    public class PublishedMessage : Identifiable
    {
        public Sin Sin { get; set; }
        public PublishType PublishType { get; set; }
    }
}
