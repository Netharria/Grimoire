// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public partial class SettingsModule
{
    private const string MuteRoleCacheKeyPrefix = "MuteRole_{0}";

    public async Task<RoleId?> GetMuteRole(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return null;
        var cacheEntry = await GetMuteRoleCacheEntry(guildId, cancellationToken);
        return cacheEntry?.Id;
    }

    private async Task<MuteCacheEntry?> GetMuteRoleCacheEntry(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(MuteRoleCacheKeyPrefix, guildId);
        return await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext
                .ModerationSettings
                .AsNoTracking()
                .Where(reward => reward.GuildId == guildId)
                .Select(reward => reward.MuteRole)
                .FirstOrDefaultAsync(cancellationToken);
            return new MuteCacheEntry { Id = result };
        }, this._cacheEntryOptions);
    }

    public async Task SetMuteRole(RoleId muteRoleId, GuildId guildId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var result = await dbContext
            .ModerationSettings
            .AsNoTracking()
            .Where(reward => reward.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken) ?? new ModerationSettings { GuildId = guildId };

        result.MuteRole = muteRoleId;
        await dbContext.AddAsync(result, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var cacheKey = string.Format(MuteRoleCacheKeyPrefix, guildId);
        this._memoryCache.Remove(cacheKey);
    }

    public async Task<bool> IsMemberMuted(
        UserId userId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Mutes
            .AsNoTracking()
            .AnyAsync(x =>
                    x.UserId == userId
                    && x.GuildId == guildId
                    && x.EndTime > DateTime.UtcNow,
                cancellationToken);
    }

    public async Task AddMute(
        UserId userId,
        GuildId guildId,
        SinId sinId,
        DateTimeOffset muteEndTime,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLock = await dbContext.Mutes
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLock is not null) dbContext.Mutes.Remove(existingLock);
        var newLock = new Mute { UserId = userId, GuildId = guildId, EndTime = muteEndTime, SinId = sinId };
        dbContext.Mutes.Add(newLock);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Mute?> RemoveMute(UserId userId, GuildId guildId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingMute = await dbContext.Mutes
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingMute is null)
            return null;
        dbContext.Mutes.Remove(existingMute);
        await dbContext.SaveChangesAsync(cancellationToken);
        return existingMute;
    }

    public async IAsyncEnumerable<Mute> GetAllExpiredMutes(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expiredMutes in dbContext.Mutes
                           .AsNoTracking()
                           .Where(x => x.EndTime <= DateTime.UtcNow)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expiredMutes;
    }

    public async IAsyncEnumerable<Mute> GetAllMutes(GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expiredMutes in dbContext.Mutes
                           .AsNoTracking()
                           .Where(mute => mute.GuildId == guildId)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expiredMutes;
    }

    private record struct MuteCacheEntry
    {
        public RoleId? Id { get; init; }
    }
}
