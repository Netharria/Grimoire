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
    /// <summary>30 years: in practice, sins are never pardoned automatically unless a guild sets a duration.</summary>
    public static TimeSpan DefaultAutoPardonDuration { get; } = TimeSpan.FromDays(10950);

    public Task<Fin<TimeSpan>> GetAutoPardonDuration(GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetDocument<ModerationConfig>(guildId, cancellationToken)
            .MapFin(moderation => moderation.AutoPardonAfter is { } span && span != TimeSpan.Zero
                ? span
                : DefaultAutoPardonDuration);

    public Task<Fin<Write<TimeSpan>>> SetAutoPardonDuration(
        GuildId guildId,
        ModeratorId moderatorId,
        TimeSpan autoPardonAfter,
        CancellationToken cancellationToken = default)
        => UpdateDocument<ModerationConfig>(guildId, moderatorId,
                moderation => moderation with { AutoPardonAfter = autoPardonAfter }, cancellationToken)
            .MapFin(write => write.Map(_ => autoPardonAfter));
}
