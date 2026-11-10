// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Tests.Features.Alerts;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Events;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Urgent alerts and the daily report beyond <see cref="AlertTests" /> (backlog A-49), sections A and B of
///     <c>docs/testing/08-alerts-health.md</c>. Urgent alerts go to <c>#general</c>, the report to <c>#news</c>. The
///     restart cases dispose the bot and start a new one on the same database and the same manual clock. Failed posts
///     are 403s, never timeouts, because of KI-54.
/// </summary>
[Collection("Test collection")]
public sealed class AlertGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);
    private static readonly DateTimeOffset Start = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A channel id that no guild has, standing in for a deleted channel.</summary>
    private const ulong DeletedChannelId = 1_100_000_000_000_000_299;

    private readonly GrimoireCoreFactory _database = database;
    private readonly ManualTimeProvider _clock = new(Start);
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private IAlertSender Alerts => this._bot.Services.GetRequiredService<IAlertSender>();

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.StartAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>Starts the bot, or disposes it and starts a new one on the same database and clock.</summary>
    private async Task StartAsync(ulong? urgentChannel = null, ulong? warningChannel = null)
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureHost: builder =>
        {
            var guild = new FakeGuild();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["channelId"] = (urgentChannel ?? guild.General.Id).ToString(),
                ["warningChannelId"] = (warningChannel ?? guild.Announcements.Id).ToString()
            });
            builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(this._clock));
        });
    }

    // ── A. Urgent alerts ──────────────────────────────────────────────────────

    /// <summary>ALRT-02: a command that fails raises an urgent alert naming the command and the guild.</summary>
    [Fact]
    public async Task ACommandThatFails_RaisesAnUrgentAlert_WithTheCommandAndGuild()
    {
        await this._bot.SetModuleAsync(Grimoire.Settings.Enums.Module.Moderation, true);
        await using var dbContext = this._database.CreateDbContext();
        var sins = dbContext.Model.FindEntityType(typeof(Sin))!;
        var (schema, name) = (sins.GetSchema() ?? "public", sins.GetTableName());
        await dbContext.Database.ExecuteSqlRawAsync($"""ALTER TABLE "{schema}"."{name}" RENAME TO "SinsUnreadable" """,
            TestContext.Current.CancellationToken);
        try
        {
            this._bot.RunSlashCommand("user_info", this.Guild.Admin, SlashOption.User("user", this.Guild.User1));

            var content = (await this.WaitForPostAsync(this.Guild.General, "UnhandledException"))["content"]!
                .GetValue<string>();
            content.ShouldStartWith(
                $"**[URGENT]** UnhandledException `UserInfo` (guild {this.Guild.Id})\nEncountered exception while executing UserInfo [Id `");
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync($"""ALTER TABLE "{schema}"."SinsUnreadable" RENAME TO "{name}" """,
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>ALRT-08: an urgent channel that no longer exists should say so in the log. KI-92.</summary>
    [Fact(Skip = "Fails: AlertService returns quietly when the urgent channel can't be found, so the alert is lost with nothing logged. KI-92")]
    public async Task AMissingUrgentChannel_IsLogged()
    {
        await this.StartAsync(urgentChannel: DeletedChannelId);
        var warningsBefore = this.WarningLogCount();

        this.Alerts.Send(Urgent("disc", "it broke"));

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Get
                                                           && request.Path == $"channels/{DeletedChannelId}");
        (await this.PostedWithinAsync(null, "it broke", NothingHappensWindow)).ShouldBeFalse();
        this.WarningLogCount().ShouldBeGreaterThan(warningsBefore, "the lost alert should be logged");
    }

    /// <summary>ALRT-09: an urgent post Discord refuses is logged as an error, and nothing alerts about the alert.</summary>
    [Fact]
    public async Task AnUrgentPostThatIsRefused_IsLogged_AndNotAlertedAbout()
    {
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.General.Id}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions"));

        this.Alerts.Send(Urgent("disc", "it broke"));

        var logged = await this._bot.Logs.WaitForAsync(logEvent => logEvent.MessageTemplate.Text == "Failed to process alert {AlertType}",
            TimeSpan.FromSeconds(10));
        logged.Exception.ShouldBeOfType<UnauthorizedException>();
        await Task.Delay(NothingHappensWindow, TestContext.Current.CancellationToken);
        this._bot.Rest.Requests.Count(request => request.Method == HttpMethod.Post && request.Path.StartsWith("channels/", StringComparison.Ordinal))
            .ShouldBe(1, "only the refused post, with no alert about it");
    }

    /// <summary>ALRT-09: after a refused post, the next repeat should be posted, not held back by the cooldown. KI-91.</summary>
    [Fact(Skip = "Fails: AlertService takes the cooldown before posting, so a refused post holds back repeats for five minutes. KI-91")]
    public async Task AfterARefusedPost_TheNextRepeatIsPosted()
    {
        this._bot.Rest.FailOnce(HttpMethod.Post, $"channels/{this.Guild.General.Id}/messages",
            FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions"));
        this.Alerts.Send(Urgent("disc", "refused"));
        await this._bot.Logs.WaitForAsync(logEvent => logEvent.MessageTemplate.Text == "Failed to process alert {AlertType}",
            TimeSpan.FromSeconds(10));

        this.Alerts.Send(Urgent("disc", "retried"));

        (await this.PostedWithinAsync(this.Guild.General, "retried", TimeSpan.FromSeconds(5))).ShouldBeTrue();
    }

    /// <summary>ALRT-10: the cooldown is in memory, so after a restart the same problem posts at once with no count.</summary>
    [Fact]
    public async Task AfterARestart_TheCooldownStartsEmpty()
    {
        this.Alerts.Send(Urgent("disc", "before"));
        await this.WaitForPostAsync(this.Guild.General, "before");
        this.Alerts.Send(Urgent("disc", "held back"));
        (await this.PostedWithinAsync(this.Guild.General, "held back", NothingHappensWindow)).ShouldBeFalse();

        await this.StartAsync();
        this.Alerts.Send(Urgent("disc", "after"));

        (await this.WaitForPostAsync(this.Guild.General, "after"))["content"]!.GetValue<string>()
            .ShouldBe($"**[URGENT]** TestAlert `disc` (guild {this.Guild.Id})\nafter");
    }

    /// <summary>ALRT-13: when the queue holds 256 alerts, new ones are dropped; the queued ones still go through.</summary>
    [Fact]
    public async Task AFullQueue_DropsNewAlerts_AndKeepsTheQueuedOnes()
    {
        using var release = await this.FloodTheQueueAsync(266);

        release.Set();
        await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await this._bot.RunBackgroundTickAsync<WarningFlushService>();
            return await this.OccurrencesAsync();
        }, rows => rows.Sum(row => row.Count) >= 256);
        await Task.Delay(NothingHappensWindow, TestContext.Current.CancellationToken);
        await this._bot.RunBackgroundTickAsync<WarningFlushService>();
        (await this.OccurrencesAsync()).ShouldHaveSingleItem().Count.ShouldBe(256, "the ten over capacity were dropped");
    }

    /// <summary>ALRT-13: each dropped alert should log the "Dropped alert" warning. KI-110.</summary>
    [Fact(Skip = "Fails: with BoundedChannelFullMode.DropWrite, TryWrite returns true and drops the alert, so the drop warning is never logged. KI-110")]
    public async Task AFullQueue_LogsEachDroppedAlert()
    {
        using var release = await this.FloodTheQueueAsync(266);

        this._bot.Logs.Events.Count(logEvent => logEvent.MessageTemplate.Text.StartsWith("Dropped alert", StringComparison.Ordinal)
                                                && logEvent.Level == LogEventLevel.Warning)
            .ShouldBe(10);
        release.Set();
    }

    /// <summary>
    ///     Holds the alert queue's single reader on an urgent post that waits for the returned event, then sends that
    ///     many copies of one warning, which pile up in the queue behind it. The post is refused once released.
    /// </summary>
    private async Task<ManualResetEventSlim> FloodTheQueueAsync(int warnings)
    {
        var posting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new ManualResetEventSlim();
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.General.Id}/messages", _ =>
        {
            posting.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(30));
            return FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions");
        });

        this.Alerts.Send(Urgent("disc", "holds the reader"));
        await posting.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        foreach (var _ in Enumerable.Range(0, warnings))
            this.Alerts.Send(Warning("flood"));
        return release;
    }

    // ── B. Warnings and the daily report ──────────────────────────────────────

    /// <summary>ALRT-21, 22: repeats in two guilds add to one row, and the report shows the count and both guilds.</summary>
    [Fact]
    public async Task RepeatsInTwoGuilds_AddToOneRow_AndTheReportShowsBoth()
    {
        var otherGuild = new GuildId(this.Guild.Id + 1_000);
        this.Alerts.Send(Warning("slow"));
        await this.FlushAsync();
        var firstSeen = (await this.OccurrencesAsync()).ShouldHaveSingleItem().LastSeen;

        this._clock.Advance(TimeSpan.FromMinutes(10));
        this.Alerts.Send(Warning("slow"));
        this.Alerts.Send(Warning("slow", otherGuild));
        await this.FlushAsync();

        var row = (await this.OccurrencesAsync()).ShouldHaveSingleItem();
        row.Count.ShouldBe(3);
        row.GuildIds.ShouldBe([new GuildId(this.Guild.Id), otherGuild], ignoreOrder: true);
        row.LastSeen.ShouldBe(firstSeen + TimeSpan.FromMinutes(10));
        row.ReportedAt.ShouldBeNull();

        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();

        var report = (await this.WaitForPostAsync(this.Guild.Announcements))["embeds"]![0]!;
        var description = report["description"]!.GetValue<string>();
        description.ShouldStartWith("**3×** TestWarning (slow)");
        description.ShouldContain("· 2 guild(s)");
        description.ShouldContain("> it was slow");
        report["footer"]!["text"]!.GetValue<string>().ShouldBe("1 warning types, 3 occurrences");
        (await this.OccurrencesAsync()).ShouldHaveSingleItem().ReportedAt.ShouldBe(this._clock.GetUtcNow());
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>ALRT-25: a report with more than 15 types shows 15 and says how many more are in the file.</summary>
    [Fact]
    public async Task ALongReport_SaysHowManyMoreAreInTheFile()
    {
        foreach (var i in Enumerable.Range(0, WarningReportFormatter.MaxEntries + 5))
            this.Alerts.Send(Warning($"kind {i:D2}"));
        await this.FlushAsync();

        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();

        var description = (await this.WaitForPostAsync(this.Guild.Announcements))["embeds"]![0]!["description"]!
            .GetValue<string>();
        description.ShouldEndWith("…and 5 more warning types, in the attached file.");
        System.Text.RegularExpressions.Regex.Count(description, @"\*\*\d+×\*\* TestWarning")
            .ShouldBe(WarningReportFormatter.MaxEntries);
    }

    /// <summary>
    ///     ALRT-26: without a warning channel the day is still marked done. The once-a-day pruning shows it: a row
    ///     past retention survives a second tick the same day and is deleted on the next day's tick.
    /// </summary>
    [Fact]
    public async Task WithoutAWarningChannel_TheDayIsStillMarkedDone()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureHost: builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["channelId"] = new FakeGuild().General.Id.ToString()
            });
            builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(this._clock));
        });
        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();
        await this.SeedOccurrenceAsync("old", lastSeen: Start - TimeSpan.FromDays(31));

        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();
        (await this.OccurrencesAsync()).ShouldHaveSingleItem("the day was already done, so nothing ran");

        this._clock.Advance(TimeSpan.FromDays(1));
        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();
        (await this.OccurrencesAsync()).ShouldBeEmpty();
    }

    /// <summary>ALRT-27: a warning channel that no longer exists posts nothing; the rows stay open and it is retried each tick.</summary>
    [Fact]
    public async Task AMissingWarningChannel_KeepsRowsOpen_AndIsRetried()
    {
        await this.StartAsync(warningChannel: DeletedChannelId);
        this.Alerts.Send(Warning("slow"));
        await this.FlushAsync();

        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();
        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();

        this._bot.Rest.Requests.Count(request => request.Method == HttpMethod.Get && request.Path == $"channels/{DeletedChannelId}")
            .ShouldBe(2);
        (await this.OccurrencesAsync()).ShouldHaveSingleItem().ReportedAt.ShouldBeNull();
        (await this.PostedWithinAsync(null, "Daily warning report", NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>ALRT-27: a missing warning channel should say so in the log. KI-92.</summary>
    [Fact(Skip = "Fails: DailyWarningReportService returns quietly when the warning channel can't be found, every tick, with nothing logged. KI-92")]
    public async Task AMissingWarningChannel_IsLogged()
    {
        await this.StartAsync(warningChannel: DeletedChannelId);
        this.Alerts.Send(Warning("slow"));
        await this.FlushAsync();
        var warningsBefore = this.WarningLogCount();

        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();

        this.WarningLogCount().ShouldBeGreaterThan(warningsBefore, "the missing channel should be logged");
    }

    /// <summary>
    ///     ALRT-28, in part: a warning channel Discord refuses makes each tick fail, and the rows stay open. The urgent
    ///     alert after three failed ticks comes from the real timer loop, which <c>RunBackgroundTickAsync</c> bypasses.
    /// </summary>
    [Fact]
    public async Task ARefusedWarningChannel_FailsEachTick_AndKeepsRowsOpen()
    {
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.Announcements.Id}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions"));
        this.Alerts.Send(Warning("slow"));
        await this.FlushAsync();

        foreach (var _ in Enumerable.Range(0, 3))
        {
            this._clock.Advance(TimeSpan.FromMinutes(1));
            await Should.ThrowAsync<UnauthorizedException>(() => this._bot.RunBackgroundTickAsync<DailyWarningReportService>());
        }

        this._bot.Rest.Requests.Count(request => request.Method == HttpMethod.Post && request.Path == $"channels/{this.Guild.Announcements.Id}/messages")
            .ShouldBe(3);
        (await this.OccurrencesAsync()).ShouldHaveSingleItem().ReportedAt.ShouldBeNull();
    }

    /// <summary>ALRT-29: a flushed warning survives a restart and is in the report.</summary>
    [Fact]
    public async Task AFlushedWarning_SurvivesARestart()
    {
        this.Alerts.Send(Warning("before restart"));
        await this.FlushAsync();

        await this.StartAsync();
        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();

        (await this.WaitForPostAsync(this.Guild.Announcements))["embeds"]![0]!["description"]!.GetValue<string>()
            .ShouldStartWith("**1×** TestWarning (before restart)");
    }

    /// <summary>ALRT-30: a warning not yet flushed when the bot stops should still be in the next report. KI-94.</summary>
    [Fact(Skip = "Fails: nothing flushes the warning buffer on shutdown, so warnings from the last 30 seconds are lost. KI-94")]
    public async Task AnUnflushedWarning_SurvivesACleanRestart()
    {
        this.Alerts.Send(Warning("before restart"));
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        await this.StartAsync();
        await this._bot.RunBackgroundTickAsync<WarningFlushService>();

        (await this.OccurrencesAsync()).ShouldHaveSingleItem().AlertType.ShouldBe("TestWarning (before restart)");
    }

    /// <summary>ALRT-31: after today's report, a restart should not post a second report the same day. KI-95.</summary>
    [Fact(Skip = "Fails: the reported-today date is kept in memory, so a restart posts a second report the same day. KI-95")]
    public async Task AfterTodaysReport_ARestartDoesNotPostASecondOne()
    {
        this.Alerts.Send(Warning("first"));
        await this.FlushAsync();
        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();
        await this.WaitForPostAsync(this.Guild.Announcements, "first");
        this.Alerts.Send(Warning("second"));
        await this.FlushAsync();

        await this.StartAsync();
        this._clock.Advance(TimeSpan.FromMinutes(1));
        await this._bot.RunBackgroundTickAsync<DailyWarningReportService>();

        (await this.PostedWithinAsync(this.Guild.Announcements, "Daily warning report", NothingHappensWindow)).ShouldBeFalse();
        (await this.OccurrencesAsync()).Single(row => row.AlertType == "TestWarning (second)").ReportedAt.ShouldBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Alert Urgent(string discriminator, string message) => new()
    {
        Severity = AlertSeverity.Urgent,
        Type = "TestAlert",
        Discriminator = discriminator,
        Message = message,
        GuildId = new GuildId(new FakeGuild().Id)
    };

    private static Alert Warning(string discriminator, GuildId? guildId = null) => new()
    {
        Severity = AlertSeverity.Warning,
        Type = "TestWarning",
        Discriminator = discriminator,
        Message = "it was slow",
        GuildId = guildId ?? new GuildId(new FakeGuild().Id)
    };

    /// <summary>Warnings and errors that Grimoire itself logged, leaving out DSharpPlus's own request logging.</summary>
    private int WarningLogCount() => this._bot.Logs.Events.Count(logEvent => logEvent.Level >= LogEventLevel.Warning
        && logEvent.Properties.TryGetValue("SourceContext", out var source)
        && source.ToString().StartsWith("\"Grimoire.", StringComparison.Ordinal));

    /// <summary>Lets the alert queue hand the warnings to the buffer, then flushes the buffer to the database.</summary>
    private async Task FlushAsync()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await this._bot.RunBackgroundTickAsync<WarningFlushService>();
    }

    private async Task SeedOccurrenceAsync(string discriminator, DateTimeOffset lastSeen)
    {
        await using var dbContext = this._database.CreateDbContext();
        dbContext.AlertOccurrences.Add(new AlertOccurrence
        {
            AlertKey = $"seeded:{discriminator}",
            AlertType = $"TestWarning ({discriminator})",
            Count = 1,
            FirstSeen = lastSeen,
            LastSeen = lastSeen,
            SampleMessage = "seeded",
            GuildIds = []
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<System.Text.Json.Nodes.JsonNode> WaitForPostAsync(FakeChannel channel, string? containing = null)
        => (await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                               && request.Path == $"channels/{channel.Id}/messages"
                                                               && (containing is null || request.Body?.Contains(containing, StringComparison.Ordinal) == true))).Json!;

    /// <summary>Whether a message containing the text is posted, in the channel or, with none given, in any channel.</summary>
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
