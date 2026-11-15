// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public Task<Fin<SpamFilterOverrideOption>> GetSpamFilterOverrideAsync(
        GuildId guildId,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
            {
                var result = await this._cache.GetOrCreateAsync(
                    CacheKey.SpamFilterOverride(channelId),
                    new { GuildId = guildId, ChannelId = channelId },
                    async (state, innerCt) =>
                    {
                        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(innerCt);
                        var spamOverride = await dbContext.SpamFilterOverrides
                            .AsNoTracking()
                            .Where(x => x.GuildId == state.GuildId)
                            .Where(x => x.ChannelId == state.ChannelId)
                            .OrderByDescending(x => x.SetAt)
                            .Select(x => (SpamFilterOverrideOption?)x.ChannelOption)
                            .FirstOrDefaultAsync(innerCt);
                        return spamOverride ?? SpamFilterOverrideOption.Inherit;
                    },
                    this._cacheEntryOptions,
                    cancellationToken: ct);
                return Fin.Succ<SpamFilterOverrideOption>(result);
            }, "Could not retrieve spam filter override.",
            cancellationToken);

    public async IAsyncEnumerable<SpamFilterOverride> GetAllSpamFilterOverrideAsync(GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<SpamFilterOverride> overrides;
        try
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            // EF Core cannot translate a .Where() after GroupBy().Select(g => g.First()), so the latest row per
            // channel is loaded and filtered in memory.
            overrides = await dbContext.SpamFilterOverrides
                .AsNoTracking()
                .Where(x => x.GuildId == guildId)
                .GroupBy(x => x.ChannelId)
                .Select(g => g.OrderByDescending(x => x.SetAt).First())
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            yield break;
        }

        foreach (var spamFilterOverride in overrides)
            if (spamFilterOverride.ChannelOption != SpamFilterOverrideOption.Inherit)
                yield return spamFilterOverride;
    }

    public Task<Fin<Write<SpamFilterOverride>>> SetSpamFilterOverrideAsync(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        SpamFilterOverrideOption option,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync<Write<SpamFilterOverride>>(async ct =>
            {
                var currentSetting = await GetSpamFilterOverrideAsync(guildId, channelId, ct)
                    .OrDefaultDuringMigration(SpamFilterOverrideOption.Inherit);

                if (currentSetting == option)
                    return new Write<SpamFilterOverride>.Unchanged(
                        "The channel is already set to this spam filter option.");

                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);

                var spamFilterOverride =
                    SpamFilterOverride.Create(option, channelId, guildId, setBy, DateTimeOffset.UtcNow);
                dbContext.SpamFilterOverrides.Add(spamFilterOverride);
                await dbContext.SaveChangesAsync(ct);
                await this._cache.SetAsync(CacheKey.SpamFilterOverride(channelId), option,
                    this._cacheEntryOptions, cancellationToken: ct);
                return new Write<SpamFilterOverride>.Saved(spamFilterOverride);
            }, "Could not save the spam filter override.",
            cancellationToken);
}
