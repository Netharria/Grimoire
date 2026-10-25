// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Tests.Features.Alerts;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     The alert pipeline end to end (backlog A-31). Urgent alerts post at once to <c>channelId</c>, here
///     <c>#general</c>, with a five-minute cooldown per problem. Warnings are buffered, flushed to the database by
///     <c>WarningFlushService</c>, and posted once a day by <c>DailyWarningReportService</c> to
///     <c>warningChannelId</c>, here <c>#news</c>. A manual clock replaces the system one; the timed services run by
///     hand.
/// </summary>
[Collection("Test collection")]
public sealed class AlertTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);
    private static readonly DateTimeOffset Start = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);

    private readonly GrimoireCoreFactory _database = database;
    private readonly ManualTimeProvider _clock = new(Start);
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private IAlertSender Alerts => this._bot.Services.GetRequiredService<IAlertSender>();

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.StartAsync(urgentChannel: true, warningChannel: true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task StartAsync(bool urgentChannel, bool warningChannel, string? reportTime = null)
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureHost: builder =>
        {
            var guild = new FakeGuild();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["channelId"] = urgentChannel ? guild.General.Id.ToString() : null,
                ["warningChannelId"] = warningChannel ? guild.Announcements.Id.ToString() : null,
                ["warningReportTimeUtc"] = reportTime
            });
            builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(this._clock));
        });
    }

    // ── Urgent ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnUrgentAlert_IsPostedAtOnce()
    {
        this.Alerts.Send(Urgent("disc", "it broke"));

        (await this.WaitForPostAsync(this.Guild.General))["content"]!.GetValue<string>()
            .ShouldBe($"**[URGENT]** TestAlert `disc` (guild {this.Guild.Id})\nit broke");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>The same problem posts once per cooldown, and the next post says how many repeats it held back.</summary>
    [Fact]
    public async Task RepeatsOfTheSameProblem_AreHeldBack_ThenCounted()
    {
        this.Alerts.Send(Urgent("disc", "first"));
        await this.WaitForPostAsync(this.Guild.General);

        this.Alerts.Send(Urgent("disc", "second"));
        this.Alerts.Send(Urgent("disc", "third"));
        (await this.PostedWithinAsync(this.Guild.General, "second", NothingHappensWindow)).ShouldBeFalse();

        this._clock.Advance(TimeSpan.FromMinutes(6));
        this.Alerts.Send(Urgent("disc", "fourth"));

        (await this.WaitForPostAsync(this.Guild.General, "fourth"))["content"]!.GetValue<string>()
            .ShouldStartWith($"**[URGENT]** TestAlert `disc` (guild {this.Guild.Id}) — repeated 2 more times since the last alert");
    }

    [Fact]
    public async Task DifferentProblems_AreNotHeldBackByEachOther()
    {
        this.Alerts.Send(Urgent("one", "first"));
        await this.WaitForPostAsync(this.Guild.General, "first");

        this.Alerts.Send(Urgent("two", "second"));

        await this.WaitForPostAsync(this.Guild.General, "second");
    }

    [Fact]
    public async Task WithoutAnUrgentChannel_NothingIsPosted()
    {
        await this.StartAsync(urgentChannel: false, warningChannel: true);

        this.Alerts.Send(Urgent("disc", "it broke"));

        (await this.PostedWithinAsync(null, "it broke", NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    // ── Warnings and the daily report ─────────────────────────────────────────

    /// <summary>Warnings aren't posted. They're flushed to the database, and the daily report posts them once.</summary>
    [Fact]
    public async Task Warnings_AreFlushed_ThenReportedOnceADay()
    {
        this.Alerts.Send(Warning("slow"));
        this.Alerts.Send(Warning("slow"));
        (await this.PostedWithinAsync(null, "slow", NothingHappensWindow)).ShouldBeFalse();

        await this._bot.RunBackgroundTickAsync<WarningFlushService>();
        var occurrence = (await this.OccurrencesAsync()).ShouldHaveSingleItem();
        (occurrence.AlertType, occurrence.Count, occurrence.ReportedAt).ShouldBe(("TestWarning (slow)", 2, null));

        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();

        var report = (await this.WaitForPostAsync(this.Guild.Announcements))["embeds"]![0]!;
        report["title"]!.GetValue<string>().ShouldBe("Daily warning report");
        report["description"]!.GetValue<string>().ShouldStartWith("**2×** TestWarning (slow)");
        report["footer"]!["text"]!.GetValue<string>().ShouldBe("1 warning types, 2 occurrences");
        (await this.OccurrencesAsync()).ShouldHaveSingleItem().ReportedAt.ShouldBe(Start);

        this.Alerts.Send(Warning("slow"));
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await this._bot.RunBackgroundTickAsync<WarningFlushService>();
        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();
        this._bot.Rest.Requests.Count(request => request.Path == $"channels/{this.Guild.Announcements.Id}/messages")
            .ShouldBe(1, "the report is posted once a day");
    }

    [Fact]
    public async Task TheReport_WaitsForItsTimeOfDay()
    {
        await this.StartAsync(urgentChannel: true, warningChannel: true, reportTime: "11:00");
        this.Alerts.Send(Warning("slow"));
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await this._bot.RunBackgroundTickAsync<WarningFlushService>();

        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();
        (await this.PostedWithinAsync(this.Guild.Announcements, "", NothingHappensWindow)).ShouldBeFalse();

        this._clock.Advance(TimeSpan.FromHours(1));
        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();
        await this.WaitForPostAsync(this.Guild.Announcements);
    }

    [Fact]
    public async Task WithoutAWarningChannel_NoReportIsPosted_AndWarningsStayOpen()
    {
        await this.StartAsync(urgentChannel: true, warningChannel: false);
        this.Alerts.Send(Warning("slow"));
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await this._bot.RunBackgroundTickAsync<WarningFlushService>();

        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();

        (await this.PostedWithinAsync(null, "", NothingHappensWindow)).ShouldBeFalse();
        (await this.OccurrencesAsync()).ShouldHaveSingleItem().ReportedAt.ShouldBeNull();
    }

    private static Alert Urgent(string discriminator, string message) => new()
    {
        Severity = AlertSeverity.Urgent,
        Type = "TestAlert",
        Discriminator = discriminator,
        Message = message,
        GuildId = new GuildId(new FakeGuild().Id)
    };

    private static Alert Warning(string discriminator) => new()
    {
        Severity = AlertSeverity.Warning,
        Type = "TestWarning",
        Discriminator = discriminator,
        Message = "it was slow"
    };

    private async Task<System.Text.Json.Nodes.JsonNode> WaitForPostAsync(FakeChannel channel, string? containing = null)
        => (await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                               && request.Path == $"channels/{channel.Id}/messages"
                                                               && (containing is null || request.Body?.Contains(containing, StringComparison.Ordinal) == true))).Json!;

    /// <summary>Whether a message containing the text is posted, in the channel or, with none given, anywhere but the mod log.</summary>
    private Task<bool> PostedWithinAsync(FakeChannel? channel, string containing, TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                               && (channel is null
                                                                   ? request.Path.StartsWith("channels/", StringComparison.Ordinal)
                                                                   : request.Path == $"channels/{channel.Id}/messages")
                                                               && request.Body?.Contains(containing, StringComparison.Ordinal) == true, window);

    private async Task<List<AlertOccurrence>> OccurrencesAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.AlertOccurrences.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }
}
