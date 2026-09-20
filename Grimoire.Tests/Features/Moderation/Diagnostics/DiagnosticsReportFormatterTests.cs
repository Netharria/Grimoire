// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Moderation.Diagnostics;
using static Grimoire.Tests.Features.Moderation.Diagnostics.DiagnosticsTestData;

namespace Grimoire.Tests.Features.Moderation.Diagnostics;

public sealed class DiagnosticsReportFormatterTests
{
    private static (GuildSnapshot Guild, DiagnosticsResult Result) Run(GuildSnapshot guild, params ulong[] ignored)
        => (guild, ModerationDiagnostics.Analyze(guild, new DiagnosticsIgnores(ignored.ToHashSet(), new HashSet<ulong>())));

    [Fact]
    public void Healthy_guild_reports_no_problems()
    {
        var (guild, result) = Run(Guild([Channel(overwrites: MuteDeny)]));

        DiagnosticsReportFormatter.BuildText(guild, result).ShouldContain("No problems found.");
        DiagnosticsReportFormatter.BuildEmbed(guild, result).Build().Description.ShouldNotBeNull()
            .ShouldContain("No problems found.");
    }

    [Fact]
    public void Excluded_roles_are_listed_before_any_findings()
    {
        var (guild, result) = Run(
            Guild([Channel(overwrites: [MuteDeny, Role(ModeratorRole, allow: Speak)])]), ModeratorRole);

        var text = DiagnosticsReportFormatter.BuildText(guild, result);

        text.ShouldContain("Administrator roles");
        text.ShouldContain("Admin");
        text.ShouldContain("Ignored roles");
        text.ShouldContain("Moderator");
        text.IndexOf("Ignored roles", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("No problems found.", StringComparison.Ordinal));
    }

    [Fact]
    public void Ignored_members_are_listed_with_the_excluded_roles()
    {
        var guild = Guild([Channel(overwrites: [MuteDeny, Member(999, Speak)])]);
        var result = ModerationDiagnostics.Analyze(guild,
            new DiagnosticsIgnores(new HashSet<ulong>(), new HashSet<ulong> { 999 }));

        DiagnosticsReportFormatter.BuildText(guild, result).ShouldContain("Ignored members");
    }

    [Fact]
    public void Channel_findings_name_the_channel_the_roles_and_a_fix()
    {
        var (guild, result) = Run(Guild([Channel(overwrites: [MuteDeny, Role(MemberRole, allow: Speak)])]));

        var text = DiagnosticsReportFormatter.BuildText(guild, result);

        text.ShouldContain("Mutes may not work in these channels");
        text.ShouldContain("#general");
        text.ShouldContain("Member");
        text.ShouldContain("Fix:");
    }

    [Fact]
    public void Embed_summarizes_channel_counts_and_stays_within_the_description_limit()
    {
        var channels = Enumerable.Range(0, 300)
            .Select(i => new ChannelSnapshot((ulong)(1000 + i), $"channel-{i}", ChannelKind.Text, [], true));
        var (guild, result) = Run(Guild(channels));

        var description = DiagnosticsReportFormatter.BuildEmbed(guild, result).Build().Description.ShouldNotBeNull();

        description.ShouldContain("Mutes may not work in **300** channel(s)");
        description.Length.ShouldBeLessThanOrEqualTo(4096);
    }
}
