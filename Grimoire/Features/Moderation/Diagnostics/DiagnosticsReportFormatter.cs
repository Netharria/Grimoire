// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Features.Moderation.Diagnostics;

public static class DiagnosticsReportFormatter
{
    private const int MaxEmbedDescriptionLength = 4000;

    public static string BuildText(GuildSnapshot guild, DiagnosticsResult result)
    {
        var text = new StringBuilder("Moderation diagnostics\n======================\n\n");
        AppendExcludedRoles(text, result);

        if (!result.HasFindings)
            return text.Append("No problems found.\n").ToString();

        if (result.GuildFindings.Count > 0)
        {
            text.Append("Setup problems\n--------------\n");
            foreach (var finding in result.GuildFindings)
            {
                var (problem, fix) = Describe(finding.Kind);
                text.Append("- ").Append(problem).Append("\n  Fix: ").Append(fix).Append('\n');
            }

            text.Append('\n');
        }

        AppendChannelSection(text, guild, result, "Mutes may not work in these channels",
            ChannelFindingKind.MuteRoleNotDenied, ChannelFindingKind.RoleCanSpeakWhenMuted,
            ChannelFindingKind.MemberCanSpeakWhenMuted);
        AppendChannelSection(text, guild, result, "Locks may not work in these channels",
            ChannelFindingKind.BotCannotLock, ChannelFindingKind.RoleCanSpeakWhenLocked,
            ChannelFindingKind.MemberCanSpeakWhenLocked);

        return text.ToString();
    }

    public static DiscordEmbedBuilder BuildEmbed(GuildSnapshot guild, DiagnosticsResult result)
    {
        var description = new StringBuilder();
        AppendExcludedRoles(description, result);

        if (!result.HasFindings)
            description.Append("No problems found.");
        else
        {
            foreach (var finding in result.GuildFindings)
            {
                var (problem, fix) = Describe(finding.Kind);
                description.Append("- ").Append(problem).Append(" **Fix:** ").Append(fix).Append('\n');
            }

            var muteChannels = ChannelCount(result, ChannelFindingKind.MuteRoleNotDenied,
                ChannelFindingKind.RoleCanSpeakWhenMuted, ChannelFindingKind.MemberCanSpeakWhenMuted);
            var lockChannels = ChannelCount(result, ChannelFindingKind.BotCannotLock,
                ChannelFindingKind.RoleCanSpeakWhenLocked, ChannelFindingKind.MemberCanSpeakWhenLocked);
            description.Append($"\nMutes may not work in **{muteChannels}** channel(s). ")
                .Append($"Locks may not work in **{lockChannels}** channel(s). See the attached report for details.");
        }

        return new DiscordEmbedBuilder()
            .WithColor(result.HasFindings ? GrimoireColor.Yellow : GrimoireColor.Green)
            .WithTitle("Moderation diagnostics")
            .WithDescription(description.Length <= MaxEmbedDescriptionLength
                ? description.ToString()
                : description.ToString()[..(MaxEmbedDescriptionLength - 3)] + "...");
    }

    private static void AppendExcludedRoles(StringBuilder text, DiagnosticsResult result)
    {
        if (result.AdministratorRoles.Count > 0)
            text.Append("Administrator roles (excluded, Administrator ignores channel permissions): ")
                .Append(string.Join(", ", result.AdministratorRoles.Select(r => r.Name))).Append('\n');
        if (result.IgnoredRoles.Count > 0)
            text.Append("Ignored roles (excluded on request): ")
                .Append(string.Join(", ", result.IgnoredRoles.Select(r => r.Name))).Append('\n');
        if (result.IgnoredMemberIds.Count > 0)
            text.Append("Ignored members (excluded on request, user IDs): ")
                .Append(string.Join(", ", result.IgnoredMemberIds)).Append('\n');
        if (result.AdministratorRoles.Count > 0 || result.IgnoredRoles.Count > 0 || result.IgnoredMemberIds.Count > 0)
            text.Append('\n');
    }

    private static int ChannelCount(DiagnosticsResult result, params ChannelFindingKind[] kinds)
        => result.ChannelFindings.Where(f => kinds.Contains(f.Kind)).Select(f => f.Channel.Id).Distinct().Count();

    private static void AppendChannelSection(StringBuilder text, GuildSnapshot guild, DiagnosticsResult result,
        string title, params ChannelFindingKind[] kinds)
    {
        var findings = result.ChannelFindings.Where(f => kinds.Contains(f.Kind)).ToList();
        if (findings.Count == 0) return;

        text.Append(title).Append('\n').Append(new string('-', title.Length)).Append('\n');
        foreach (var channel in findings.GroupBy(f => f.Channel))
        {
            text.Append('#').Append(channel.Key.Name).Append('\n');
            foreach (var finding in channel)
                text.Append("  - ").Append(DescribeChannelFinding(guild, finding)).Append('\n');
        }

        text.Append('\n');
    }

    private static string DescribeChannelFinding(GuildSnapshot guild, ChannelFinding finding)
    {
        var roles = string.Join(", ", finding.SubjectIds.Select(id =>
            guild.Roles.FirstOrDefault(r => r.Id == id)?.Name ?? id.ToString()));
        var members = string.Join(", ", finding.SubjectIds);
        return finding.Kind switch
        {
            ChannelFindingKind.MuteRoleNotDenied =>
                "The mute role does not deny the needed permissions here, so muted members can still use this channel. "
                + "Fix: run /Mutes Refresh.",
            ChannelFindingKind.RoleCanSpeakWhenMuted =>
                $"Members with these roles can still use this channel while muted: {roles}. "
                + "Fix: remove the explicit allow for those roles in this channel, or ignore the roles if that is intended.",
            ChannelFindingKind.MemberCanSpeakWhenMuted =>
                $"These members have their own permission overwrite that overrides the mute (user IDs): {members}. "
                + "Fix: remove their overwrite in this channel.",
            ChannelFindingKind.RoleCanSpeakWhenLocked =>
                $"Members with these roles can still use this channel while it is locked: {roles}. "
                + "Fix: remove the explicit allow for those roles in this channel, or ignore the roles if that is intended.",
            ChannelFindingKind.MemberCanSpeakWhenLocked =>
                $"These members have their own permission overwrite that overrides a lock (user IDs): {members}. "
                + "Fix: remove their overwrite in this channel.",
            ChannelFindingKind.BotCannotLock =>
                "The bot does not have Manage Channels in this channel, so it cannot lock it. "
                + "Fix: check this channel's permission overwrites for the bot's role.",
            _ => throw new ArgumentOutOfRangeException(nameof(finding), finding.Kind, null)
        };
    }

    private static (string Problem, string Fix) Describe(GuildFindingKind kind) => kind switch
    {
        GuildFindingKind.MuteRoleNotConfigured => (
            "No mute role is configured.",
            "run /Mutes Create to make one, or /Mutes Set to choose an existing role."),
        GuildFindingKind.MuteRoleNotFound => (
            "The configured mute role no longer exists.",
            "run /Mutes Create to make a new one, or /Mutes Set to choose another role."),
        GuildFindingKind.BotMissingManageRoles => (
            "The bot does not have the Manage Roles permission, so it cannot mute or unmute anyone.",
            "give the bot's role Manage Roles."),
        GuildFindingKind.MuteRoleAboveBot => (
            "The mute role is at or above the bot's highest role, so the bot cannot assign it.",
            "in Server Settings > Roles, drag the bot's role above the mute role."),
        GuildFindingKind.BotMissingManageChannels => (
            "The bot does not have the Manage Channels permission, so it cannot lock channels.",
            "give the bot's role Manage Channels."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
