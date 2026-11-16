// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using Grimoire.Structs;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;

namespace Grimoire.UnitTests.Functional.Discord;

/// <summary>Spec 1.4, section 3.8: one record, timestamped when the event happened (Q26, defect 27).</summary>
public sealed class GuildLogMessageTests
{
    private static readonly GuildId Guild = new(10);
    private static readonly DateTimeOffset At = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = At.AddHours(-1);

    [Fact]
    public void Text_BuildsTheUsualEmbed_AtTheEventsTime()
    {
        var message = GuildLogMessage.Text(Guild, GuildLogType.Moderation, At, "what happened", GrimoireColor.Red,
            "Title", "Footer");

        message.Guild.ShouldBe(Guild);
        message.Type.ShouldBe(GuildLogType.Moderation);
        message.At.ShouldBe(At);
        var embed = message.Message.Embeds.ShouldHaveSingleItem();
        embed.Description.ShouldBe("what happened");
        embed.Author!.Name.ShouldBe("Title");
        embed.Footer!.Text.ShouldBe("Footer");
        embed.Color.ShouldBe(GrimoireColor.Red);
        embed.Timestamp.ShouldBe(At);
    }

    [Fact]
    public void Text_IsPurple_ByDefault()
        => GuildLogMessage.Text(Guild, GuildLogType.Moderation, At, "x").Message.Embeds.ShouldHaveSingleItem()
            .Color.ShouldBe(GrimoireColor.Purple);

    [Fact]
    public void Embed_WithoutATimestamp_GetsTheEventsTime()
        => GuildLogMessage.Embed(Guild, GuildLogType.Moderation, At, new DiscordEmbedBuilder().WithDescription("x"))
            .Message.Embeds.ShouldHaveSingleItem().Timestamp.ShouldBe(At);

    [Fact]
    public void Embed_WithItsOwnTimestamp_KeepsIt()
        => GuildLogMessage.Embed(Guild, GuildLogType.Moderation, At,
                new DiscordEmbedBuilder().WithDescription("x").WithTimestamp(Earlier))
            .Message.Embeds.ShouldHaveSingleItem().Timestamp.ShouldBe(Earlier);

    [Fact]
    public void Of_StampsOnlyTheEmbedsWithoutATimestamp()
    {
        var builder = new DiscordMessageBuilder()
            .AddEmbed(new DiscordEmbedBuilder().WithDescription("own").WithTimestamp(Earlier))
            .AddEmbed(new DiscordEmbedBuilder().WithDescription("none"));

        var message = GuildLogMessage.Of(Guild, GuildLogType.MessageEdited, At, builder);

        message.Message.Embeds.Select(embed => (embed.Description, embed.Timestamp))
            .ShouldBe([("own", Earlier), ("none", At)]);
    }

    [Fact]
    public void TheBridge_StampsAnOldTextMessage_WithTheTimeItWasQueued()
    {
        var old = new GuildLogTextMessage
        {
            GuildId = Guild, GuildLogType = GuildLogType.Leveling, Description = "levelled up", Title = "Level"
        };

        var message = old.ToMessage(At);

        message.At.ShouldBe(At);
        message.Type.ShouldBe(GuildLogType.Leveling);
        var embed = message.Message.Embeds.ShouldHaveSingleItem();
        (embed.Description, embed.Author!.Name, embed.Timestamp).ShouldBe(("levelled up", "Level", (DateTimeOffset?)At));
    }

    [Fact]
    public void TheBridge_KeepsAnOldTextMessagesOwnTimestamp()
        => new GuildLogTextMessage
            {
                GuildId = Guild, GuildLogType = GuildLogType.Leveling, Description = "x", Timestamp = Earlier
            }
            .ToMessage(At).Message.Embeds.ShouldHaveSingleItem().Timestamp.ShouldBe(Earlier);

    [Fact]
    public void TheBridge_StampsAnOldCustomEmbed_ThatHasNoTimestamp()
        => new GuildLogMessageCustomEmbed
            {
                GuildId = Guild, GuildLogType = GuildLogType.Moderation,
                Embed = new DiscordEmbedBuilder().WithDescription("x").Build()
            }
            .ToMessage(At).Message.Embeds.ShouldHaveSingleItem().Timestamp.ShouldBe(At);
}

/// <summary>Spec 1.4, section 3.9: the log gate (Q26b, LOG-2).</summary>
public sealed class GuildLogPostTests
{
    private static readonly GuildId Guild = new(10);
    private static readonly DateTimeOffset At = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly TestEnv _env = new();
    private bool _built;

    private Eff<GrimoireRuntime, GuildLogMessage> Build => Eff<GrimoireRuntime, GuildLogMessage>.Lift(() =>
    {
        this._built = true;
        return GuildLogMessage.Text(Guild, GuildLogType.MessageEdited, At, "edited");
    });

    private Task<Fin<LanguageExt.Unit>> Post() =>
        this._env.Run(GuildLogs<GrimoireRuntime>.Post(Guild, GuildLogType.MessageEdited, this.Build));

    [Fact]
    public async Task WhenTheLogIsOff_TheBuildNeverRuns()
    {
        this._env.GuildLog.Logged = false;

        (await this.Post()).ShouldSucceed();

        this._built.ShouldBeFalse();
        this._env.GuildLog.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenTheLogIsOn_ItIsBuilt_ThenQueued()
    {
        (await this.Post()).ShouldSucceed();

        this._built.ShouldBeTrue();
        this._env.GuildLog.Sent.ShouldHaveSingleItem().Message.Embeds.ShouldHaveSingleItem().Description
            .ShouldBe("edited");
        this._env.Journal.Entries.ShouldBe(["guild log gate MessageEdited", "guild log"]);
    }

    [Fact]
    public async Task AFailingBuild_FailsThePost_WithItsCase()
    {
        var post = GuildLogs<GrimoireRuntime>.Post(Guild, GuildLogType.MessageEdited,
            Eff<GrimoireRuntime, GuildLogMessage>.Fail(FakeDiscord.Outage()));

        (await this._env.Run(post)).ShouldFail().ShouldBeOfType<Unavailable>();

        this._env.GuildLog.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task ABuiltMessage_GoesThroughTheGateToo()
    {
        this._env.GuildLog.Logged = false;

        (await this._env.Run(GuildLogs<GrimoireRuntime>.Post(
            GuildLogMessage.Text(Guild, GuildLogType.Moderation, At, "x")))).ShouldSucceed();

        this._env.GuildLog.Sent.ShouldBeEmpty();
        this._env.Journal.Entries.ShouldBe(["guild log gate Moderation"]);
    }
}
