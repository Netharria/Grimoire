// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Functional.Discord;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;

namespace Grimoire.UnitTests.Functional.Discord;

/// <summary>Spec 1.4, section 3.6: what Discord&lt;RT&gt; retries, against the fake.</summary>
public sealed class DiscordEffectTests
{
    private static readonly GuildId Guild = new(10);
    private static readonly ChannelId Channel = new(20);
    private static readonly UserId User = new(30);

    private readonly TestEnv _env = new();

    private static MemberSnapshot Member { get; } = new(Guild,
        new UserSnapshot(User, Username.Create("someone"), None, "https://cdn.test/a.png", false),
        None, "https://cdn.test/a.png", [], 0, DiscordPermissions.None, DateTimeOffset.UnixEpoch, None);

    private int Calls(string operation) =>
        this._env.Journal.Entries.Count(entry => entry == $"discord {operation}");

    [Fact]
    public async Task ALookup_ThatFailsTwiceWithAnOutage_IsRetried_AndSucceeds()
    {
        this._env.Discord.Has(Member).Fails("Discord.GetMember", FakeDiscord.Outage(), times: 2);

        (await this._env.Run(Discord<GrimoireRuntime>.GetMember(Guild, User))).ShouldSucceed().ShouldBe(Member);

        this.Calls("Discord.GetMember").ShouldBe(3);
    }

    [Fact]
    public async Task ALookup_ThatKeepsFailing_IsUnavailable_AfterThreeRetries()
    {
        this._env.Discord.Has(Member).Fails("Discord.GetMember", FakeDiscord.Outage());

        (await this._env.Run(Discord<GrimoireRuntime>.GetMember(Guild, User))).ShouldFail()
            .ShouldBeOfType<Unavailable>();

        this.Calls("Discord.GetMember").ShouldBe(4);
    }

    [Fact]
    public async Task AMissingMember_IsNotFound_AndNotRetried()
    {
        (await this._env.Run(Discord<GrimoireRuntime>.GetMember(Guild, User))).ShouldFail()
            .ShouldBeOfType<NotFound>().Message.ShouldBe("That member no longer exists.");

        this.Calls("Discord.GetMember").ShouldBe(1);
    }

    [Fact]
    public async Task ARefusal_IsNotRetried()
    {
        this._env.Discord.Has(Member).Fails("Discord.GetMember", FakeDiscord.MissingPermission(Guild, "View Channel"));

        (await this._env.Run(Discord<GrimoireRuntime>.GetMember(Guild, User))).ShouldFail()
            .ShouldBeOfType<Forbidden>();

        this.Calls("Discord.GetMember").ShouldBe(1);
    }

    [Fact]
    public async Task AnOutageThatIsntTransient_IsNotRetried()
    {
        this._env.Discord.Has(Member).Fails("Discord.GetMember", new Unavailable("rate limited"));

        await this._env.Run(Discord<GrimoireRuntime>.GetMember(Guild, User));

        this.Calls("Discord.GetMember").ShouldBe(1);
    }

    [Fact]
    public async Task SendingAMessage_IsNeverRetried()
    {
        this._env.Discord.Fails("Discord.SendMessage", FakeDiscord.Outage(), times: 1);

        (await this._env.Run(Discord<GrimoireRuntime>.SendMessage(Channel, new DiscordMessageBuilder())))
            .ShouldFail().ShouldBeOfType<Unavailable>();

        this.Calls("Discord.SendMessage").ShouldBe(1);
        this._env.Discord.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendingADirectMessage_IsNeverRetried()
    {
        this._env.Discord.Fails("Discord.SendDirectMessage", FakeDiscord.Outage(), times: 1);

        await this._env.Run(Discord<GrimoireRuntime>.SendDirectMessage(User, new DiscordMessageBuilder()));

        this.Calls("Discord.SendDirectMessage").ShouldBe(1);
    }

    [Fact]
    public async Task DeletingAMessage_IsRetried()
    {
        this._env.Discord.Fails("Discord.DeleteMessage", FakeDiscord.Outage(), times: 1);

        (await this._env.Run(Discord<GrimoireRuntime>.DeleteMessage(Channel, new MessageId(40), "spam")))
            .ShouldSucceed();

        this.Calls("Discord.DeleteMessage").ShouldBe(2);
        this._env.Discord.Deleted.ShouldBe([(Channel, new MessageId(40))]);
    }

    [Fact]
    public async Task ASentMessage_IsKept_WithItsChannel()
    {
        var message = new DiscordMessageBuilder().WithContent("hello");

        (await this._env.Run(Discord<GrimoireRuntime>.SendMessage(Channel, message))).ShouldSucceed();

        this._env.Discord.Sent.ShouldBe([(Channel, message)]);
    }
}

public sealed class SnapshotTests
{
    private static UserSnapshot User(Option<string> globalName) =>
        new(new UserId(1), Username.Create("user_name"), globalName, "https://cdn.test/user.png", false);

    private static MemberSnapshot Member(Option<string> nickname, Option<string> globalName) =>
        new(new GuildId(1), User(globalName), nickname, "https://cdn.test/guild.png", [], 0,
            DiscordPermissions.None, DateTimeOffset.UnixEpoch, None);

    [Fact]
    public void TheDisplayName_IsTheNickname_First()
        => Member(Some("Nick"), Some("Global")).DisplayName.ShouldBe("Nick");

    [Fact]
    public void TheDisplayName_FallsBackToTheGlobalName()
        => Member(None, Some("Global")).DisplayName.ShouldBe("Global");

    [Fact]
    public void TheDisplayName_FallsBackToTheUsername()
        => Member(None, None).DisplayName.ShouldBe("user_name");

    [Fact]
    public void AnEmptyNickname_CountsAsNone()
        => Member(Some(""), None).DisplayName.ShouldBe("user_name");
}
