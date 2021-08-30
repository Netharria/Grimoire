namespace Cybermancy.Domain
{
    public enum Duration
    {
        Days = 1,
        Months = 2,
        Years = 3
    }
    public class GuildModerationSettings
    {
        public Guild Guild { get; set; }
        public ulong? PublicBanLog { get; set; }
        public Duration DurationType { get; set; }
        public int Duration { get; set; }
        public ulong? MuteRole { get; set; }
    }
}
