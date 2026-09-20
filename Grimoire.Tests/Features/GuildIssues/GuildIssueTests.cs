// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Tests.Features.Alerts;

namespace Grimoire.Tests.Features.GuildIssues;

public sealed class GuildIssueThrottleTests
{
    private static readonly GuildId _guild = new(1);

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void First_report_of_a_problem_is_shown()
        => new GuildIssueThrottle(this._time).TryAcquire(_guild, "code").ShouldBe(0);

    [Fact]
    public void Repeats_within_a_day_are_suppressed()
    {
        var throttle = new GuildIssueThrottle(this._time);
        throttle.TryAcquire(_guild, "code");
        this._time.Advance(TimeSpan.FromHours(23));

        throttle.TryAcquire(_guild, "code").ShouldBeNull();
    }

    [Fact]
    public void After_a_day_it_is_shown_again_with_the_number_of_suppressed_repeats()
    {
        var throttle = new GuildIssueThrottle(this._time);
        throttle.TryAcquire(_guild, "code");
        throttle.TryAcquire(_guild, "code");
        throttle.TryAcquire(_guild, "code");
        this._time.Advance(TimeSpan.FromDays(1));

        throttle.TryAcquire(_guild, "code").ShouldBe(2);
    }

    [Fact]
    public void Guilds_and_codes_are_throttled_independently()
    {
        var throttle = new GuildIssueThrottle(this._time);
        throttle.TryAcquire(_guild, "code");

        throttle.TryAcquire(new GuildId(2), "code").ShouldBe(0);
        throttle.TryAcquire(_guild, "other").ShouldBe(0);
    }
}

public sealed class GuildIssueCatalogTests
{
    private static readonly GuildId _guild = new(1);

    [Fact]
    public void Missing_permission_names_the_permission_for_known_commands()
        => GuildIssueCatalog.BotMissingPermissions(_guild, "Mute").SuggestedFix.ShouldContain("Manage Roles");

    [Fact]
    public void Missing_permission_falls_back_to_general_advice_for_unknown_commands()
        => GuildIssueCatalog.BotMissingPermissions(_guild, "Something").SuggestedFix
            .ShouldContain("permissions this command needs");

    [Fact]
    public void Missing_permission_codes_are_per_command_so_they_are_throttled_separately()
        => GuildIssueCatalog.BotMissingPermissions(_guild, "Mute").Code
            .ShouldNotBe(GuildIssueCatalog.BotMissingPermissions(_guild, "Kick").Code);

    [Fact]
    public void Every_issue_has_a_summary_and_a_fix()
    {
        GuildIssue[] issues =
        [
            GuildIssueCatalog.BotMissingPermissions(_guild, "Ban"),
            GuildIssueCatalog.RewardRolesNotGrantable(_guild, [new RoleId(5)]),
            GuildIssueCatalog.RewardRolesMissing(_guild, [new RoleId(5)]),
            GuildIssueCatalog.CannotSendInChannel(_guild, new ChannelId(7)),
            GuildIssueCatalog.CannotReadAuditLog(_guild)
        ];

        foreach (var issue in issues)
        {
            issue.Summary.ShouldNotBeNullOrWhiteSpace();
            issue.SuggestedFix.ShouldNotBeNullOrWhiteSpace();
            issue.Code.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
