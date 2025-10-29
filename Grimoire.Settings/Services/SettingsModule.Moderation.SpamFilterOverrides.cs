// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<SpamFilterOverrideOption?> GetSpamFilterOverrideAsync(ulong channelId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SpamFilterOverrides
            .AsNoTracking()
            .Where(x => x.ChannelId == channelId)
            .Select(x => x.ChannelOption)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async IAsyncEnumerable<SpamFilterOverride> GetAllSpamFilterOverrideAsync(ulong guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        await foreach (var spamFilterOverride in dbContext.SpamFilterOverrides
                           .AsNoTracking()
                           .Where(x => x.GuildId == guildId)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return spamFilterOverride;
    }

    public async Task SetSpamFilterOverrideAsync(ulong channelId, ulong guildId, SpamFilterOverrideOption option,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var spamFilterOverride = await dbContext.SpamFilterOverrides
                                     .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.GuildId == guildId,
                                         cancellationToken)
                                 ?? new SpamFilterOverride { ChannelId = channelId, GuildId = guildId };
        spamFilterOverride.ChannelOption = option;
        await dbContext.SpamFilterOverrides.AddAsync(spamFilterOverride, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveSpamFilterOverrideAsync(ulong channelId, ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var spamFilterOverride = await dbContext.SpamFilterOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.GuildId == guildId, cancellationToken);
        if (spamFilterOverride is null)
            return;
        dbContext.SpamFilterOverrides.Remove(spamFilterOverride);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
