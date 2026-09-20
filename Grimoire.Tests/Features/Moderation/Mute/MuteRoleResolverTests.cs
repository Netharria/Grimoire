// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.Mute;

namespace Grimoire.Tests.Features.Moderation.Mute;

public sealed class MuteRoleResolverTests
{
    private static readonly RoleId _recorded = new(1);
    private static readonly RoleId _configured = new(2);

    private static Func<RoleId, bool> Existing(params RoleId[] roles) => roles.Contains;

    [Fact]
    public void The_role_the_member_was_muted_with_wins_when_it_exists()
        => MuteRoleResolver.Resolve(_recorded, _configured, Existing(_recorded, _configured)).ShouldBe(_recorded);

    [Fact]
    public void The_recorded_role_is_used_even_when_the_server_mute_role_was_changed_or_removed()
    {
        MuteRoleResolver.Resolve(_recorded, _configured, Existing(_recorded)).ShouldBe(_recorded);
        MuteRoleResolver.Resolve(_recorded, null, Existing(_recorded)).ShouldBe(_recorded);
    }

    [Fact]
    public void Falls_back_to_the_current_mute_role_when_the_recorded_role_no_longer_exists()
        => MuteRoleResolver.Resolve(_recorded, _configured, Existing(_configured)).ShouldBe(_configured);

    [Fact]
    public void Falls_back_to_the_current_mute_role_for_mutes_recorded_without_a_role()
        => MuteRoleResolver.Resolve(null, _configured, Existing(_configured)).ShouldBe(_configured);

    [Fact]
    public void Nothing_is_returned_when_the_recorded_role_is_gone_and_no_mute_role_is_set()
        => MuteRoleResolver.Resolve(_recorded, null, Existing()).ShouldBeNull();

    [Fact]
    public void Nothing_is_returned_when_neither_role_exists()
        => MuteRoleResolver.Resolve(_recorded, _configured, Existing()).ShouldBeNull();

    [Fact]
    public void Nothing_is_returned_when_there_is_no_recorded_role_and_no_mute_role_is_set()
        => MuteRoleResolver.Resolve(null, null, Existing(_recorded, _configured)).ShouldBeNull();
}
