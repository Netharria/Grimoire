// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Logging.MessageLogging;

namespace Grimoire.Tests.Features.Logging;

/// <summary>
///     The author fields on a message log embed (backlog A-53). <c>Integration/Logging/PluralKitTests.cs</c> pins a
///     proxied message whose system and member are known; these add the private fallback and the field layout.
/// </summary>
public sealed class MessageLogEmbedExtensionsTests
{
    private static readonly UserId _author = new(111);
    private static readonly UserId _original = new(222);

    private static IReadOnlyList<DiscordEmbedField> Fields(UserId? originalAuthor, PluralKitSystemId? system = null,
        PluralKitMemberId? member = null)
        => new DiscordEmbedBuilder()
            .AddMessageAuthorFields(_author, originalAuthor, system, member)
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
        var fields = Fields(_original, PluralKitSystemId.Create("abcde").ShouldSucceed(),
            PluralKitMemberId.Create("fghij").ShouldSucceed());

        fields.Select(field => (field.Name, field.Value)).ShouldBe([
            ("Original Author", "<@222>"),
            ("System Id", "abcde"),
            ("Member Id", "fghij")
        ]);
        fields.ShouldAllBe(field => field.Inline);
    }

    [Fact]
    public void A_proxied_message_with_no_known_system_or_member_shows_them_as_private()
        => Fields(_original).Select(field => field.Value).ShouldBe(["<@222>", "Private", "Private"]);
}
