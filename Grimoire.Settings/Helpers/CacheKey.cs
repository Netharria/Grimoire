using Grimoire.Settings.Domain;

namespace Grimoire.Settings.Helpers;

internal static class CacheKey
{
    internal static string GuildSetting(GuildSettingType type, GuildId guildId) => $"{type}_{guildId}";
    internal static string LevelingSettings(GuildId guildId) => $"LevelingSettings_{guildId}";
    internal static string LevelingRewards(GuildId guildId) => $"LevelingRewards_{guildId}";
    internal static string XpIgnoredItems(GuildId guildId) => $"XpIgnoredItems_{guildId}";
    internal static string LogOverride(ChannelId channelId) => $"LogOverrides_{channelId}";
    internal static string SpamFilterOverride(ChannelId channelId) => $"SpamFilterOverrides_{channelId}";
    internal static string Locks(GuildId guildId) => $"Locks_{guildId}";
    internal static string Trackers(GuildId guildId) => $"Trackers_{guildId}";
}
