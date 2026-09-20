// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.Diagnostics;
using Grimoire.Settings.Domain;

namespace Grimoire.Tests.Features.Moderation.Diagnostics;

public sealed class DiagnosticsIgnoresTests
{
    [Fact]
    public void Settings_items_are_split_into_role_and_member_ids()
    {
        var guild = new GuildId(1);
        var mod = new ModeratorId(9);
        DiagnosticsIgnoredItem[] items =
        [
            DiagnosticsIgnoredRole.Create(new RoleId(10), guild, mod, DateTimeOffset.UtcNow).ShouldSucceed(),
            DiagnosticsIgnoredRole.Create(new RoleId(11), guild, mod, DateTimeOffset.UtcNow).ShouldSucceed(),
            DiagnosticsIgnoredMember.Create(new UserId(20), guild, mod, DateTimeOffset.UtcNow).ShouldSucceed()
        ];

        var ignores = DiagnosticsIgnores.From(items);

        ignores.RoleIds.Order().ShouldBe([10UL, 11UL]);
        ignores.MemberIds.ShouldBe([20UL]);
    }

    [Fact]
    public void Empty_ignores_nothing()
    {
        DiagnosticsIgnores.Empty.RoleIds.ShouldBeEmpty();
        DiagnosticsIgnores.Empty.MemberIds.ShouldBeEmpty();
    }
}
