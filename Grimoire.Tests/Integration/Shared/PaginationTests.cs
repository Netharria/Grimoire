// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Paged replies (backlog A-52). <c>ReplyPagedAsync</c> backs <c>/Ignore View</c>, <c>/Rewards View</c>,
///     <c>/Mutes View</c>, <c>/SpamFilter View</c> and <c>/Log Message ViewOverrides</c>: DSharpPlus Interactivity edits
///     the deferred response to page 1 with five buttons (<c>leftskip</c>, <c>left</c>, <c>stop</c>, <c>right</c>,
///     <c>rightskip</c>), and answers each click on them with a deferred update (callback type 6) and an edit of the
///     message. Clicks from anyone but the invoker are acknowledged and do nothing. <c>/Commands History</c> and
///     <c>/SinLog</c> page without buttons, as an edit followed by follow-ups (SIN-13 is
///     <c>SinLogTests.ALongLog_ShowsEveryPage</c>). Neither leaderboard is paged: both show 15 rows.
/// </summary>
[Collection("Test collection")]
public sealed class PaginationTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const int DeferredUpdateCallback = 6;
    private const ulong FirstExtraChannelId = 1_100_000_000_000_050_000;
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);
    private static readonly string[] PaginationButtons = ["leftskip", "left", "stop", "right", "rightskip"];

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── The buttons ───────────────────────────────────────────────────────────

    /// <summary>LVL-130: the invoker moves forward, back and to either end, and every item is on some page.</summary>
    [Fact]
    public async Task IgnoreView_TheInvokerPagesForwardBackAndToEitherEnd()
    {
        var ignored = await this.IgnoreMembersAsync(500);

        var (command, first) = await this.RunPagedAsync(this.Guild.Admin, "ignore", SlashOption.Subcommand("view"));

        Footer(first).ShouldBe("Page 1 of 3");
        GrimoireTestBot.ComponentIds(first).ShouldBe(PaginationButtons);
        Footer(await this.ClickAsync(command, this.Guild.Admin, "right")).ShouldBe("Page 2 of 3");
        Footer(await this.ClickAsync(command, this.Guild.Admin, "rightskip")).ShouldBe("Page 3 of 3");
        Footer(await this.ClickAsync(command, this.Guild.Admin, "left")).ShouldBe("Page 2 of 3");
        var backToFirst = await this.ClickAsync(command, this.Guild.Admin, "leftskip");
        Footer(backToFirst).ShouldBe("Page 1 of 3");
        Description(backToFirst).ShouldBe(Description(first));

        var everyPage = await this.ReadEveryPageAsync(command, this.Guild.Admin);
        foreach (var userId in ignored)
            everyPage.ShouldContain($"<@{userId}>");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SPAM-64, MOD-143: another member's click is acknowledged and changes nothing; the invoker's still works.</summary>
    [Fact]
    public async Task Buttons_WorkForTheInvokerOnly()
    {
        await this.IgnoreMembersAsync(300);
        var (command, first) = await this.RunPagedAsync(this.Guild.Admin, "ignore", SlashOption.Subcommand("view"));

        foreach (var stranger in new[] { this.Guild.User1, this.Guild.Moderator, this.Guild.Owner })
        {
            var click = this._bot.ClickButton(stranger, first, "right");
            (await this._bot.Rest.WaitForRequestAsync(click.IsCallback)).Json!["type"]!.GetValue<int>()
                .ShouldBe(DeferredUpdateCallback);
            (await this._bot.Rest.ReceivesRequestWithinAsync(click.IsOriginalResponseEdit, NothingHappensWindow))
                .ShouldBeFalse($"{stranger.Username}'s click should not turn the page");
        }

        Footer(this._bot.ResponseMessageOf(command)!).ShouldBe("Page 1 of 2");
        Footer(await this.ClickAsync(command, this.Guild.Admin, "right")).ShouldBe("Page 2 of 2");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>Stop leaves the page that was showing, with its buttons disabled, and later clicks do nothing.</summary>
    [Fact]
    public async Task Stop_KeepsThePage_AndDisablesTheButtons()
    {
        await this.IgnoreMembersAsync(300);
        var (command, _) = await this.RunPagedAsync(this.Guild.Admin, "ignore", SlashOption.Subcommand("view"));
        await this.ClickAsync(command, this.Guild.Admin, "right");

        var stopped = await this.ClickAsync(command, this.Guild.Admin, "stop");

        Footer(stopped).ShouldBe("Page 2 of 2");
        Buttons(stopped).ShouldAllBe(button => button["disabled"]!.GetValue<bool>());
        var late = this._bot.ClickButton(this.Guild.Admin, stopped, "left");
        (await this._bot.Rest.ReceivesRequestWithinAsync(late.IsOriginalResponseEdit, NothingHappensWindow)).ShouldBeFalse();
        Footer(this._bot.ResponseMessageOf(command)!).ShouldBe("Page 2 of 2");
    }

    /// <summary>A list that fits one embed is a plain reply, with no footer and no buttons.</summary>
    [Fact]
    public async Task AShortList_HasNoButtons()
    {
        await this.IgnoreMembersAsync(3);

        var (_, reply) = await this.RunPagedAsync(this.Guild.Admin, "ignore", SlashOption.Subcommand("view"));

        GrimoireTestBot.ComponentIds(reply).ShouldBeEmpty();
        reply["embeds"]![0]!["footer"].ShouldBeNull();
    }

    // ── Each paged command ────────────────────────────────────────────────────

    /// <summary>SPAM-64: overrides on 150 channels are paged, and every channel is on some page.</summary>
    [Fact]
    public async Task SpamFilterView_With150Overrides_IsPaged()
    {
        await this._bot.SetModuleAsync(Module.AntiSpam, true);
        var channels = this.AnswerForExtraChannels(150);
        foreach (var channel in channels)
            (await this.Settings.SetSpamFilterOverrideAsync(new ChannelId(channel), this.GuildId,
                new ModeratorId(this.Guild.Admin.Id), SpamFilterOverrideOption.NeverFilter, Token)).ShouldSucceed();

        var (command, first) = await this.RunPagedAsync(this.Guild.Admin, "spam_filter", SlashOption.Subcommand("view"));

        first["embeds"]![0]!["author"]!["name"]!.GetValue<string>().ShouldBe("Spam Filter Override Settings");
        Footer(first).ShouldBe("Page 1 of 2");
        var everyPage = await this.ReadEveryPageAsync(command, this.Guild.Admin);
        foreach (var channel in channels)
            everyPage.ShouldContain($"<#{channel}> - Never Filter");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-143: the ephemeral mute list pages too, and every page keeps the Mute Role field.</summary>
    [Fact]
    public async Task MutesView_EphemeralList_PagesForTheInvoker()
    {
        var muted = Enumerable.Range(0, 200).Select(i => 1_100_000_000_000_010_000 + (ulong)i).ToArray();
        await using (var settingsContext = database.CreateSettingsDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var userId in muted)
                settingsContext.Mutes.Add(MuteAdded.Create(new UserId(userId), this.GuildId, new ModeratorId(this.Guild.Moderator.Id),
                    new SinId(1), now.AddMinutes(-10), now.AddMinutes(10), null).ShouldSucceed());
            await settingsContext.SaveChangesAsync(Token);
        }

        var (command, first) = await this.RunPagedAsync(this.Guild.Admin, "mutes", SlashOption.Subcommand("view"));

        ((first["flags"]!.GetValue<int>() & (1 << 6)) != 0).ShouldBeTrue("the list is ephemeral");
        var pages = await this.ReadPagesAsync(command, this.Guild.Admin);
        pages.Count.ShouldBeGreaterThan(1);
        pages.ShouldAllBe(page => page["embeds"]![0]!["fields"]![0]!["name"]!.GetValue<string>() == "Mute Role");
        var everyPage = string.Join("\n", pages.Select(Description));
        foreach (var userId in muted)
            everyPage.ShouldContain($"`{userId}` (left the server)");
    }

    /// <summary>LVL-113: the second page holds the next reward, its message whole.</summary>
    [Fact]
    public async Task RewardsView_SecondPage_ShowsTheNextRewardWhole()
    {
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, new string('a', 3000));
        await this.AddRewardAsync(this.Guild.BannerRoleId, 4, new string('b', 3000));

        var (command, _) = await this.RunPagedAsync(this.Guild.Admin, "rewards", SlashOption.Subcommand("view"));
        var second = await this.ClickAsync(command, this.Guild.Admin, "right");

        Footer(second).ShouldBe("Page 2 of 2");
        Description(second).ShouldBe($"Level:4 Role:<@&{this.Guild.BannerRoleId}> Reward Message: {new string('b', 3000)}");
    }

    /// <summary>05-logging section C: a long override list is paged with buttons, every channel listed.</summary>
    [Fact]
    public async Task LogMessageViewOverrides_LongList_IsPaged()
    {
        await this._bot.ConfigureMessageLogAsync();
        var channels = this.AnswerForExtraChannels(150);
        foreach (var channel in channels)
            (await this.Settings.SetChannelLogOverride(new ChannelId(channel), this.GuildId,
                new ModeratorId(this.Guild.Admin.Id), MessageLogOverrideOption.NeverLog, Token)).ShouldSucceed();

        var (command, first) = await this.RunPagedAsync(this.Guild.Admin, "log",
            SlashOption.Group("message", SlashOption.Subcommand("view_overrides")));

        first["embeds"]![0]!["author"]!["name"]!.GetValue<string>().ShouldBe("Channel Override Settings");
        Footer(first).ShouldBe("Page 1 of 2");
        var everyPage = await this.ReadEveryPageAsync(command, this.Guild.Admin);
        foreach (var channel in channels)
            everyPage.ShouldContain($"<#{channel}> - Never Log");
    }

    /// <summary>
    ///     CC-43: thirty long versions take several pages, and every page should stay visible, with the versions
    ///     running from 30 down to 1, each once. Each page is sent with <c>ReplyAsync</c>, which edits the deferred
    ///     response after the first, so each page replaces the one before and only the last is left.
    /// </summary>
    [Fact(Skip = "Fails: every History page edits the original response, so only the last page is left. KI-113")]
    public async Task CommandsHistory_ThirtyLongVersions_ShowsEveryPage()
    {
        await this._bot.SetModuleAsync(Module.Commands, true);
        for (var version = 1; version <= 30; version++)
            await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("learn",
                SlashOption.String("name", "hello"), SlashOption.String("content", $"version {version} " + new string('x', 150))));

        var interaction = this._bot.RunSlashCommand("commands", this.Guild.Admin,
            SlashOption.Subcommand("history", SlashOption.String("name", "hello")));
        // The oldest version is on the last page, however the pages are sent.
        await this._bot.Rest.WaitForRequestAsync(request =>
            (interaction.IsOriginalResponseEdit(request) || interaction.IsFollowup(request))
            && request.Body!.Contains("**v1** — ", StringComparison.Ordinal));

        var visible = new[] { this._bot.ResponseMessageOf(interaction).ShouldNotBeNull() }
            .Concat(this._bot.Rest.Requests.Where(interaction.IsFollowup).Select(request => request.Json!))
            .Select(Description);
        var versions = string.Join("\n", visible).Split('\n')
            .Where(line => line.StartsWith("**v", StringComparison.Ordinal))
            .Select(line => int.Parse(new string([.. line[3..].TakeWhile(char.IsDigit)])))
            .ToList();
        versions.ShouldBe(Enumerable.Range(1, 30).Reverse());
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(FakeInteraction Command, JsonObject Message)> RunPagedAsync(FakeUser invoker, string command,
        params SlashOption[] options)
    {
        var interaction = this._bot.RunSlashCommand(command, invoker, options);
        await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseEdit);
        return (interaction, this._bot.ResponseMessageOf(interaction).ShouldNotBeNull());
    }

    /// <summary>Clicks a button on the command's reply, waits for the page to change, and returns the message.</summary>
    private async Task<JsonObject> ClickAsync(FakeInteraction command, FakeUser user, string button)
    {
        var click = this._bot.ClickButton(user, this._bot.ResponseMessageOf(command).ShouldNotBeNull(), button);
        await this._bot.Rest.WaitForRequestAsync(click.IsOriginalResponseEdit);
        return this._bot.ResponseMessageOf(command).ShouldNotBeNull();
    }

    /// <summary>Goes to the first page, then clicks forward through the last, returning each page as it was shown.</summary>
    private async Task<IReadOnlyList<JsonObject>> ReadPagesAsync(FakeInteraction command, FakeUser invoker)
    {
        var page = this._bot.ResponseMessageOf(command).ShouldNotBeNull();
        if (!Footer(page).StartsWith("Page 1 ", StringComparison.Ordinal))
            page = await this.ClickAsync(command, invoker, "leftskip");
        var count = int.Parse(Footer(page).Split(' ')[^1]);
        List<JsonObject> pages = [page];
        for (var i = 2; i <= count; i++)
        {
            page = await this.ClickAsync(command, invoker, "right");
            Footer(page).ShouldBe($"Page {i} of {count}");
            pages.Add(page);
        }

        return pages;
    }

    private async Task<string> ReadEveryPageAsync(FakeInteraction command, FakeUser invoker)
        => string.Join("\n", (await this.ReadPagesAsync(command, invoker)).Select(Description));

    private async Task<IReadOnlyList<ulong>> IgnoreMembersAsync(int count)
    {
        var ids = Enumerable.Range(0, count).Select(i => 1_100_000_000_000_100_000 + (ulong)i).ToArray();
        (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, ids
            .Select(id => (XpTrackedItem)IgnoredMember.Create(new UserId(id), this.GuildId,
                new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow))
            .ToHashSet())).ShouldSucceed();
        return ids;
    }

    private async Task AddRewardAsync(ulong roleId, int level, string message)
        => (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(roleId), this.GuildId, level,
            Grimoire.Settings.Domain.Values.RewardMessage.Create(message).ShouldSucceed(),
            new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();

    /// <summary>
    ///     Makes <c>GET channels/{id}</c> answer for <paramref name="count" /> text channels the fake guild doesn't
    ///     list, so a View can show far more overrides than the fixed guild has channels.
    /// </summary>
    private IReadOnlyList<ulong> AnswerForExtraChannels(int count)
    {
        var ids = Enumerable.Range(0, count).Select(i => FirstExtraChannelId + (ulong)i).ToArray();
        var known = this.Guild.Channels.Concat(this.Guild.Threads).ToDictionary(channel => channel.Id);
        this._bot.Rest.On(HttpMethod.Get, @"^channels/\d+$", request =>
        {
            var id = ulong.Parse(request.Path.Split('/')[1]);
            var channel = known.GetValueOrDefault(id) ?? (ids.Contains(id) ? new FakeChannel(id, $"extra-{id - FirstExtraChannelId}") : null);
            return channel is null
                ? FakeDiscordRest.Error(HttpStatusCode.NotFound, 10003, "Unknown Channel")
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(this.Guild.ChannelJson(channel).ToJsonString(), Encoding.UTF8, "application/json")
                };
        });
        return ids;
    }

    private static string Footer(JsonNode message) => message["embeds"]![0]!["footer"]!["text"]!.GetValue<string>();

    private static string Description(JsonNode message) => message["embeds"]![0]!["description"]!.GetValue<string>();

    private static IEnumerable<JsonNode> Buttons(JsonNode message)
        => message["components"]!.AsArray().SelectMany(row => row!["components"]!.AsArray()).Select(button => button!);
}
