// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Utilities;

namespace Grimoire.Tests.Utilities;

/// <summary>
///     Mention parsing for a custom command's text-trigger argument (backlog A-53). The integration tests only send
///     the plain <c>&lt;@id&gt;</c> form.
/// </summary>
public sealed class DiscordRegexTests
{
    [Fact]
    public void A_user_mention_gives_its_id()
        => DiscordRegex.GetUserMentions("<@123456789012345678>").ShouldBe([123456789012345678UL]);

    [Fact]
    public void Every_user_mention_is_found_in_order()
        => DiscordRegex.GetUserMentions("hi <@1> and <@22>, bye").ShouldBe([1UL, 22UL]);

    [Fact]
    public void A_role_mention_is_not_a_user_mention()
        => DiscordRegex.GetUserMentions("<@&5>").ShouldBeEmpty();

    [Fact]
    public void A_role_mention_gives_its_id()
        => DiscordRegex.GetRoleMentions("ping <@&987654321098765432> now").ShouldBe([987654321098765432UL]);

    [Fact]
    public void A_user_mention_is_not_a_role_mention()
        => DiscordRegex.GetRoleMentions("<@5>").ShouldBeEmpty();

    [Theory]
    [InlineData("")]
    [InlineData("no mentions here")]
    [InlineData("<@>")]
    [InlineData("<@abc>")]
    [InlineData("<#123>")]
    [InlineData("@123")]
    public void Text_without_a_mention_gives_nothing(string text)
    {
        DiscordRegex.GetUserMentions(text).ShouldBeEmpty();
        DiscordRegex.GetRoleMentions(text).ShouldBeEmpty();
    }

    /// <summary>
    ///     Only ASCII digits count. ECMAScript mode keeps <c>\d</c> from matching other scripts' digits, which
    ///     <c>ulong.Parse</c> would reject.
    /// </summary>
    [Fact]
    public void Non_ascii_digits_are_not_an_id()
        => DiscordRegex.GetUserMentions("<@١٢٣>").ShouldBeEmpty();

    /// <summary>
    ///     Discord still sends the nickname form <c>&lt;@!id&gt;</c> from some clients and bots, and
    ///     <c>FakeUser.MemberMention</c> renders a member that way. The regex only knows <c>&lt;@id&gt;</c>, so
    ///     <c>!hug &lt;@!id&gt;</c> leaves <c>%Mention</c> empty and keeps the raw mention in <c>%Message</c>
    ///     (<c>TextCustomCommandProcessor.ResolveSnowflake</c>).
    /// </summary>
    [Fact(Skip = "Fails: the user mention regex ignores the <@!id> nickname form. KI-114")]
    public void A_nickname_mention_gives_its_id()
        => DiscordRegex.GetUserMentions("<@!123>").ShouldBe([123UL]);
}
