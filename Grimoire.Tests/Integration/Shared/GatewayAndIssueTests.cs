// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Gateway;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Tests.Features.Alerts;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     <c>GuildIssueReporter</c> and its once-a-day throttle, <c>GatewayWatchdogService</c>, and <c>GatewayEvents</c>
///     through the fake transport (backlog A-32). Urgent alerts go to <c>#general</c>; a manual clock replaces the
///     system one.
/// </summary>
[Collection("Test collection")]
public sealed class GatewayAndIssueTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildIssueReporter Reporter => this._bot.Services.GetRequiredService<GuildIssueReporter>();
    private GatewayHealth Health => this._bot.Services.GetRequiredService<GatewayHealth>();

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureHost: builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["channelId"] = new FakeGuild().General.Id.ToString()
            });
            builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(this._clock));
        });
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Guild issues ──────────────────────────────────────────────────────────

    /// <summary>An issue posts to the mod log once a day; repeats are counted in the next post and every time in the warnings.</summary>
    [Fact]
    public async Task AnIssue_PostsOncePerDay_AndCountsRepeats()
    {
        var issue = GuildIssueCatalog.CannotReadAuditLog(new GuildId(this.Guild.Id));

        await this.Reporter.ReportAsync(issue);
        await this.Reporter.ReportAsync(issue);
        await this.Reporter.ReportAsync(issue);

        var first = await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith(issue.Summary, StringComparison.Ordinal) == true);
        first["author"]!["name"]!.GetValue<string>().ShouldBe("Setup problem");
        first["description"]!.GetValue<string>().ShouldBe($"{issue.Summary}\n**Suggested fix:** {issue.SuggestedFix}");
        (await this.ModLogPostsWithinAsync(issue.Summary, NothingHappensWindow)).ShouldBe(1);

        this._clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));
        await this.Reporter.ReportAsync(issue);

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.EndsWith(
            "\nThis happened 2 more time(s) since it was last shown.", StringComparison.Ordinal) == true);
        await this._bot.RunBackgroundTickAsync<WarningFlushService>();
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.AlertOccurrences.SingleAsync(TestContext.Current.CancellationToken)).Count.ShouldBe(4);
    }

    [Fact]
    public async Task DifferentIssues_AreThrottledSeparately()
    {
        var guildId = new GuildId(this.Guild.Id);

        await this.Reporter.ReportAsync(GuildIssueCatalog.CannotReadAuditLog(guildId));
        await this.Reporter.ReportAsync(GuildIssueCatalog.MuteRoleMissing(guildId));

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith("I cannot read the audit log", StringComparison.Ordinal) == true);
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith("A mute expired", StringComparison.Ordinal) == true);
    }

    // ── Gateway ───────────────────────────────────────────────────────────────

    /// <summary>When Discord asks for a reconnect, the bot resumes its session and keeps handling commands.</summary>
    [Fact]
    public async Task AReconnectRequest_IsResumed_AndCommandsStillWork()
    {
        this._bot.Gateway.RequestReconnect();

        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this._bot.Gateway.SentByBot.Count(sent => sent["op"]?.GetValue<int>() == 6)),
            resumes => resumes > 0)).ShouldBe(1);
        (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).ShouldNotBeNull();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>A resume after known downtime is recorded as a warning.</summary>
    [Fact(Skip = "Fails: DSharpPlus 5.0.0-nightly-02611's resume loop consumes the RESUMED payload without forwarding " +
                 "it, so SessionResumed never fires after a resume. KI-48")]
    public async Task AResumeAfterDowntime_IsRecordedAsAWarning()
    {
        this.Health.MarkDisconnected();
        this._clock.Advance(TimeSpan.FromSeconds(40));

        this._bot.Gateway.RequestReconnect();

        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.Health.DisconnectedFor()), downtime => downtime is null)).ShouldBeNull();
        await this._bot.RunBackgroundTickAsync<WarningFlushService>();
        await using var dbContext = this._database.CreateDbContext();
        var occurrence = await dbContext.AlertOccurrences.SingleAsync(TestContext.Current.CancellationToken);
        occurrence.AlertType.ShouldStartWith("GatewayResumed");
        occurrence.SampleMessage.ShouldBe("Gateway session resumed after 40s.");
    }

    /// <summary>The watchdog posts an urgent alert once the gateway has been down for two minutes, and not before.</summary>
    [Fact]
    public async Task TheWatchdog_AlertsAfterTwoMinutesDown()
    {
        this.Health.MarkDisconnected();
        this._clock.Advance(TimeSpan.FromMinutes(1));
        await this._bot.RunBackgroundTickAsync<GatewayWatchdogService>();
        (await this._bot.Rest.ReceivesRequestWithinAsync(this.IsUrgentPost, NothingHappensWindow)).ShouldBeFalse();

        this._clock.Advance(TimeSpan.FromMinutes(2));
        await this._bot.RunBackgroundTickAsync<GatewayWatchdogService>();

        (await this._bot.Rest.WaitForRequestAsync(this.IsUrgentPost)).Json!["content"]!.GetValue<string>()
            .ShouldBe("**[URGENT]** GatewayDisconnected\nDiscord gateway has been disconnected for 3 minutes.");
    }

    /// <summary>When Discord drops the connection, the bot should know it's down so the watchdog can alert.</summary>
    [Fact(Skip = "Fails: DSharpPlus 5.0.0-nightly-02611 never dispatches SocketClosed or Zombied, so GatewayEvents " +
                 "never marks the gateway down and the watchdog can't alert. KI-48")]
    public async Task ADroppedConnection_IsNoticed()
    {
        this._bot.Gateway.HoldReconnects = true;

        this._bot.Gateway.CloseFromServer(4000);

        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.Health.DisconnectedFor()), downtime => downtime is not null))
            .ShouldNotBeNull();
    }

    private bool IsUrgentPost(RecordedRequest request)
        => request.Method == HttpMethod.Post && request.Path == $"channels/{this.Guild.General.Id}/messages"
                                             && request.Body?.Contains("[URGENT]", StringComparison.Ordinal) == true;

    private async Task<int> ModLogPostsWithinAsync(string text, TimeSpan window)
    {
        await Task.Delay(window, TestContext.Current.CancellationToken);
        return this._bot.Rest.Requests.Count(request => this._bot.IsModLogPost(request)
                                                        && request.Body?.Contains(text, StringComparison.Ordinal) == true);
    }
}
