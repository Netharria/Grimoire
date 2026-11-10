// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Tests.Features.CustomCommands;

public sealed class GetCustomCommandTests
{
    // ── TruncateForDiscord ────────────────────────────────────────────────────

    [Fact]
    public void TruncateForDiscord_EmptyString_ReturnsEmpty()
        => GetCustomCommand.TruncateForDiscord("", 2000).ShouldBeEmpty();

    [Fact]
    public void TruncateForDiscord_ShortString_Unchanged()
        => GetCustomCommand.TruncateForDiscord("hello", 2000).ShouldBe("hello");

    [Fact]
    public void TruncateForDiscord_ExactLength_Unchanged()
    {
        var s = new string('a', 2000);
        GetCustomCommand.TruncateForDiscord(s, 2000).ShouldBe(s);
    }

    [Fact]
    public void TruncateForDiscord_OneOverMax_TruncatesWithEllipsis()
    {
        var s = new string('a', 2001);
        var result = GetCustomCommand.TruncateForDiscord(s, 2000);
        result.Length.ShouldBe(2000);
        result.ShouldEndWith("…");
    }

    [Fact]
    public void TruncateForDiscord_SurrogatePairAtCutPoint_RollsBackToAvoidSplit()
    {
        // Place a surrogate-pair emoji (2 UTF-16 code units) so its high surrogate
        // lands exactly at the cut index (maxLength - ellipsis.Length).
        // With maxLength=100, ellipsis="…" (1 unit): content cut is at index 99.
        // 98 'a' chars + 😀 (high at index 98, low at 99) + padding.
        var s = new string('a', 98) + "\U0001F600" + new string('b', 10);
        var result = GetCustomCommand.TruncateForDiscord(s, 100);
        // Should roll back past the orphaned high surrogate
        result.ShouldBe(new string('a', 98) + "…");
    }

    // ── ApplyMention ─────────────────────────────────────────────────────────

    [Fact]
    public void ApplyMention_NoPlaceholder_TextUnchanged()
        => GetCustomCommand.ApplyMention("hello world", null, 1)
            .ShouldBe("hello world");

    [Fact]
    public void ApplyMention_NullSnowflake_PlaceholderReplacedWithEmpty()
        => GetCustomCommand.ApplyMention("ping %Mention please", null, 1)
            .ShouldBe("ping  please");

    [Fact]
    public void ApplyMention_PlaceholderCaseInsensitive_Replaced()
        => GetCustomCommand.ApplyMention("ping %mention please", null, 1)
            .ShouldBe("ping  please");

    // ── ApplyMessage ──────────────────────────────────────────────────────────

    [Fact]
    public void ApplyMessage_NoPlaceholder_TextUnchanged()
        => GetCustomCommand.ApplyMessage("hello world", "ignored")
            .ShouldBe("hello world");

    [Fact]
    public void ApplyMessage_WithPlaceholder_ReplacedWithMessage()
        => GetCustomCommand.ApplyMessage("say: %Message", "world")
            .ShouldBe("say: world");

    [Fact]
    public void ApplyMessage_PlaceholderCaseInsensitive_Replaced()
        => GetCustomCommand.ApplyMessage("say: %message", "world")
            .ShouldBe("say: world");

    [Fact]
    public void ApplyMessage_MessageContainsAtEveryone_LeftAsTyped()
        => GetCustomCommand.ApplyMessage("say: %Message", "@everyone")
            .ShouldBe("say: @everyone");

    [Fact]
    public void ApplyMessage_EmptyMessage_PlaceholderReplaced()
        => GetCustomCommand.ApplyMessage("say: %Message!", "")
            .ShouldBe("say: !");

    // ── IsUserAuthorized ──────────────────────────────────────────────────────

    [Fact]
    public void IsUserAuthorized_NullMember_ReturnsFalse()
    {
        var name = CustomCommandName.Create("cmd").ShouldSucceed();
        var content = CustomCommandContent.Create("hi").ShouldSucceed();
        var cmd = TextCustomCommand.Create(name, new GuildId(1), content, [], [], new ModeratorId(1UL), DateTimeOffset.UtcNow);

        GetCustomCommand.IsUserAuthorized(null, cmd).ShouldBeFalse();
    }

    /// <summary>Every row of the table in <c>spec-custom-command-role-policy.md</c>.</summary>
    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, false, true, false)]
    public void IsUserAuthorized_FollowsTheRolePolicy(bool allowConfigured, bool denyConfigured, bool hasAllowRole,
        bool hasDenyRole, bool expected)
    {
        RoleId allow = new(10UL), deny = new(20UL), unrelated = new(30UL);
        RoleId[] memberRoles = [unrelated, .. hasAllowRole ? [allow] : Array.Empty<RoleId>(),
            .. hasDenyRole ? [deny] : Array.Empty<RoleId>()];

        GetCustomCommand.IsUserAuthorized(memberRoles, allowConfigured ? [allow] : [], denyConfigured ? [deny] : [])
            .ShouldBe(expected);
    }
}
