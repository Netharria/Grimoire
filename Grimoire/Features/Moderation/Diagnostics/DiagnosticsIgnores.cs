// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Diagnostics;

/// <summary>Roles and members that a guild has asked the diagnostics to leave out.</summary>
public sealed record DiagnosticsIgnores(IReadOnlySet<ulong> RoleIds, IReadOnlySet<ulong> MemberIds)
{
    public static DiagnosticsIgnores Empty { get; } = new(new HashSet<ulong>(), new HashSet<ulong>());

    public static DiagnosticsIgnores From(IEnumerable<DiagnosticsIgnoredItem> items)
    {
        var all = items.ToList();
        return new DiagnosticsIgnores(
            all.OfType<DiagnosticsIgnoredRole>().Select(role => role.RoleId.Value).ToFrozenSet(),
            all.OfType<DiagnosticsIgnoredMember>().Select(member => member.UserId.Value).ToFrozenSet());
    }
}

public static class DiagnosticsIgnoresSettingsExtensions
{
    extension(SettingsModule settingsModule)
    {
        /// <summary>The guild's ignore list. If it cannot be loaded, nothing is ignored.</summary>
        public async Task<DiagnosticsIgnores> GetDiagnosticsIgnores(GuildId guildId)
            => await settingsModule.GetDiagnosticsIgnoredItemsAsync(guildId)
                .Map(DiagnosticsIgnores.From)
                .GetOrElse(() => DiagnosticsIgnores.Empty);
    }
}
