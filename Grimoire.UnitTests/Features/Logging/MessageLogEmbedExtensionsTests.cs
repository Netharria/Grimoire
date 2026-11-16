// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Logging.MessageLogging;

namespace Grimoire.UnitTests.Features.Logging;

/// <summary>
///     The author fields on a message log embed (backlog A-53). <c>Integration/Logging/PluralKitTests.cs</c> pins a
///     proxied message end to end; these pin the field layout. A proxy always has its system and member (LOG-3, Q37).
/// </summary>
public sealed class MessageLogEmbedExtensionsTests
{
    private static readonly UserId _author = new(111);
    private static readonly UserId _original = new(222);

    private static IReadOnlyList<DiscordEmbedField> Fields(ProxyAuthor? proxy)
        => new DiscordEmbedBuilder()
            .AddMessageAuthorFields(_author, proxy)
            .Fields;

    [Fact]
    public void A_plain_message_names_its_author_only()
    {
        var field = Fields(null).ShouldHaveSingleItem();

        field.Name.ShouldBe("Author");
        field.Value.ShouldBe("<@111>");
        field.Inline.ShouldBeTrue();
    }

    [Fact]
    public void A_proxied_message_names_the_account_behind_it_with_its_system_and_member()
    {
        var fields = Fields(new ProxyAuthor(_original, PluralKitSystemId.Create("abcde").ShouldSucceed(),
            PluralKitMemberId.Create("fghij").ShouldSucceed()));

        fields.Select(field => (field.Name, field.Value)).ShouldBe([
            ("Original Author", "<@222>"),
            ("System Id", "abcde"),
            ("Member Id", "fghij")
        ]);
        fields.ShouldAllBe(field => field.Inline);
    }
}
