using Cybermancy.Domain;
using System.Linq;

namespace Cybermancy.Core.Extensions
{
    public static class UserExtensions
    {
        public static UserLevel GetUserLevel(this User user, ulong guildId)
        {
            return user.UserLevels.FirstOrDefault(x => x.GuildId == guildId);
        }
    }
}
