// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    /// <summary>Turns a module on or off. Only the Modules document changes, so the module's settings stay (C13).</summary>
    public Task<Fin<Write<bool>>> SetModuleState(
        Module moduleType,
        GuildId guildId,
        ModeratorId moderatorId,
        bool enableModule,
        CancellationToken cancellationToken = default)
        => moduleType == Module.General
            ? Task.FromResult(Fin.Fail<Write<bool>>(new Invalid("Cannot disable the general module.")))
            : UpdateDocument<ModuleStates>(guildId, moderatorId,
                    modules => modules.With(moduleType, enableModule), cancellationToken)
                .MapFin(write => write.Map(_ => enableModule));

    public Task<Fin<bool>> IsModuleEnabled(
        Module moduleType,
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => moduleType == Module.General
            ? Task.FromResult(Fin.Succ<bool>(true))
            : GetDocument<ModuleStates>(guildId, cancellationToken)
                .MapFin(modules => modules.IsEnabled(moduleType));

    /// <summary>All six module flags, from the one Modules document.</summary>
    public Task<Fin<ModuleStates>> GetAllModuleState(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetDocument<ModuleStates>(guildId, cancellationToken);
}
