// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.Diagnostics;
using Grimoire.Settings.Domain.Documents;

namespace Grimoire.UnitTests.Features.Moderation.Diagnostics;

public sealed class DiagnosticsIgnoresTests
{
    [Fact]
    public void Settings_items_are_split_into_role_and_member_ids()
    {
        DiagnosticsTarget[] items =
        [
            new DiagnosticsTarget.Role(new RoleId(10)),
            new DiagnosticsTarget.Role(new RoleId(11)),
            new DiagnosticsTarget.Member(new UserId(20))
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
