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
    public async Task<SpamFilterOverrideOption> GetSpamFilterOverrideAsync(
        GuildId guildId,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        return await this._cache.GetOrCreateAsync(
            CacheKey.SpamFilterOverride(channelId),
            new { GuildId = guildId, ChannelId = channelId },
            async (state, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var result = await dbContext.SpamFilterOverrides
                    .AsNoTracking()
                    .Where(x => x.GuildId == state.GuildId)
                    .Where(x => x.ChannelId == state.ChannelId)
                    .OrderByDescending(x => x.SetAt)
                    .Select(x => (SpamFilterOverrideOption?)x.ChannelOption)
                    .FirstOrDefaultAsync(ct);
                return result
                       ?? SpamFilterOverrideOption.Inherit;
            },
            this._cacheEntryOptions,
            cancellationToken: cancellationToken);
    }

    public async IAsyncEnumerable<SpamFilterOverride> GetAllSpamFilterOverrideAsync(GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var spamFilterOverride in dbContext.SpamFilterOverrides
                           .AsNoTracking()
                           .Where(x => x.GuildId == guildId)
                           .GroupBy(x => x.ChannelId)
                           .Select(g => g.OrderByDescending(x => x.SetAt).First())
                           .Where(x => x.ChannelOption != SpamFilterOverrideOption.Inherit)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return spamFilterOverride;
    }

    public async Task<SettingsResult> SetSpamFilterOverrideAsync(ChannelId channelId, GuildId guildId,
        ModeratorId setBy,
        SpamFilterOverrideOption option,
        CancellationToken cancellationToken = default)
    {
        var currentSetting = await GetSpamFilterOverrideAsync(guildId, channelId, cancellationToken);

        if  (currentSetting == option)
            return SettingsResult.Unchanged();

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        var spamFilterOverride = new SpamFilterOverride
        {
            ChannelId = channelId,
            GuildId = guildId,
            SetBy = setBy,
            SetAt = DateTimeOffset.UtcNow,
            ChannelOption = option
        };

        dbContext.SpamFilterOverrides.Add(spamFilterOverride);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.SetAsync(CacheKey.SpamFilterOverride(channelId),
            option, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
        return SettingsResult.Written();
    }
}
