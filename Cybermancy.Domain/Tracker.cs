using System;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Tracker: Identifiable
    {
        public ulong UserId { get; set; }
        public User User { get; set; }
        public ulong GuildId { get; set; }
        public Guild Guild { get; set; }
        public Channel LogChannel { get; set; }
        public DateTime EndTime { get; set; }
        public ulong ModeratorId { get; set; }
        public User Moderator { get; set; }
    }
}