namespace Cybermancy.Domain
{
    public class Reward
    {
        public ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; }
        public ulong RoleId { get; set; }
        public virtual Role Role { get; set; }
        public int RewardLevel { get; set; }
    }
}