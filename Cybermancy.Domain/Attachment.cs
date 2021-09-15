namespace Cybermancy.Domain
{
    public class Attachment
    {
        public ulong MessageId { get; set; }
        public virtual Message Message { get; set; }
        public string AttachmentUrl { get; set; }
    }
}