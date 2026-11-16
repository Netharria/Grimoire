// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.IntegrationTests.Shared.Discord;

/// <summary>
///     KI-54 and DSP-3/4/5 (spec 1.4, section 3.10; docs/upstream/dsharpplus.md). What DSharpPlus does with the next
///     post to a channel after a post to it timed out. Each run starts a fresh bot, so DSharpPlus's rate-limit state
///     starts empty. These are investigations: explicit, so they don't run with the suite. Run them with
///     <c>dotnet test --project Grimoire.IntegrationTests -- --filter-class *DSharpPlusTimeoutInvestigation* --explicit on</c>.
///     The failure message lists every run that went wrong, with its stack trace.
/// </summary>
[Collection("Test collection")]
public sealed class DSharpPlusTimeoutInvestigation(GrimoireCoreFactory database)
{
    private const int Runs = 30;
    private static readonly TimeSpan SecondPostLimit = TimeSpan.FromSeconds(10);

    /// <summary>DSP-3: the timed-out post is the first one DSharpPlus makes on that channel's route.</summary>
    [Fact(Explicit = true)]
    public Task AfterTheRoutesFirstPostTimesOut_TheNextPostStillGoesThrough()
        => this.InvestigateAsync(warmRouteFirst: false);

    /// <summary>KI-54 as found: an earlier post to the channel went through, then one timed out.</summary>
    [Fact(Explicit = true)]
    public Task AfterALaterPostTimesOut_TheNextPostStillGoesThrough()
        => this.InvestigateAsync(warmRouteFirst: true);

    /// <summary>
    ///     DSP-4: a post straight after the bot starts, before DSharpPlus has finished caching the guild. This is where
    ///     KI-54's NullReferenceException came from in the first runs of this investigation, with no timeout at all.
    /// </summary>
    [Fact(Explicit = true)]
    public Task APostRightAfterStartup_GoesThrough()
        => this.InvestigateAsync(_ => this.PostRightAfterStartupAsync());

    private Task InvestigateAsync(bool warmRouteFirst) => this.InvestigateAsync(_ => this.RunOnceAsync(warmRouteFirst));

    private async Task InvestigateAsync(Func<int, Task<(bool Failed, string Summary)>> once)
    {
        var report = new StringBuilder();
        var failures = 0;
        for (var run = 1; run <= Runs; run++)
        {
            var outcome = await once(run);
            if (outcome.Failed)
                failures++;
            report.Append("Run ").Append(run).Append(": ").AppendLine(outcome.Summary);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        failures.ShouldBe(0, report.ToString());
    }

    private async Task<(bool Failed, string Summary)> PostRightAfterStartupAsync()
    {
        await database.ResetDatabase();
        await using var bot = await GrimoireTestBot.StartAsync(database);
        var client = bot.Services.GetRequiredService<DiscordClient>();
        try
        {
            var channel = await client.GetChannelAsync(bot.Guild.LevelLog.Id);
            await channel.SendMessageAsync("straight after startup");
            return (false, "ok");
        }
        catch (Exception exception)
        {
            return (true, $"FAILED: {exception.GetType().Name} (guild cached: "
                          + $"{client.Guilds.ContainsKey(bot.Guild.Id)})\n{exception}");
        }
    }

    /// <summary>
    ///     Whether the channel DSharpPlus handed back, and the one in its guild cache, know their client. DSharpPlus's
    ///     <c>DiscordChannel.Guild</c> dereferences <c>Discord</c> without a check, so a null there throws (DSP-4).
    /// </summary>
    private static string Diagnose(DiscordClient client, DSharpPlus.Entities.DiscordChannel returned, ulong channelId)
    {
        static string ClientOf(object? entity) =>
            entity is null
                ? "not cached"
                : entity.GetType().GetProperty("Discord",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)?.GetValue(entity) is null
                    ? "Discord is NULL"
                    : "Discord set";

        var cached = client.Guilds.Values.SelectMany(guild => guild.Channels.Values)
            .FirstOrDefault(channel => channel.Id == channelId);
        return $"returned channel: {ClientOf(returned)}; cached channel: {ClientOf(cached)}; same object: "
               + $"{ReferenceEquals(returned, cached)}; guild id: {returned.GuildId}";
    }

    private async Task<(bool Failed, string Summary)> RunOnceAsync(bool warmRouteFirst)
    {
        await database.ResetDatabase();
        await using var bot = await GrimoireTestBot.StartAsync(database);
        var path = $"channels/{bot.Guild.LevelLog.Id}/messages";
        var client = bot.Services.GetRequiredService<DiscordClient>();
        // Past the startup race that APostRightAfterStartup_GoesThrough looks at: the guild and the channel are cached.
        (await GrimoireTestBot.EventuallyAsync(
            () => Task.FromResult(client.Guilds.TryGetValue(bot.Guild.Id, out var guild)
                                  && guild.Channels.ContainsKey(bot.Guild.LevelLog.Id)),
            cached => cached)).ShouldBeTrue();
        var channel = await client.GetChannelAsync(bot.Guild.LevelLog.Id);
        int Posts() => bot.Rest.Requests.Count(request => request.Method == HttpMethod.Post && request.Path == path);

        if (warmRouteFirst)
            try
            {
                await channel.SendMessageAsync("warm");
            }
            catch (NullReferenceException exception)
            {
                var atFailure = Diagnose(client, channel, bot.Guild.LevelLog.Id);
                await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                var later = Diagnose(client, channel, bot.Guild.LevelLog.Id);
                string retry;
                try
                {
                    await channel.SendMessageAsync("3 s later");
                    retry = "a post 3 s later went through";
                }
                catch (Exception again)
                {
                    retry = $"a post 3 s later failed too: {again.GetType().Name}";
                }

                var otherChannel = await client.GetChannelAsync(bot.Guild.General.Id);
                return (true, $"FAILED on the warm-up post, with no timeout: {atFailure}. 3 s later: {later}; "
                              + $"{retry}. Another cached channel: {Diagnose(client, otherChannel, bot.Guild.General.Id)}"
                              + $"\n{exception}");
            }

        var before = Posts();
        var timedOut = bot.Rest.TimeOutOnce(HttpMethod.Post, path);
        Exception? first = null;
        try
        {
            await channel.SendMessageAsync("times out");
        }
        catch (Exception exception)
        {
            first = exception;
        }

        await timedOut.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        // DSP-5: more than one POST here means DSharpPlus sent the timed-out message again.
        var timedOutPosts = Posts() - before;

        var second = channel.SendMessageAsync("after the timeout");
        var finished = await Task.WhenAny(second, Task.Delay(SecondPostLimit, TestContext.Current.CancellationToken));
        var firstText = first is null ? "no exception (DSharpPlus retried it?)" : first.GetType().Name;

        if (finished != second)
            return (true, $"STALL: the next post didn't finish within {SecondPostLimit.TotalSeconds} s "
                          + $"(first post: {firstText}, POSTs for it: {timedOutPosts})");
        if (second.Exception?.InnerException is { } failed)
            return (true, $"FAILED: {failed.GetType().Name} (first post: {firstText}, POSTs for it: "
                          + $"{timedOutPosts})\n{failed}");
        return (timedOutPosts > 1,
            $"ok (first post: {firstText}, POSTs for it: {timedOutPosts}{(timedOutPosts > 1 ? ", DUPLICATE" : "")})");
    }
}
