// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule(
    IDbContextFactory<SettingsDbContext> dbContextFactory,
    HybridCache cache,
    ILogger<SettingsModule> logger)
{
    private readonly HybridCache _cache = cache;

    private readonly HybridCacheEntryOptions _cacheEntryOptions = new() { Expiration = TimeSpan.FromDays(1) };

    private readonly IDbContextFactory<SettingsDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<SettingsModule> _logger = logger;

    private static string GetGuildSettingsCacheKey(GuildSettingType key, GuildId guildId) => $"{key}_{guildId}";

    private ValueTask<CachedSetting> GetGuildSetting(GuildSettingType key, GuildId guildId,
        CancellationToken cancellationToken = default)
        => this._cache.GetOrCreateAsync(
            GetGuildSettingsCacheKey(key, guildId),
            new { key, guildId },
            async (state, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var result = await dbContext.GuildSettings
                    .AsNoTracking()
                    .Where(setting => setting.Type == state.key && setting.GuildId == state.guildId)
                    .OrderByDescending(setting => setting.SetAt)
                    .FirstOrDefaultAsync(ct);
                return ToCachedSetting(result);
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);

    private async IAsyncEnumerable<GuildSetting> GetGuildSettings(
        GuildId guildId,
        IReadOnlyList<GuildSettingType> settingTypes,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        var settings = dbContext.GuildSettings
            .AsNoTracking()
            .Where(s => s.GuildId == guildId && settingTypes.Contains(s.Type))
            .GroupBy(s => s.Type)
            .Select(g => g
                .OrderByDescending(s => s.SetAt)
                .First())
            .AsAsyncEnumerable();

        await foreach (var setting in settings.WithCancellation(cancellationToken)) yield return setting;
    }

    private static CachedSetting ToCachedSetting(GuildSetting? guildSetting) =>
        guildSetting switch
        {
            GuildSettingCustomValue customValue => new CachedCustomSetting(customValue.Value),
            GuildSettingDisabled => new CachedDisabledSetting(),
            _ => new CachedDefaultSetting()
        };

    private async Task SetGuildSetting(GuildSetting newSetting,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var current = await dbContext.GuildSettings
            .AsNoTracking()
            .Where(s => s.GuildId == newSetting.GuildId && s.Type == newSetting.Type)
            .OrderByDescending(s => s.SetAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (IsRedundantWrite(current, newSetting))
            return;
        dbContext.GuildSettings.Add(newSetting);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(
            GetGuildSettingsCacheKey(newSetting.Type, newSetting.GuildId),
            cancellationToken);
    }

    private static bool IsRedundantWrite(GuildSetting? current, GuildSetting incoming) =>
        (current, incoming) switch
        {
            (GuildSettingDisabled, GuildSettingDisabled) => true,
            (GuildSettingDefault, GuildSettingDefault) => true,
            (GuildSettingCustomValue a, GuildSettingCustomValue b) =>
                string.Equals(a.Value, b.Value, StringComparison.Ordinal),
            _ => false
        };

    public async Task<ChannelId?> GetUserCommandChannel(GuildId guildId, CancellationToken cancellationToken = default)
    {
        var result = await GetGuildSetting(GuildSettingType.UserCommandChannel, guildId, cancellationToken);

        if (result is not CachedCustomSetting setting)
            return null;
        if (ulong.TryParse(setting.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var channelId)
            && channelId != 0)
            return new ChannelId(channelId);

        return null;
    }

    public Task SetUserCommandChannelSetting(
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
    {
        if (channelId is null)
            return SetGuildSetting(
                new GuildSettingDisabled
                {
                    GuildId = guildId, Type = GuildSettingType.UserCommandChannel, SetBy = moderatorId
                }, cancellationToken);
        return SetGuildSetting(
            new GuildSettingCustomValue
            {
                GuildId = guildId,
                Type = GuildSettingType.UserCommandChannel,
                SetBy = moderatorId,
                Value = channelId.Value.Value.ToString(CultureInfo.InvariantCulture)
            }, cancellationToken);
    }

    private abstract record CachedSetting;

    private record CachedDefaultSetting : CachedSetting;

    private record CachedDisabledSetting : CachedSetting;

    private record CachedCustomSetting(string Value) : CachedSetting;
}
