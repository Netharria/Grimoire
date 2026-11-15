// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<Fin<bool>> ShouldLogMessage(ChannelId channelId,
        GuildId guildId,
        IReadOnlyDictionary<ChannelId, ChannelId?> channelNodes,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.MessageLog, guildId, cancellationToken)
                .OrDefaultDuringMigration(false))
            return Fin.Succ<bool>(false);

        ChannelId? currentChannelId = channelId;
        while (currentChannelId is not null)
        {
            var overrideOption = await GetChannelLogOverride(currentChannelId.Value, guildId, cancellationToken)
                .OrDefaultDuringMigration(MessageLogOverrideOption.Inherit);
            switch (overrideOption)
            {
                case MessageLogOverrideOption.AlwaysLog:
                    return Fin.Succ<bool>(true);
                case MessageLogOverrideOption.NeverLog:
                    return Fin.Succ<bool>(false);
                case MessageLogOverrideOption.Inherit:
                default:
                    currentChannelId = channelNodes.GetValueOrDefault(currentChannelId.Value);
                    break;
            }
        }

        return Fin.Succ<bool>(true);
    }

    private Task<Fin<MessageLogOverrideOption>> GetChannelLogOverride(ChannelId channelId,
        GuildId guildId,
        CancellationToken cancellationToken)
        => ExecuteSafelyAsync(async ct =>
            {
                var result = await this._cache.GetOrCreateAsync(CacheKey.LogOverride(channelId),
                    new { channelId, guildId },
                    async (state, innerCt) =>
                    {
                        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(innerCt);
                        var channelOverride = await dbContext.MessageLogChannelOverrides
                            .AsNoTracking()
                            .Where(ovr => ovr.GuildId == state.guildId && ovr.ChannelId == state.channelId)
                            .OrderByDescending(x => x.SetAt)
                            .Select(ovr => (MessageLogOverrideOption?)ovr.ChannelOption)
                            .FirstOrDefaultAsync(innerCt);
                        return channelOverride ?? MessageLogOverrideOption.Inherit;
                    }, this._cacheEntryOptions,
                    cancellationToken: ct);
                return Fin.Succ<MessageLogOverrideOption>(result);
            }, "Could not retrieve channel log override.",
            cancellationToken);

    public Task<Fin<Write<MessageLogChannelOverride>>> SetChannelLogOverride(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        MessageLogOverrideOption option,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync<Write<MessageLogChannelOverride>>(async ct =>
        {
            var existingOverride = await GetChannelLogOverride(channelId, guildId, ct)
                .OrDefaultDuringMigration(MessageLogOverrideOption.Inherit);

            if (existingOverride == option)
                return new Write<MessageLogChannelOverride>.Unchanged("The channel is already set to this log option.");

            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);

            var newOverride = MessageLogChannelOverride.Create(option, channelId, guildId, setBy, DateTimeOffset.UtcNow);
            dbContext.MessageLogChannelOverrides.Add(newOverride);
            await dbContext.SaveChangesAsync(ct);
            await this._cache.SetAsync(CacheKey.LogOverride(channelId), option, this._cacheEntryOptions,
                cancellationToken: ct);
            return new Write<MessageLogChannelOverride>.Saved(newOverride);
        }, "Could not set channel log override.", cancellationToken);

    public async IAsyncEnumerable<MessageLogChannelOverride> GetAllOverriddenChannels(GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<MessageLogChannelOverride> overrides;
        try
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            // EF Core cannot translate a .Where() after GroupBy().Select(g => g.First()), so the latest row per
            // channel is loaded and filtered in memory.
            overrides = await dbContext.MessageLogChannelOverrides
                .AsNoTracking()
                .Where(ovr => ovr.GuildId == guildId)
                .GroupBy(ovr => ovr.ChannelId)
                .Select(ovr => ovr.OrderByDescending(x => x.SetAt).First())
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            yield break;
        }

        foreach (var channelOverride in overrides
                     .Where(channelOverride => channelOverride.ChannelOption != MessageLogOverrideOption.Inherit))
            yield return channelOverride;
    }
}
