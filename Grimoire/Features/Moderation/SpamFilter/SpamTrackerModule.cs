// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Features.Moderation.SpamFilter;

public class SpamTrackerModule(SettingsModule settingsModule, IMemoryCache memoryCache)
{
    // private const string CacheKeyPrefix = "SpamFilterOverrideChannel_{0}";
    // private const string SpamUserCacheKeyPrefix = "SpamUser_{0}_{1}";
    private const double BasePoints = 10.0;
    private const double AttachmentMultiplier = 4.15;
    private const double SpamThreshold = 60.0;
    private const double DecayRate = 2.0;
    private const double MessageLengthMultiplier = 0.00625;
    private const double MessageLineMultiplier = 0.714;
    private const double MentionMultiplier = 2.5;
    private const double DuplicateMessageMultiplier = 10.0;

    private readonly MemoryCacheEntryOptions _cacheEntryOptions = new() { SlidingExpiration = TimeSpan.FromHours(2) };

    private readonly IMemoryCache _memoryCache = memoryCache;

    private readonly SettingsModule _settingsModule = settingsModule;

    private static string GetSpamFilterCacheKey(ChannelId channelId)
        => $"SpamFilterOverrideChannel_{channelId}";

    private static string GetSpamUserCacheKey(GuildId guildId, ulong memberId)
        => $"SpamUser_{guildId}_{memberId}";

    private async Task<SpamFilterOverrideCacheOption> GetSpamFilterOverrideChannelAsync(ChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        return await this._memoryCache.GetOrCreateAsync(GetSpamFilterCacheKey(channelId), async _ =>
        {
            var spamFilterOverrideOption =
                await this._settingsModule.GetSpamFilterOverrideAsync(channelId, cancellationToken);
            return spamFilterOverrideOption switch
            {
                SpamFilterOverrideOption.AlwaysFilter => SpamFilterOverrideCacheOption.AlwaysFilter,
                SpamFilterOverrideOption.NeverFilter => SpamFilterOverrideCacheOption.NeverFilter,
                _ => SpamFilterOverrideCacheOption.Default
            };
        }, this._cacheEntryOptions);
    }

    private void SetSpamFilterCache(ChannelId channelId, SpamFilterOverrideOption? overrideOption)
        => this._memoryCache.Set(GetSpamFilterCacheKey(channelId),
            overrideOption switch
            {
                SpamFilterOverrideOption.AlwaysFilter => SpamFilterOverrideCacheOption.AlwaysFilter,
                SpamFilterOverrideOption.NeverFilter => SpamFilterOverrideCacheOption.NeverFilter,
                _ => SpamFilterOverrideCacheOption.Default
            }, this._cacheEntryOptions);

    public async Task AddOrUpdateOverride(ChannelId channelId, GuildId guildId, SpamFilterOverrideOption option,
        CancellationToken cancellationToken = default)
    {
        await this._settingsModule.SetSpamFilterOverrideAsync(channelId, guildId, option, cancellationToken);
        SetSpamFilterCache(channelId, option);
    }

    public async Task RemoveOverride(ChannelId channelId, GuildId guildId, CancellationToken cancellationToken = default)
    {
        await this._settingsModule.RemoveSpamFilterOverrideAsync(channelId, guildId, cancellationToken);
        SetSpamFilterCache(channelId, null);
    }

    public async Task<CheckSpamResult> CheckSpam(DiscordMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Author is not DiscordMember member
            || message.Channel is null
            || member.Permissions.HasPermission(DiscordPermission.ManageChannels)
            || member.IsOwner)
            return new CheckSpamResult { IsSpam = false };

        var currentChannel = message.Channel;
        while (currentChannel is not null)
        {
            var spamFilterOverrideOption =
                await GetSpamFilterOverrideChannelAsync(currentChannel.GetChannelId(), cancellationToken);

            if (spamFilterOverrideOption == SpamFilterOverrideCacheOption.AlwaysFilter)
                break;
            if (spamFilterOverrideOption == SpamFilterOverrideCacheOption.NeverFilter)
                return new CheckSpamResult { IsSpam = false };

            currentChannel = currentChannel.Parent;
        }

        var spamTracker = this._memoryCache.GetOrCreate(
            GetSpamUserCacheKey(member.Guild.GetGuildId(), member.Id),
            entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                return new SpamTracker();
            }) ?? new SpamTracker();

        if (message.Id == spamTracker.LastMessageId)
            return new CheckSpamResult { IsSpam = false };

        spamTracker.LastMessageId = message.Id;

        spamTracker.PointTotal -= (message.Timestamp - spamTracker.DateTimeOffset).TotalSeconds * DecayRate;

        if (spamTracker.PointTotal < 0)
            spamTracker.PointTotal = 0;

        var points = BasePoints;
        points += message.Attachments.Count * AttachmentMultiplier;
        points += message.Embeds.Count * AttachmentMultiplier;
        points += message.Content.Length * MessageLengthMultiplier;
        points += message.Content.Count(c => c == '\n') * MessageLineMultiplier;
        points += (message.MentionedRoles.Count + message.MentionedUsers.Count) * MentionMultiplier;

        if (message.Content.Length > 0 && message.Content == spamTracker.MessageCache)
            points += DuplicateMessageMultiplier;

        spamTracker.PointTotal += points;
        spamTracker.MessageCache = message.Content;
        spamTracker.DateTimeOffset = message.Timestamp;

        return new CheckSpamResult
        {
            IsSpam = spamTracker.PointTotal > SpamThreshold,
            Reason = spamTracker.PointTotal > SpamThreshold ? "Auto mod: Spam detected." : string.Empty
        };
    }

    private sealed record SpamTracker
    {
        public double PointTotal { get; set; }
        public string MessageCache { get; set; } = string.Empty;
        public ulong LastMessageId { get; set; }
        public DateTimeOffset DateTimeOffset { get; set; }
    }

    public sealed record CheckSpamResult
    {
        public required bool IsSpam { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    private enum SpamFilterOverrideCacheOption
    {
        AlwaysFilter,
        NeverFilter,
        Default
    }
}
