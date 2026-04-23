// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;
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
    private readonly ILogger<SettingsModule> _logger = logger;

    private readonly HybridCacheEntryOptions _cacheEntryOptions = new() { Expiration = TimeSpan.FromDays(1) };

    private readonly IDbContextFactory<SettingsDbContext> _dbContextFactory = dbContextFactory;

    private async ValueTask<Result<CachedSetting>> GetGuildSetting(GuildSettingType key, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await this._cache.GetOrCreateAsync(
                CacheKey.GuildSetting(key, guildId),
                new { key, guildId, this._dbContextFactory },
                async (state, ct) =>
                {
                    await using var dbContext = await state._dbContextFactory.CreateDbContextAsync(ct);
                    var result = await dbContext.GuildSettings
                        .AsNoTracking()
                        .Where(setting => setting.Type == state.key && setting.GuildId == state.guildId)
                        .OrderByDescending(setting => setting.SetAt)
                        .FirstOrDefaultAsync(ct);
                    return ToCachedSetting(result);
                }, this._cacheEntryOptions,
                cancellationToken: cancellationToken);

            return result switch
            {
                not null => Result<CachedSetting>.Ok(result),
                _ => new Result<CachedSetting>.NotFound(new Error($"guild-setting.{key}.not-found",
                    $"Was not able to find an entry for {key} for guild {guildId}"))
            };
        }
        catch (Exception ex)
        {
            LogSettingLookupFailure(_logger, ex.Message, ex);
            return Result<CachedSetting>.Fail(new Error($"guild-setting.{key}.lookup-failed",
                $"Failed when retrieving entry for {key} for guild {guildId}"));
        }

    }

    [LoggerMessage(LogLevel.Error, "Was not able to retrieve a setting from the database or cache for the following reason : {message}")]
    private static partial void LogSettingLookupFailure(ILogger logger, string message, Exception? ex);

    private async IAsyncEnumerable<GuildSetting> GetGuildSettings(
      GuildId guildId,
      FrozenSet<GuildSettingType> settingTypes,
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {

      await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
      await foreach (var setting in dbContext.GuildSettings
          .AsNoTracking()
          .Where(s => s.GuildId == guildId && settingTypes.Contains(s.Type))
          .GroupBy(s => s.Type)
          .Select(g => g.OrderByDescending(s => s.SetAt).First())
          .AsAsyncEnumerable()
          .WithCancellation(cancellationToken))
      {
          yield return setting;
      }
  }


    private static CachedSetting ToCachedSetting(GuildSetting? guildSetting) =>
        guildSetting switch
        {
            GuildSettingCustomValue customValue => new CachedCustomSetting(customValue.Value),
            GuildSettingDisabled => new CachedDisabledSetting(),
            _ => new CachedDefaultSetting()
        };

    private async Task<Result<GuildSetting>> SetGuildSetting(GuildSetting newSetting,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var current = await dbContext.GuildSettings
                .AsNoTracking()
                .Where(s => s.GuildId == newSetting.GuildId && s.Type == newSetting.Type)
                .OrderByDescending(s => s.SetAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (IsRedundantWrite(current, newSetting))
                return new Result<GuildSetting>.NotModified(new Error($"guild-setting.{newSetting.Type}.not-changed",
                    "The setting was already set to that value."));
            dbContext.GuildSettings.Add(newSetting);
            await dbContext.SaveChangesAsync(cancellationToken);
            await this._cache.RemoveAsync(
                CacheKey.GuildSetting(newSetting.Type, newSetting.GuildId),
                cancellationToken);
            return Result<GuildSetting>.Ok(newSetting);
        }
        catch (Exception ex)
        {
            LogSettingSaveFailure(_logger, ex.Message, ex);
            return Result<GuildSetting>.Fail(new Error($"guild-setting.{newSetting.Type}.conflict", "Was not able to save the setting to the database."));
        }
    }

    [LoggerMessage(LogLevel.Error, "Was not able to save a setting to the database or cache for the following reason : {message}")]
    private static partial void LogSettingSaveFailure(ILogger logger, string message, Exception? ex);

    private static bool IsRedundantWrite(GuildSetting? current, GuildSetting incoming) =>
        (current, incoming) switch
        {
            (GuildSettingDisabled, GuildSettingDisabled) => true,
            (GuildSettingDefault, GuildSettingDefault) => true,
            (GuildSettingCustomValue a, GuildSettingCustomValue b) =>
                string.Equals(a.Value, b.Value, StringComparison.Ordinal),
            _ => false
        };

    public async Task<Result<ChannelId?>> GetUserCommandChannel(GuildId guildId, CancellationToken cancellationToken = default)
        => (await GetGuildSetting(GuildSettingType.UserCommandChannel, guildId, cancellationToken))
            .Map(ParseChannelId);

    public async Task<Result<ChannelId?>> SetUserCommandChannelSetting(
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
        => (channelId switch
        {
            not null => await SetGuildSetting(
                new GuildSettingCustomValue(GuildSettingType.UserCommandChannel, guildId, moderatorId,
                    DateTimeOffset.UtcNow,
                    channelId.Value.Value.ToString(CultureInfo.InvariantCulture)),
                cancellationToken),
            _ => await SetGuildSetting(
                new GuildSettingDisabled(GuildSettingType.UserCommandChannel, guildId, moderatorId,
                    DateTimeOffset.UtcNow),
                cancellationToken)
        }).Map(_ => channelId);

    private static T? ParseId<T>(CachedSetting setting, Func<ulong, T> create) where T : struct =>
        setting is CachedCustomSetting { Value: var v }
        && ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id != 0
            ? create(id)
            : null;

    private static ChannelId? ParseChannelId(CachedSetting setting)
    => ParseId(setting, id => new ChannelId(id));

    private static RoleId? ParseRoleId(CachedSetting setting)
        => ParseId(setting, id => new RoleId(id));

    private abstract record CachedSetting;

    private record CachedDefaultSetting : CachedSetting;

    private record CachedDisabledSetting : CachedSetting;

    private record CachedCustomSetting(string Value) : CachedSetting;
}
