// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public Task<Fin<IReadOnlySet<DiagnosticsTarget>>> GetDiagnosticsIgnoredItemsAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ReadDocument<DiagnosticsIgnoreList, IReadOnlySet<DiagnosticsTarget>>(guildId,
            ignores => ignores.Targets,
            cancellationToken);

    /// <summary>Adds each target that is not already ignored. Saved holds only the ones that were added.</summary>
    public Task<Fin<Write<IReadOnlyList<DiagnosticsTarget>>>> AddDiagnosticsIgnoredItemsAsync(
        GuildId guildId,
        ModeratorId setBy,
        IReadOnlyCollection<DiagnosticsTarget> targets,
        CancellationToken cancellationToken = default)
        => targets.Count == 0
            ? Task.FromResult<Fin<Write<IReadOnlyList<DiagnosticsTarget>>>>(
                new Write<IReadOnlyList<DiagnosticsTarget>>.Unchanged("No roles or members were provided to ignore."))
            : UpdateDocument<DiagnosticsIgnoreList, IReadOnlyList<DiagnosticsTarget>>(guildId, setBy,
                ignores => ignores.Add(targets),
                "Those roles and members are already ignored.", cancellationToken,
                "Could not save the diagnostics ignore list.");

    /// <summary>
    ///     Removes each target that is currently ignored, and returns those. NotFound when none were, as before
    ///     spec 2.5.
    /// </summary>
    public async Task<Fin<IReadOnlyList<DiagnosticsTarget>>> RemoveDiagnosticsIgnoredItemsAsync(
        GuildId guildId,
        ModeratorId setBy,
        IReadOnlyCollection<DiagnosticsTarget> targets,
        CancellationToken cancellationToken = default)
        => (await UpdateDocument<DiagnosticsIgnoreList, IReadOnlyList<DiagnosticsTarget>>(guildId, setBy,
                ignores => ignores.Remove(targets),
                "None of those roles or members are ignored.", cancellationToken,
                "Could not update the diagnostics ignore list."))
            .Bind(write => write.Match<Fin<IReadOnlyList<DiagnosticsTarget>>>(
                saved: removed => Fin.Succ(removed),
                unchanged: message => new NotFound(message)));

    /// <summary>
    ///     Removes a role from the ignore list, for when Discord deletes it: a deleted role can't be picked in
    ///     <c>/Diagnostics Ignore Remove</c>, so it would stay on the list for good (KI-62). NotFound when it wasn't
    ///     ignored. <paramref name="setBy" /> is the bot, since a version needs a who.
    /// </summary>
    public async Task<Fin<Unit>> RemoveDiagnosticsIgnoredRoleAsync(
        GuildId guildId,
        ModeratorId setBy,
        RoleId roleId,
        CancellationToken cancellationToken = default)
        => (await RemoveDiagnosticsIgnoredItemsAsync(guildId, setBy, [new DiagnosticsTarget.Role(roleId)],
                cancellationToken))
            .Map(_ => unit)
            .MapFail(error => error is NotFound ? new NotFound("That role is not ignored.") : error);
}
