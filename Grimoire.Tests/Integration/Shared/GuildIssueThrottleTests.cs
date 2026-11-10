// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Features.Alerts;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Setup problems beyond <see cref="GatewayAndIssueTests" /> (backlog A-49), section C of
///     <c>docs/testing/08-alerts-health.md</c>: the once-a-day throttle per guild, and what happens when the mod log
///     is unset or gone. Two guilds, with a manual clock.
/// </summary>
[Collection("Test collection")]
public sealed class GuildIssueThrottleTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private FakeGuild SecondGuild => this._bot.SecondGuild!;
    private GuildIssueReporter Reporter => this._bot.Services.GetRequiredService<GuildIssueReporter>();

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database, secondGuild: true, configureHost: builder =>
            builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(this._clock)));
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>ALRT-41: repeats the same day aren't posted again, but each one counts in the warning row.</summary>
    [Fact]
    public async Task RepeatsTheSameDay_AreNotPosted_ButAreCounted()
    {
        await this._bot.ConfigureModerationAsync();
        var issue = GuildIssueCatalog.CannotReadAuditLog(new GuildId(this.Guild.Id));

        await this.Reporter.ReportAsync(issue, cancellationToken: TestContext.Current.CancellationToken);
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.StartsWith(issue.Summary, StringComparison.Ordinal) == true);
        await this.Reporter.ReportAsync(issue, cancellationToken: TestContext.Current.CancellationToken);
        await this.Reporter.ReportAsync(issue, cancellationToken: TestContext.Current.CancellationToken);

        (await this.PostsWithinAsync(this.Guild.ModLog, issue.Summary)).ShouldBe(1);
        (await this.FlushedOccurrenceAsync("GuildIssue (audit-log.forbidden)", 3)).GuildIds
            .ShouldBe([new GuildId(this.Guild.Id)]);
        this._bot.Logs.Events.Count(logEvent => logEvent.MessageTemplate.Text == "Guild {GuildId} has a setup problem: {Code}")
            .ShouldBe(3);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>ALRT-44: the same problem in another guild is posted there; guilds are throttled separately.</summary>
    [Fact]
    public async Task TheSameProblemInAnotherGuild_IsPostedThereToo()
    {
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureModerationAsync(this.SecondGuild);
        var first = GuildIssueCatalog.CannotReadAuditLog(new GuildId(this.Guild.Id));
        var second = GuildIssueCatalog.CannotReadAuditLog(new GuildId(this.SecondGuild.Id));

        await this.Reporter.ReportAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.StartsWith(first.Summary, StringComparison.Ordinal) == true);
        await this.Reporter.ReportAsync(second, cancellationToken: TestContext.Current.CancellationToken);

        var post = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                                      && request.Path == $"channels/{this.SecondGuild.ModLog.Id}/messages");
        post.Json!["embeds"]![0]!["description"]!.GetValue<string>().ShouldStartWith(second.Summary);
        (await this.PostsWithinAsync(this.Guild.ModLog, first.Summary)).ShouldBe(1);
    }

    /// <summary>ALRT-46: with no mod log set, a setup problem posts nothing; it is only logged and counted.</summary>
    [Fact]
    public async Task WithoutAModLog_NothingIsPosted_ButTheProblemIsLoggedAndCounted()
    {
        var issue = GuildIssueCatalog.CannotReadAuditLog(new GuildId(this.Guild.Id));

        await this.Reporter.ReportAsync(issue, cancellationToken: TestContext.Current.CancellationToken);

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path.StartsWith("channels/", StringComparison.Ordinal),
            NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Events.ShouldContain(logEvent => logEvent.MessageTemplate.Text == "Guild {GuildId} has a setup problem: {Code}");
        await this.FlushedOccurrenceAsync("GuildIssue (audit-log.forbidden)", 1);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>ALRT-46, 47: once a mod log is set, the problem should be shown the same day. KI-96.</summary>
    [Fact(Skip = "Fails: GuildIssueReporter takes the daily throttle before queueing the post, so a post that had nowhere to go hides the problem for a day. KI-96")]
    public async Task AProblemWithNowhereToGo_IsShownOnceAModLogIsSet()
    {
        var issue = GuildIssueCatalog.CannotReadAuditLog(new GuildId(this.Guild.Id));
        await this.Reporter.ReportAsync(issue, cancellationToken: TestContext.Current.CancellationToken);
        (await this.PostsWithinAsync(this.Guild.ModLog, issue.Summary)).ShouldBe(0);

        await this._bot.ConfigureModerationAsync();
        await this.Reporter.ReportAsync(issue, cancellationToken: TestContext.Current.CancellationToken);

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                                    && request.Body?.Contains(issue.Summary, StringComparison.Ordinal) == true,
            TimeSpan.FromSeconds(5))).ShouldBeTrue();
    }

    /// <summary>
    ///     ALRT-51: with the mod log set to a channel that was deleted, a mod log entry is lost and the guild isn't told. The missing log
    ///     is reported as <c>log.channel-missing:Moderation</c>, whose own post fails the same way and is then
    ///     throttled, so only the daily warning report shows it.
    /// </summary>
    [Fact]
    public async Task ADeletedModLog_IsOnlyInTheDailyReport()
    {
        const ulong deletedChannel = 1_100_000_000_000_000_298;
        await this._bot.ConfigureModerationAsync();
        (await this._bot.Services.GetRequiredService<SettingsModule>().SetLogChannelSetting(GuildLogType.Moderation,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), new ChannelId(deletedChannel), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();

        await this.Reporter.ReportAsync(GuildIssueCatalog.MuteRoleMissing(new GuildId(this.Guild.Id)), cancellationToken: TestContext.Current.CancellationToken);

        var missing = await this.FlushedOccurrenceAsync("GuildIssue (log.channel-missing:Moderation)", 2);
        missing.SampleMessage.ShouldContain($"<#{deletedChannel}>");
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path.StartsWith("channels/", StringComparison.Ordinal),
            NothingHappensWindow)).ShouldBeFalse();
        (await this.FlushedOccurrenceAsync("GuildIssue (log.channel-missing:Moderation)", 2)).Count.ShouldBe(2,
            "the second report was throttled, so the loop stops there");
    }

    /// <summary>Flushes the warning buffer until the row of the given type has reached the count, and returns it.</summary>
    private async Task<AlertOccurrence> FlushedOccurrenceAsync(string alertType, int count)
        => (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await this._bot.RunBackgroundTickAsync<WarningFlushService>();
            await using var dbContext = database.CreateDbContext();
            return await dbContext.AlertOccurrences.AsNoTracking()
                .Where(row => row.AlertType == alertType)
                .ToListAsync(TestContext.Current.CancellationToken);
        }, rows => rows.Sum(row => row.Count) >= count)).ShouldHaveSingleItem();

    private async Task<int> PostsWithinAsync(FakeChannel channel, string text)
    {
        await Task.Delay(NothingHappensWindow, TestContext.Current.CancellationToken);
        return this._bot.Rest.Requests.Count(request => request.Method == HttpMethod.Post
                                                        && request.Path == $"channels/{channel.Id}/messages"
                                                        && request.Body?.Contains(text, StringComparison.Ordinal) == true);
    }
}
