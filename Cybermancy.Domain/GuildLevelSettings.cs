namespace Cybermancy.Domain
{
    public class GuildLevelSettings
    {
        public ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; }
        public bool IsLevelingEnabled { get; set; }
        public int TextTime { get; set; }
        public int Base { get; set; }
        public int Modifier { get; set; }
        public int Amount { get; set; }
        public ulong? LevelChannelLog { get; set; }
    }
}