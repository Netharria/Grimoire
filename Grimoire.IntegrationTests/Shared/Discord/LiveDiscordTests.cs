// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using DSharpPlus.Entities;
using Grimoire.Functional.Discord;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using Grimoire.IntegrationTests.FakeDiscord;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using static LanguageExt.Prelude;

namespace Grimoire.IntegrationTests.Shared.Discord;

/// <summary>
///     The live Discord edge against the fake REST (spec 1.4, section 8): what each failure becomes, and which calls
///     are retried. The fake answers like Discord, so the exceptions are DSharpPlus's own.
/// </summary>
[Collection("Test collection")]
public sealed class LiveDiscordTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);
    private ChannelId General => new(this.Guild.General.Id);

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task<Fin<A>> Run<A>(Eff<GrimoireRuntime, A> effect)
    {
        using var envIO = EnvIO.New(token: TestContext.Current.CancellationToken);
        return await effect.RunAsync(new GrimoireRuntime(this._bot.Services.GetRequiredService<GrimoireEnv>()),
            envIO);
    }

    private int Requests(HttpMethod method, string path) =>
        this._bot.Rest.Requests.Count(request => request.Method == method && request.Path == path);

    private MessageId SeedMessage() =>
        new(this._bot.Rest.SeedHistory(this.Guild.General, (this.Guild.User1, DateTimeOffset.UtcNow.AddMinutes(-1)))[0]);

    // ── Lookups ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AMember_IsCopiedIntoASnapshot()
    {
        var member = (await this.Run(Discord<GrimoireRuntime>.GetMember(this.GuildId, new UserId(this.Guild.User1.Id))))
            .ShouldSucceed();

        member.Guild.ShouldBe(this.GuildId);
        member.User.Id.ShouldBe(new UserId(this.Guild.User1.Id));
        member.User.Username.Value.ShouldBe("user1");
        member.DisplayName.ShouldBe("user1");
        member.AvatarUrl.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnUnknownMember_IsNotFound()
        => (await this.Run(Discord<GrimoireRuntime>.GetMember(this.GuildId, new UserId(1_100_000_000_000_000_999))))
            .ShouldFail().ShouldBeOfType<NotFound>().Message.ShouldBe("That member no longer exists.");

    [Fact]
    public async Task AGuildTheBotIsntIn_IsNotFound_WithNoRefusal()
        => (await this.Run(Discord<GrimoireRuntime>.GetMember(new GuildId(1_100_000_000_000_009_999),
                new UserId(this.Guild.User1.Id))))
            .ShouldFail().ShouldBeOfType<NotFound>().Message.ShouldBe("The server is no longer available to me.");

    [Fact]
    public async Task AChannel_IsCopiedIntoASnapshot()
    {
        var channel = (await this.Run(Discord<GrimoireRuntime>.GetChannel(this.General))).ShouldSucceed();

        (channel.Id, channel.Guild, channel.Name).ShouldBe((this.General, Some(this.GuildId), "general"));
    }

    [Fact]
    public async Task AMessage_IsCopiedIntoASnapshot()
    {
        var id = this.SeedMessage();

        var message = (await this.Run(Discord<GrimoireRuntime>.GetMessage(this.General, id))).ShouldSucceed();

        (message.Id, message.Channel, message.Author, message.Content)
            .ShouldBe((id, this.General, new UserId(this.Guild.User1.Id), "seeded"));
    }

    [Fact]
    public async Task ALookupThatGetsAServerError_IsRetried_AndSucceeds()
    {
        var id = this.SeedMessage();
        var path = $"channels/{this.Guild.General.Id}/messages/{id.Value}";
        this._bot.Rest.FailOnce(HttpMethod.Get, path,
            FakeDiscordRest.Error(HttpStatusCode.InternalServerError, 0, "Internal Server Error"));

        (await this.Run(Discord<GrimoireRuntime>.GetMessage(this.General, id))).ShouldSucceed();

        this.Requests(HttpMethod.Get, path).ShouldBe(2);
    }

    [Fact]
    public async Task ALookupRefusedForAPermission_IsForbidden_WithTheRefusal()
    {
        var id = this.SeedMessage();
        this._bot.Rest.FailOnce(HttpMethod.Get, $"channels/{this.Guild.General.Id}/messages/{id.Value}",
            FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions"));

        var error = (await this.Run(Discord<GrimoireRuntime>.GetMessage(this.General, id))).ShouldFail()
            .ShouldBeOfType<Forbidden>();

        error.Refusal.ShouldBe(new BotRefusal(this.GuildId, this.General, "Read Message History",
            "look up that message"));
    }

    [Fact]
    public async Task Unauthorized_IsADefect()
    {
        var id = this.SeedMessage();
        this._bot.Rest.FailOnce(HttpMethod.Get, $"channels/{this.Guild.General.Id}/messages/{id.Value}",
            FakeDiscordRest.Error(HttpStatusCode.Unauthorized, 0, "401: Unauthorized"));

        var error = (await this.Run(Discord<GrimoireRuntime>.GetMessage(this.General, id))).ShouldFail();

        error.ShouldNotBeAssignableTo<Expected>();
        error.Message.ShouldContain("401", customMessage: error.ToString());
    }

    // ── Creates are never retried (Q25) ───────────────────────────────────────

    [Fact]
    public async Task ASentMessage_ReturnsItsId()
    {
        var id = (await this.Run(Discord<GrimoireRuntime>.SendMessage(this.General,
            new DiscordMessageBuilder().WithContent("hello")))).ShouldSucceed();

        this._bot.Rest.PostsIn(this.Guild.General).ShouldContain(post => post["id"]!.ToString() == id.Value.ToString());
    }

    [Fact]
    public async Task ASendThatGetsABadGateway_IsUnavailable_AndPostedOnce()
    {
        var path = $"channels/{this.Guild.General.Id}/messages";
        this._bot.Rest.FailOnce(HttpMethod.Post, path,
            FakeDiscordRest.Error(HttpStatusCode.BadGateway, 0, "502: Bad Gateway"));

        (await this.Run(Discord<GrimoireRuntime>.SendMessage(this.General,
            new DiscordMessageBuilder().WithContent("once")))).ShouldFail().ShouldBeOfType<Unavailable>();

        this.Requests(HttpMethod.Post, path).ShouldBe(1);
    }

    [Fact]
    public async Task ASendRefusedForAPermission_NamesTheChannel_AndCarriesTheRefusal()
    {
        this._bot.Rest.FailOnce(HttpMethod.Post, $"channels/{this.Guild.General.Id}/messages",
            FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions"));

        var error = (await this.Run(Discord<GrimoireRuntime>.SendMessage(this.General,
            new DiscordMessageBuilder().WithContent("x")))).ShouldFail().ShouldBeOfType<Forbidden>();

        error.Message.ShouldBe(
            $"I don't have permission to send a message in <#{this.Guild.General.Id}>. I need **Send Messages**.");
        error.Refusal.Case.ShouldBeOfType<BotRefusal>().Channel.ShouldBe(Some(this.General));
    }

    [Fact]
    public async Task ADirectMessageToClosedDms_IsForbidden_WithNoRefusal()
    {
        // The fake's DM channel id is the recipient's id plus 1,000.
        this._bot.Rest.FailOnce(HttpMethod.Post, $"channels/{this.Guild.User1.Id + 1_000}/messages",
            FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user"));

        var error = (await this.Run(Discord<GrimoireRuntime>.SendDirectMessage(new UserId(this.Guild.User1.Id),
            new DiscordMessageBuilder().WithContent("hi")))).ShouldFail().ShouldBeOfType<Forbidden>();

        error.Message.ShouldBe("The member doesn't accept direct messages from me.");
        error.Refusal.IsNone.ShouldBeTrue();
    }

    // ── Deletes are idempotent ────────────────────────────────────────────────

    [Fact]
    public async Task DeletingAMessage_RemovesIt()
    {
        var id = this.SeedMessage();

        (await this.Run(Discord<GrimoireRuntime>.DeleteMessage(this.General, id, "test"))).ShouldSucceed();

        this._bot.Rest.HistoryIds(this.Guild.General).ShouldNotContain(id.Value);
    }

    [Fact]
    public async Task DeletingAMessageThatsAlreadyGone_Succeeds()
        => (await this.Run(Discord<GrimoireRuntime>.DeleteMessage(this.General, new MessageId(1_100_000_000_000_777_777),
            "test"))).ShouldSucceed();
}
