// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Diagnostics;

/// <summary>Works out where the configured mute role and channel locks would fail to stop someone.</summary>
public static class ModerationDiagnostics
{
    public static DiagnosticsResult Analyze(GuildSnapshot guild, DiagnosticsIgnores ignores)
    {
        var ignoredRoleIds = ignores.RoleIds;
        var administrators = guild.Roles
            .Where(role => role.Id != guild.GuildId && role.Permissions.HasPermission(DiscordPermission.Administrator))
            .ToList();
        var ignored = guild.Roles
            .Where(role => role.Id != guild.GuildId && ignoredRoleIds.Contains(role.Id)
                                                     && !administrators.Contains(role))
            .ToList();

        var skipped = administrators.Concat(ignored).Select(role => role.Id).ToHashSet();
        var candidates = guild.Roles
            .Where(role => role.Id != guild.GuildId && role.Id != guild.MuteRoleId && !skipped.Contains(role.Id))
            .ToList();

        var muteRoleExists = guild.MuteRoleId is { } id && guild.Roles.Any(role => role.Id == id);

        return new DiagnosticsResult(
            administrators,
            ignored,
            [.. ignores.MemberIds.Order()],
            [.. CheckGuild(guild, muteRoleExists)],
            [
                .. guild.Channels.SelectMany(channel =>
                    CheckMutes(guild, channel, candidates, muteRoleExists, ignores.MemberIds)),
                .. guild.Channels.SelectMany(channel => CheckLocks(guild, channel, candidates, ignores.MemberIds))
            ]);
    }

    private static IEnumerable<GuildFinding> CheckGuild(GuildSnapshot guild, bool muteRoleExists)
    {
        if (guild.MuteRoleId is null)
            yield return new GuildFinding(GuildFindingKind.MuteRoleNotConfigured);
        else if (!muteRoleExists)
            yield return new GuildFinding(GuildFindingKind.MuteRoleNotFound);

        if (!HasPermission(guild.BotPermissions, DiscordPermission.ManageRoles))
            yield return new GuildFinding(GuildFindingKind.BotMissingManageRoles);
        else if (guild.MuteRolePosition is { } position && position >= guild.BotTopRolePosition)
            yield return new GuildFinding(GuildFindingKind.MuteRoleAboveBot);

        if (!HasPermission(guild.BotPermissions, DiscordPermission.ManageChannels))
            yield return new GuildFinding(GuildFindingKind.BotMissingManageChannels);
    }

    private static IEnumerable<ChannelFinding> CheckMutes(GuildSnapshot guild, ChannelSnapshot channel,
        IReadOnlyList<RoleSnapshot> candidates, bool muteRoleExists, IReadOnlySet<ulong> ignoredMemberIds)
    {
        if (!muteRoleExists || guild.MuteRoleId is not { } muteRoleId) yield break;

        var relevant = RelevantPermissions(channel);
        HashSet<ulong> muted = [muteRoleId];

        if (EffectivePermissions.For(guild.Roles, guild.GuildId, channel, muted).HasAnyPermission(relevant))
        {
            // Every other role would leak too, so one finding is enough.
            yield return new ChannelFinding(channel, ChannelFindingKind.MuteRoleNotDenied, []);
            yield break;
        }

        var leakingRoles = candidates
            .Where(role => EffectivePermissions.For(guild.Roles, guild.GuildId, channel, new HashSet<ulong> { muteRoleId, role.Id })
                .HasAnyPermission(relevant))
            .Select(role => role.Id)
            .ToList();
        if (leakingRoles.Count > 0)
            yield return new ChannelFinding(channel, ChannelFindingKind.RoleCanSpeakWhenMuted, leakingRoles);

        var leakingMembers = MembersAllowed(channel, relevant, ignoredMemberIds);
        if (leakingMembers.Count > 0)
            yield return new ChannelFinding(channel, ChannelFindingKind.MemberCanSpeakWhenMuted, leakingMembers);
    }

    private static IEnumerable<ChannelFinding> CheckLocks(GuildSnapshot guild, ChannelSnapshot channel,
        IReadOnlyList<RoleSnapshot> candidates, IReadOnlySet<ulong> ignoredMemberIds)
    {
        // Locking only applies to text-like channels.
        if (channel.Kind is not ChannelKind.Text) yield break;

        if (!channel.BotCanManage)
            yield return new ChannelFinding(channel, ChannelFindingKind.BotCannotLock, []);

        var relevant = RelevantPermissions(channel);
        var locked = channel with { Overwrites = ApplyLock(channel.Overwrites, guild.GuildId, relevant) };

        var leakingRoles = candidates
            .Where(role => EffectivePermissions.For(guild.Roles, guild.GuildId, locked, new HashSet<ulong> { role.Id })
                .HasAnyPermission(relevant))
            .Select(role => role.Id)
            .ToList();
        if (leakingRoles.Count > 0)
            yield return new ChannelFinding(channel, ChannelFindingKind.RoleCanSpeakWhenLocked, leakingRoles);

        var leakingMembers = MembersAllowed(channel, relevant, ignoredMemberIds);
        if (leakingMembers.Count > 0)
            yield return new ChannelFinding(channel, ChannelFindingKind.MemberCanSpeakWhenLocked, leakingMembers);
    }

    /// <summary>What locking does to the channel: deny the lock permissions for @everyone.</summary>
    private static IReadOnlyList<OverwriteSnapshot> ApplyLock(IReadOnlyList<OverwriteSnapshot> overwrites,
        ulong guildId, DiscordPermissions lockPermissions)
    {
        var everyone = overwrites.FirstOrDefault(o => o.Target == OverwriteTarget.Role && o.Id == guildId)
                       ?? new OverwriteSnapshot(guildId, OverwriteTarget.Role, DiscordPermissions.None,
                           DiscordPermissions.None);
        var locked = everyone with
        {
            Allowed = everyone.Allowed & ~lockPermissions, Denied = everyone.Denied | lockPermissions
        };
        return [.. overwrites.Where(o => o != everyone), locked];
    }

    private static List<ulong> MembersAllowed(ChannelSnapshot channel, DiscordPermissions relevant,
        IReadOnlySet<ulong> ignoredMemberIds)
        => channel.Overwrites
            .Where(o => o.Target == OverwriteTarget.Member && !ignoredMemberIds.Contains(o.Id)
                                                            && o.Allowed.HasAnyPermission(relevant))
            .Select(o => o.Id)
            .ToList();

    private static DiscordPermissions RelevantPermissions(ChannelSnapshot channel)
        => channel.Kind is ChannelKind.Text ? PermissionValues.LockPermissions : PermissionValues.VoiceLockPermissions;

    // Administrator implies every permission.
    private static bool HasPermission(DiscordPermissions permissions, DiscordPermission permission)
        => permissions.HasPermission(DiscordPermission.Administrator) || permissions.HasPermission(permission);
}
