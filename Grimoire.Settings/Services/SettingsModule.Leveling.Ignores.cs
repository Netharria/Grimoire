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
    /// <param name="parentChannelId">The channel a thread is in, so ignoring a channel also covers its threads.</param>
    public Task<Fin<bool>> IsMessageIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        ChannelId channelId,
        ChannelId? parentChannelId = null,
        CancellationToken cancellationToken = default)
        => ReadDocument<XpExclusions, bool>(guildId,
            exclusions => exclusions.ExcludesMessage(userId, userRoleIds, channelId, parentChannelId),
            cancellationToken);

    public Task<Fin<bool>> IsMemberIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        CancellationToken cancellationToken = default)
        => ReadDocument<XpExclusions, bool>(guildId,
            exclusions => exclusions.ExcludesMember(userId, userRoleIds),
            cancellationToken);

    /// <remarks>A failure is returned, not an empty set, so a caller can't mistake an outage for "nothing ignored".</remarks>
    public Task<Fin<IReadOnlySet<XpTarget>>> GetAllIgnoredItems(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ReadDocument<XpExclusions, IReadOnlySet<XpTarget>>(guildId,
            exclusions => exclusions.Targets,
            cancellationToken);

    /// <summary>
    ///     Leaves the targets out of XP gain. Saved holds only the ones that weren't already left out (X-12); Unchanged
    ///     when none, so nothing is saved and nothing is reported as changed.
    /// </summary>
    public Task<Fin<Write<IReadOnlySet<XpTarget>>>> IgnoreXpAsync(
        GuildId guildId,
        ModeratorId setBy,
        IReadOnlySet<XpTarget> targets,
        CancellationToken cancellationToken = default)
        => targets.Count == 0
            ? Task.FromResult<Fin<Write<IReadOnlySet<XpTarget>>>>(
                new Write<IReadOnlySet<XpTarget>>.Unchanged("No items were provided to ignore."))
            : UpdateDocument<XpExclusions, IReadOnlySet<XpTarget>>(guildId, setBy,
                exclusions => exclusions.Add(targets),
                "Nothing changed: every item was already in that state.", cancellationToken,
                "Could not save the ignored items.");

    /// <summary>Lets the targets earn XP again. Saved holds only the ones that were left out; Unchanged when none.</summary>
    public Task<Fin<Write<IReadOnlySet<XpTarget>>>> UnignoreXpAsync(
        GuildId guildId,
        ModeratorId setBy,
        IReadOnlySet<XpTarget> targets,
        CancellationToken cancellationToken = default)
        => targets.Count == 0
            ? Task.FromResult<Fin<Write<IReadOnlySet<XpTarget>>>>(
                new Write<IReadOnlySet<XpTarget>>.Unchanged("No items were provided to ignore."))
            : UpdateDocument<XpExclusions, IReadOnlySet<XpTarget>>(guildId, setBy,
                exclusions => exclusions.Remove(targets),
                "Nothing changed: every item was already in that state.", cancellationToken,
                "Could not save the ignored items.");
}
