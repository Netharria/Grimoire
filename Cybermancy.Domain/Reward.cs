namespace Cybermancy.Domain
{
    public class Reward
    {
        public ulong GuildId { get; set; }
        public Guild Guild { get; set; }
        public ulong RoleId { get; set; }
        public Role Role { get; set; }
        public int RewardLevel { get; set; }
    }
}