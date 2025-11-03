// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Shared;
using Grimoire.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task SetModuleState(Module moduleType, GuildId guildId, bool enableModule,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        IModule? module = moduleType switch
        {
            Module.Leveling => await dbContext.LevelingSettings
                .Where(settings => settings.GuildId == guildId)
                .FirstOrDefaultAsync(cancellationToken) ?? new LevelingSettings { GuildId = guildId },
            Module.UserLog => await dbContext.UserLogSettings
                .Where(settings => settings.GuildId == guildId)
                .FirstOrDefaultAsync(cancellationToken) ?? new UserLogSettings { GuildId = guildId },
            Module.Moderation => await dbContext.ModerationSettings
                .Where(settings => settings.GuildId == guildId)
                .FirstOrDefaultAsync(cancellationToken) ?? new ModerationSettings { GuildId = guildId },
            Module.MessageLog => await dbContext.MessageLogSettings
                .Where(settings => settings.GuildId == guildId)
                .FirstOrDefaultAsync(cancellationToken) ?? new MessageLogSettings { GuildId = guildId },
            Module.Commands => await dbContext.CustomCommandsSettings
                .Where(settings => settings.GuildId == guildId)
                .FirstOrDefaultAsync(cancellationToken) ?? new CustomCommandsSettings { GuildId = guildId },
            Module.General => null,
            _ => throw new ArgumentOutOfRangeException(nameof(moduleType), moduleType, "Unknown module type")
        };

        if (module is null)
            return;

        module.ModuleEnabled = enableModule;

        await dbContext.AddAsync(module, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        this._memoryCache.Remove(moduleType.GetCacheKey(guildId));
    }

    public async Task<bool> IsModuleEnabled(Module moduleType, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        return await this._memoryCache.GetOrCreateAsync(moduleType.GetCacheKey(guildId),
            async _ =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
                return moduleType switch
                {
                    Module.Leveling => await dbContext.LevelingSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.ModuleEnabled)
                        .FirstOrDefaultAsync(cancellationToken),
                    Module.UserLog => await dbContext.UserLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.ModuleEnabled)
                        .FirstOrDefaultAsync(cancellationToken),
                    Module.Moderation => await dbContext.ModerationSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.ModuleEnabled)
                        .FirstOrDefaultAsync(cancellationToken),
                    Module.MessageLog => await dbContext.MessageLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.ModuleEnabled)
                        .FirstOrDefaultAsync(cancellationToken),
                    Module.Commands => await dbContext.CustomCommandsSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.ModuleEnabled)
                        .FirstOrDefaultAsync(cancellationToken),
                    Module.General => true,
                    _ => throw new ArgumentOutOfRangeException(nameof(moduleType), moduleType, "Unknown module type")
                };
            }, this._cacheEntryOptions);
    }

    public async Task<GuildModuleState> GetAllModuleState(GuildId guildId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.LevelingSettings
            .Where(settings => settings.GuildId == guildId)
            .Select(settings => new GuildModuleState
            {
                LevelingEnabled = settings.ModuleEnabled,
                // ReSharper disable AccessToDisposedClosure
                UserLogEnabled = dbContext.UserLogSettings
                    .Where(x => x.GuildId == guildId)
                    .Select(x => x.ModuleEnabled).First(),
                ModerationEnabled = dbContext.UserLogSettings
                    .Where(x => x.GuildId == guildId)
                    .Select(x => x.ModuleEnabled).First(),
                MessageLogEnabled = dbContext.UserLogSettings
                    .Where(x => x.GuildId == guildId)
                    .Select(x => x.ModuleEnabled).First(),
                CommandsEnabled = dbContext.UserLogSettings
                    .Where(x => x.GuildId == guildId)
                    .Select(x => x.ModuleEnabled).First()
                // ReSharper restore AccessToDisposedClosure
            })
            .FirstOrDefaultAsync(cancellationToken) ?? new GuildModuleState
        {
            LevelingEnabled = false,
            UserLogEnabled = false,
            ModerationEnabled = false,
            MessageLogEnabled = false,
            CommandsEnabled = false
        };
    }

    public record GuildModuleState
    {
        public required bool LevelingEnabled { get; init; }
        public required bool UserLogEnabled { get; init; }
        public required bool ModerationEnabled { get; init; }
        public required bool MessageLogEnabled { get; init; }
        public required bool CommandsEnabled { get; init; }
    }
}
