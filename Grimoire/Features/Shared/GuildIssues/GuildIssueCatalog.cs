// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.GuildIssues;

/// <summary>The known moderator-fixable problems and what to tell moderators to do about them.</summary>
public static class GuildIssueCatalog
{
    private static readonly IReadOnlyDictionary<string, string> _permissionForCommand =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mute"] = "Manage Roles",
            ["Unmute"] = "Manage Roles",
            ["Mutes"] = "Manage Roles",
            ["Kick"] = "Kick Members",
            ["Ban"] = "Ban Members",
            ["Unban"] = "Ban Members",
            ["Lock"] = "Manage Channels",
            ["Unlock"] = "Manage Channels",
            ["Purge"] = "Manage Messages"
        };

    public static GuildIssue BotMissingPermissions(GuildId guildId, string command)
    {
        var permission = _permissionForCommand.TryGetValue(command.Split(' ')[0], out var required)
            ? $"Make sure my role has the **{required}** permission. "
            : "Make sure my role has the permissions this command needs. ";

        return new GuildIssue(guildId, $"bot-missing-permissions:{command}",
            $"Discord refused `{command}`: I do not have permission to do that.",
            permission
            + "If it is about a specific member or role, move my role above theirs in Server Settings > Roles. "
            + "Channel permission overwrites for my role can also block it.");
    }

    public static GuildIssue RewardRolesNotGrantable(GuildId guildId, IEnumerable<RoleId> roles)
        => new(guildId, "leveling.reward-roles-forbidden",
            $"I could not give the level reward roles {Mentions(roles)}.",
            "Give my role the **Manage Roles** permission and move it above the reward roles in Server Settings > Roles.");

    public static GuildIssue RewardRolesMissing(GuildId guildId, IEnumerable<RoleId> roles)
        => new(guildId, "leveling.reward-roles-missing",
            $"Level rewards point at roles that no longer exist: {string.Join(' ', roles.Select(r => $"`{r}`"))}.",
            "Remove those rewards with the reward commands, or re-create the roles and add the rewards again.");

    public static GuildIssue CannotSendInChannel(GuildId guildId, ChannelId channelId)
        => new(guildId, $"channel.cannot-send:{channelId}",
            $"I could not post in <#{channelId}>.",
            "Give my role **View Channel** and **Send Messages** in that channel, and check its permission overwrites.");

    public static GuildIssue CannotReadAuditLog(GuildId guildId)
        => new(guildId, "audit-log.forbidden",
            "I cannot read the audit log, so I cannot tell who deleted messages or banned members outside my commands.",
            "Give my role the **View Audit Log** permission.");

    private static string Mentions(IEnumerable<RoleId> roles) => string.Join(' ', roles.Select(RoleExtensions.Mention));
}
