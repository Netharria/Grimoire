using System;

namespace Cybermancy.Domain
{
    public class Pardon
    {
        public Sin Sin { get; set; }
        public User Moderator { get; set; }
        public DateTime PardonDate { get; set; }
        public string Reason { get; set; }
    }
}
