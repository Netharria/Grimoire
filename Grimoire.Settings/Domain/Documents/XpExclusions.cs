// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace Grimoire.Settings.Domain.Documents;

/// <summary>Something that can be left out of XP gain (DD-8): a channel, a member or a role.</summary>
public abstract record XpTarget
{
    private XpTarget() { }

    public sealed record Channel(ChannelId Id) : XpTarget;

    public sealed record Member(UserId Id) : XpTarget;

    public sealed record Role(RoleId Id) : XpTarget;
}

/// <summary>
///     The channels, members and roles a guild leaves out of XP gain (C14, spec 2.5). Ignoring a channel also covers
///     its threads (X-12).
/// </summary>
public sealed record XpExclusions : ISettingsDocument<XpExclusions>
{
    public Set<ChannelId> Channels { get; init; }
    public Set<UserId> Members { get; init; }
    public Set<RoleId> Roles { get; init; }

    public static SettingsDocument Kind => SettingsDocument.XpExclusions;
    public static XpExclusions Empty { get; } = new();

    /// <remarks>
    ///     <c>Enumerable.Select</c>, not <c>Set.Select</c>, which would build another sorted set of targets, and
    ///     targets have no order.
    /// </remarks>
    [JsonIgnore]
    public IReadOnlySet<XpTarget> Targets
        => Enumerable.Select(this.Channels, XpTarget (id) => new XpTarget.Channel(id))
            .Concat(Enumerable.Select(this.Members, XpTarget (id) => new XpTarget.Member(id)))
            .Concat(Enumerable.Select(this.Roles, XpTarget (id) => new XpTarget.Role(id)))
            .ToFrozenSet();

    public bool Excludes(XpTarget target)
        => target switch
        {
            XpTarget.Channel channel => this.Channels.Contains(channel.Id),
            XpTarget.Member member => this.Members.Contains(member.Id),
            XpTarget.Role role => this.Roles.Contains(role.Id),
            _ => throw new InvariantViolationException($"Unknown XP target {target.GetType().Name}.")
        };

    /// <summary>Whether a member, by themselves or one of their roles, is left out. Channels don't count.</summary>
    public bool ExcludesMember(UserId member, IReadOnlySet<RoleId> roles)
        => this.Members.Contains(member) || roles.Any(this.Roles.Contains);

    /// <param name="parent">The channel a thread is in, so ignoring a channel also covers its threads.</param>
    public bool ExcludesMessage(UserId member, IReadOnlySet<RoleId> roles, ChannelId channel, ChannelId? parent)
        => this.Channels.Contains(channel)
           || parent is { } parentId && this.Channels.Contains(parentId)
           || this.ExcludesMember(member, roles);

    /// <summary>Leaves the targets out. Returns only the ones that weren't already left out.</summary>
    public (XpExclusions Document, IReadOnlySet<XpTarget> Added) Add(IEnumerable<XpTarget> targets)
    {
        var added = targets.Where(target => !this.Excludes(target)).ToFrozenSet();
        return (added.Aggregate(this, (document, target) => document.With(target, true)), added);
    }

    /// <summary>Lets the targets earn XP again. Returns only the ones that were left out.</summary>
    public (XpExclusions Document, IReadOnlySet<XpTarget> Removed) Remove(IEnumerable<XpTarget> targets)
    {
        var removed = targets.Where(this.Excludes).ToFrozenSet();
        return (removed.Aggregate(this, (document, target) => document.With(target, false)), removed);
    }

    private XpExclusions With(XpTarget target, bool excluded)
        => target switch
        {
            XpTarget.Channel channel => this with
            {
                Channels = excluded ? this.Channels.AddOrUpdate(channel.Id) : this.Channels.Remove(channel.Id)
            },
            XpTarget.Member member => this with
            {
                Members = excluded ? this.Members.AddOrUpdate(member.Id) : this.Members.Remove(member.Id)
            },
            XpTarget.Role role => this with
            {
                Roles = excluded ? this.Roles.AddOrUpdate(role.Id) : this.Roles.Remove(role.Id)
            },
            _ => throw new InvariantViolationException($"Unknown XP target {target.GetType().Name}.")
        };
}
