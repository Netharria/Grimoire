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

public sealed class EffectivePermissionsTests
{
    private static DiscordPermissions For(ChannelSnapshot channel, params ulong[] held)
        => EffectivePermissions.For(Roles(), TestGuild, channel, held.ToHashSet());

    [Fact]
    public void Everyone_base_permissions_apply_without_overwrites()
        => For(Channel()).HasAllPermissions(Speak).ShouldBeTrue();

    [Fact]
    public void Everyone_overwrite_deny_removes_permissions()
        => For(Channel(overwrites: Role(TestGuild, deny: Speak))).HasAnyPermission(Speak).ShouldBeFalse();

    [Fact]
    public void Role_overwrite_deny_removes_permissions_from_holders_only()
    {
        var channel = Channel(overwrites: Role(MuteRole, deny: Speak));

        For(channel, MuteRole).HasAnyPermission(Speak).ShouldBeFalse();
        For(channel).HasAllPermissions(Speak).ShouldBeTrue();
    }

    [Fact]
    public void A_role_allow_beats_another_roles_deny()
    {
        var channel = Channel(overwrites: [MuteDeny, Role(ModeratorRole, allow: Speak)]);

        For(channel, MuteRole, ModeratorRole).HasAllPermissions(Speak).ShouldBeTrue();
    }

    [Fact]
    public void Administrator_bypasses_overwrites()
    {
        var channel = Channel(overwrites: Role(TestGuild, deny: Speak));

        For(channel, AdminRole).HasAllPermissions(Speak).ShouldBeTrue();
    }

    [Fact]
    public void Overwrites_for_roles_the_member_does_not_hold_are_ignored()
        => For(Channel(overwrites: Role(ModeratorRole, deny: Speak))).HasAllPermissions(Speak).ShouldBeTrue();
}
