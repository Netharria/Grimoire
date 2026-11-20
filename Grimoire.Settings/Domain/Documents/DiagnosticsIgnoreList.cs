// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace Grimoire.Settings.Domain.Documents;

/// <summary>A role or member the moderation diagnostics can leave out (DD-8's diagnostics equivalent).</summary>
public abstract record DiagnosticsTarget
{
    private DiagnosticsTarget() { }

    public sealed record Role(RoleId Id) : DiagnosticsTarget;

    public sealed record Member(UserId Id) : DiagnosticsTarget;
}

/// <summary>
///     The roles and members the moderation diagnostics leave out, for example moderators who are expected to be able
///     to speak in locked channels (C14, spec 2.5).
/// </summary>
public sealed record DiagnosticsIgnoreList : ISettingsDocument<DiagnosticsIgnoreList>
{
    public Set<RoleId> Roles { get; init; }
    public Set<UserId> Members { get; init; }

    public static SettingsDocument Kind => SettingsDocument.DiagnosticsIgnores;
    public static DiagnosticsIgnoreList Empty { get; } = new();

    /// <remarks>
    ///     <c>Enumerable.Select</c>, not <c>Set.Select</c>, which would build another sorted set of targets, and
    ///     targets have no order.
    /// </remarks>
    [JsonIgnore]
    public IReadOnlySet<DiagnosticsTarget> Targets
        => Enumerable.Select(this.Roles, DiagnosticsTarget (id) => new DiagnosticsTarget.Role(id))
            .Concat(Enumerable.Select(this.Members, DiagnosticsTarget (id) => new DiagnosticsTarget.Member(id)))
            .ToFrozenSet();

    public bool Ignores(DiagnosticsTarget target)
        => target switch
        {
            DiagnosticsTarget.Role role => this.Roles.Contains(role.Id),
            DiagnosticsTarget.Member member => this.Members.Contains(member.Id),
            _ => throw new InvariantViolationException($"Unknown diagnostics target {target.GetType().Name}.")
        };

    /// <summary>Ignores the targets. Returns only the ones that weren't ignored yet, in the order given.</summary>
    public (DiagnosticsIgnoreList Document, IReadOnlyList<DiagnosticsTarget> Added) Add(
        IEnumerable<DiagnosticsTarget> targets)
    {
        var added = targets.Distinct().Where(target => !this.Ignores(target)).ToList();
        return (added.Aggregate(this, (document, target) => document.With(target, true)), added);
    }

    /// <summary>Stops ignoring the targets. Returns only the ones that were ignored, in the order given.</summary>
    public (DiagnosticsIgnoreList Document, IReadOnlyList<DiagnosticsTarget> Removed) Remove(
        IEnumerable<DiagnosticsTarget> targets)
    {
        var removed = targets.Distinct().Where(this.Ignores).ToList();
        return (removed.Aggregate(this, (document, target) => document.With(target, false)), removed);
    }

    private DiagnosticsIgnoreList With(DiagnosticsTarget target, bool ignored)
        => target switch
        {
            DiagnosticsTarget.Role role => this with
            {
                Roles = ignored ? this.Roles.AddOrUpdate(role.Id) : this.Roles.Remove(role.Id)
            },
            DiagnosticsTarget.Member member => this with
            {
                Members = ignored ? this.Members.AddOrUpdate(member.Id) : this.Members.Remove(member.Id)
            },
            _ => throw new InvariantViolationException($"Unknown diagnostics target {target.GetType().Name}.")
        };
}
