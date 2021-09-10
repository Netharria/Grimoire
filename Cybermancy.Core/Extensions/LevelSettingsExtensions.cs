using System;
using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class LevelSettingsExtensions
    {
        public static DateTime GetTextTimeout(this GuildLevelSettings levelSettings)
        {
            return DateTime.UtcNow + TimeSpan.FromMinutes(levelSettings.TextTime);
        }
    }
}