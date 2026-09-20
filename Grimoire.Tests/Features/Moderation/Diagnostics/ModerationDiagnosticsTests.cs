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

public sealed class ModerationDiagnosticsTests
{
    private static DiagnosticsResult Analyze(GuildSnapshot guild, params ulong[] ignored)
        => ModerationDiagnostics.Analyze(guild, new DiagnosticsIgnores(ignored.ToHashSet(), new HashSet<ulong>()));

    private static DiagnosticsResult AnalyzeIgnoringMembers(GuildSnapshot guild, params ulong[] ignoredMembers)
        => ModerationDiagnostics.Analyze(guild, new DiagnosticsIgnores(new HashSet<ulong>(), ignoredMembers.ToHashSet()));

    private static IEnumerable<ChannelFinding> Of(DiagnosticsResult result, ChannelFindingKind kind)
        => result.ChannelFindings.Where(f => f.Kind == kind);

    // ---- mutes ----

    [Fact]
    public void Channel_where_the_mute_role_is_denied_is_fine()
        => Analyze(Guild([Channel(overwrites: MuteDeny)])).ChannelFindings
            .Where(f => f.Kind is ChannelFindingKind.MuteRoleNotDenied or ChannelFindingKind.RoleCanSpeakWhenMuted)
            .ShouldBeEmpty();

    [Fact]
    public void Channel_without_a_mute_role_overwrite_is_reported_once()
        => Of(Analyze(Guild([Channel()])), ChannelFindingKind.MuteRoleNotDenied).ShouldHaveSingleItem();

    [Fact]
    public void Channel_where_everyone_cannot_speak_needs_no_mute_overwrite()
        => Analyze(Guild([Channel()], roles: Roles(DiscordPermissions.None))).ChannelFindings.ShouldBeEmpty();

    [Fact]
    public void Role_with_an_allow_overwrite_is_reported_as_speaking_while_muted()
    {
        var result = Analyze(Guild([Channel(overwrites: [MuteDeny, Role(MemberRole, allow: Speak)])]));

        Of(result, ChannelFindingKind.RoleCanSpeakWhenMuted).ShouldHaveSingleItem()
            .SubjectIds.ShouldBe([MemberRole]);
    }

    [Fact]
    public void Ignored_roles_are_not_reported_and_are_listed()
    {
        var result = Analyze(
            Guild([Channel(overwrites: [MuteDeny, Role(ModeratorRole, allow: Speak)])]),
            ModeratorRole);

        Of(result, ChannelFindingKind.RoleCanSpeakWhenMuted).ShouldBeEmpty();
        result.IgnoredRoles.Select(r => r.Id).ShouldBe([ModeratorRole]);
    }

    [Fact]
    public void Administrator_roles_are_not_reported_and_are_listed_separately()
    {
        var result = Analyze(Guild([Channel(overwrites: [MuteDeny, Role(AdminRole, allow: Speak)])]));

        Of(result, ChannelFindingKind.RoleCanSpeakWhenMuted).ShouldBeEmpty();
        result.AdministratorRoles.Select(r => r.Id).ShouldBe([AdminRole]);
        result.IgnoredRoles.ShouldBeEmpty();
    }

    [Fact]
    public void Ignoring_an_administrator_role_does_not_list_it_twice()
    {
        var result = Analyze(Guild([]), AdminRole);

        result.AdministratorRoles.Count.ShouldBe(1);
        result.IgnoredRoles.ShouldBeEmpty();
    }

    [Fact]
    public void Member_overwrite_that_allows_speaking_is_reported()
    {
        var result = Analyze(Guild([Channel(overwrites: [MuteDeny, Member(999, Speak)])]));

        Of(result, ChannelFindingKind.MemberCanSpeakWhenMuted).ShouldHaveSingleItem().SubjectIds.ShouldBe([999UL]);
    }

    [Fact]
    public void Ignored_members_are_not_reported_and_are_listed()
    {
        var guild = Guild([Channel(overwrites: [MuteDeny, Member(999, Speak), Member(888, Speak)])]);

        var result = AnalyzeIgnoringMembers(guild, 999);

        Of(result, ChannelFindingKind.MemberCanSpeakWhenMuted).ShouldHaveSingleItem().SubjectIds.ShouldBe([888UL]);
        Of(result, ChannelFindingKind.MemberCanSpeakWhenLocked).ShouldHaveSingleItem().SubjectIds.ShouldBe([888UL]);
        result.IgnoredMemberIds.ShouldBe([999UL]);
    }

    [Fact]
    public void Ignoring_every_member_with_an_overwrite_removes_the_member_findings()
        => AnalyzeIgnoringMembers(Guild([Channel(overwrites: [MuteDeny, Member(999, Speak)])]), 999).ChannelFindings
            .ShouldBeEmpty();

    [Fact]
    public void Voice_channels_are_checked_with_voice_permissions()
    {
        var voiceOnlyDeny = Role(MuteRole, deny: VoiceSpeak);

        Analyze(Guild([Channel(ChannelKind.Voice, overwrites: voiceOnlyDeny)])).ChannelFindings.ShouldBeEmpty();
        Of(Analyze(Guild([Channel(ChannelKind.Voice)])), ChannelFindingKind.MuteRoleNotDenied).ShouldHaveSingleItem();
    }

    [Fact]
    public void Without_a_mute_role_no_channel_mute_findings_are_produced()
    {
        var result = Analyze(Guild([Channel()], muteRole: null));

        result.GuildFindings.ShouldContain(f => f.Kind == GuildFindingKind.MuteRoleNotConfigured);
        Of(result, ChannelFindingKind.MuteRoleNotDenied).ShouldBeEmpty();
    }

    // ---- locks ----

    [Fact]
    public void Lock_is_fine_when_only_role_level_permissions_allow_speaking()
        => Analyze(Guild([Channel(overwrites: MuteDeny)])).ChannelFindings
            .Where(f => f.Kind is ChannelFindingKind.RoleCanSpeakWhenLocked or ChannelFindingKind.MemberCanSpeakWhenLocked)
            .ShouldBeEmpty();

    [Fact]
    public void Role_with_an_allow_overwrite_keeps_speaking_in_a_locked_channel()
    {
        var result = Analyze(Guild([Channel(overwrites: [MuteDeny, Role(ModeratorRole, allow: Speak)])]));

        Of(result, ChannelFindingKind.RoleCanSpeakWhenLocked).ShouldHaveSingleItem()
            .SubjectIds.ShouldBe([ModeratorRole]);
    }

    [Fact]
    public void Ignored_roles_may_speak_in_locked_channels()
        => Of(Analyze(Guild([Channel(overwrites: [MuteDeny, Role(ModeratorRole, allow: Speak)])]), ModeratorRole),
            ChannelFindingKind.RoleCanSpeakWhenLocked).ShouldBeEmpty();

    [Fact]
    public void Existing_everyone_allow_is_replaced_by_the_lock()
        => Of(Analyze(Guild([Channel(overwrites: [MuteDeny, Role(TestGuild, allow: Speak)])])),
            ChannelFindingKind.RoleCanSpeakWhenLocked).ShouldBeEmpty();

    [Fact]
    public void Bot_without_manage_channels_in_a_channel_cannot_lock_it()
        => Of(Analyze(Guild([Channel(botCanManage: false, overwrites: MuteDeny)])), ChannelFindingKind.BotCannotLock)
            .ShouldHaveSingleItem();

    [Fact]
    public void Voice_channels_are_not_lock_checked()
        => Analyze(Guild([Channel(ChannelKind.Voice, false, Role(MuteRole, deny: VoiceSpeak))])).ChannelFindings
            .ShouldBeEmpty();

    // ---- guild level ----

    [Fact]
    public void Bot_without_manage_roles_is_reported()
        => Analyze(Guild([], botPermissions: DiscordPermissions.None)).GuildFindings
            .ShouldContain(f => f.Kind == GuildFindingKind.BotMissingManageRoles);

    [Fact]
    public void Mute_role_at_or_above_the_bots_top_role_is_reported()
        => Analyze(Guild([], muteRolePosition: 10, botTop: 10)).GuildFindings
            .ShouldContain(f => f.Kind == GuildFindingKind.MuteRoleAboveBot);

    [Fact]
    public void Configured_mute_role_that_no_longer_exists_is_reported()
        => Analyze(Guild([], muteRole: 555)).GuildFindings
            .ShouldContain(f => f.Kind == GuildFindingKind.MuteRoleNotFound);

    [Fact]
    public void Healthy_guild_has_no_findings()
        => Analyze(Guild([Channel(overwrites: MuteDeny)])).HasFindings.ShouldBeFalse();
}
