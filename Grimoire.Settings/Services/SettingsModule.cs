// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule(
    IDbContextFactory<SettingsDbContext> dbContextFactory,
    HybridCache cache)
{
    private readonly HybridCache _cache = cache;

    private readonly HybridCacheEntryOptions _cacheEntryOptions = new() { Expiration = TimeSpan.FromDays(1) };

    private readonly IDbContextFactory<SettingsDbContext> _dbContextFactory = dbContextFactory;

    private ValueTask<CachedSetting> GetGuildSetting(GuildSettingType key, GuildId guildId,
        CancellationToken cancellationToken = default)
        => this._cache.GetOrCreateAsync(
            CacheKey.GuildSetting(key, guildId),
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
        FrozenSet<GuildSettingType> settingTypes,
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

    private async Task<SettingsResult> SetGuildSetting(GuildSetting newSetting,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var current = await dbContext.GuildSettings
            .AsNoTracking()
            .Where(s => s.GuildId == newSetting.GuildId && s.Type == newSetting.Type)
            .OrderByDescending(s => s.SetAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (IsRedundantWrite(current, newSetting))
            return SettingsResult.Unchanged();
        dbContext.GuildSettings.Add(newSetting);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(
            CacheKey.GuildSetting(newSetting.Type, newSetting.GuildId),
            cancellationToken);
        return SettingsResult.Written();
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
        => ParseChannelId(await GetGuildSetting(GuildSettingType.UserCommandChannel, guildId, cancellationToken));

    public Task<SettingsResult> SetUserCommandChannelSetting(
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
    {
        if (channelId is null)
            return SetGuildSetting(
                new GuildSettingDisabled
                {
                    GuildId = guildId,
                    Type = GuildSettingType.UserCommandChannel,
                    SetBy = moderatorId,
                    SetAt = DateTimeOffset.UtcNow
                }, cancellationToken);
        return SetGuildSetting(
            new GuildSettingCustomValue
            {
                GuildId = guildId,
                Type = GuildSettingType.UserCommandChannel,
                SetBy = moderatorId,
                SetAt = DateTimeOffset.UtcNow,
                Value = channelId.Value.Value.ToString(CultureInfo.InvariantCulture)
            }, cancellationToken);
    }

    private static ChannelId? ParseChannelId(CachedSetting setting) =>
        setting is CachedCustomSetting { Value: var v }
        && ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id != 0
            ? new ChannelId(id)
            : null;

    private static RoleId? ParseRoleId(CachedSetting setting) =>
        setting is CachedCustomSetting { Value: var v }
        && ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id != 0
            ? new RoleId(id)
            : null;

    private abstract record CachedSetting;

    private record CachedDefaultSetting : CachedSetting;

    private record CachedDisabledSetting : CachedSetting;

    private record CachedCustomSetting(string Value) : CachedSetting;
}
