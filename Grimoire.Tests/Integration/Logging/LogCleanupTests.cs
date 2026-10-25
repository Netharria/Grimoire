// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.LogCleanup;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     <c>CleanupLogsBackgroundTask</c> (backlog A-24). Each tick deletes the bot's log posts older than 30 days from
///     Discord, gives up on a post after three failed tries, deletes saved messages older than 31 days, and keeps the
///     newest three avatar, nickname and username rows per member. Ticks run by hand.
/// </summary>
[Collection("Test collection")]
public sealed class LogCleanupTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Old log posts ─────────────────────────────────────────────────────────

    /// <summary>A log post older than 30 days is deleted from Discord and forgotten. A newer one is left alone.</summary>
    [Fact]
    public async Task OldLogPosts_AreDeletedFromDiscord_NewOnesAreKept()
    {
        var (oldPost, newPost) = this.SeedTwoPosts();
        await this.TrackLogPostsAsync((oldPost, 31), (newPost, 29));

        await this._bot.RunBackgroundTickAsync<CleanupLogsBackgroundTask>();

        this._bot.Rest.HistoryIds(this.Guild.ModLog).ShouldBe([newPost]);
        (await this.TrackedPostsAsync()).Select(post => post.Id).ShouldBe([new MessageId(newPost)]);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>A post that's already gone counts as a failed try, and it's forgotten on the third.</summary>
    [Fact]
    public async Task APostThatCannotBeDeleted_IsRetried_ThenForgottenOnTheThirdTry()
    {
        const ulong missingPost = 1_100_000_000_000_009_001;
        await this.TrackLogPostsAsync((missingPost, 40));

        for (var tick = 1; tick <= 2; tick++)
        {
            await this._bot.RunBackgroundTickAsync<CleanupLogsBackgroundTask>();
            (await this.TrackedPostsAsync()).ShouldHaveSingleItem().TimesTried.ShouldBe(tick);
        }

        await this._bot.RunBackgroundTickAsync<CleanupLogsBackgroundTask>();

        (await this.TrackedPostsAsync()).ShouldBeEmpty();
    }

    /// <summary>A post in a channel that no longer exists is retried the same way.</summary>
    [Fact]
    public async Task APostInADeletedChannel_CountsAsAFailedTry()
    {
        var (oldPost, _) = this.SeedTwoPosts();
        await this.TrackLogPostsAsync((oldPost, 31));
        this._bot.DeleteChannel(this.Guild.ModLog);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);

        await this._bot.RunBackgroundTickAsync<CleanupLogsBackgroundTask>();

        (await this.TrackedPostsAsync()).ShouldHaveSingleItem().TimesTried.ShouldBe(1);
    }

    /// <summary>A Discord error on delete counts as a failed try, and doesn't stop the other posts being deleted.</summary>
    [Fact]
    public async Task ADiscordError_OnOnePost_DoesNotStopTheOthers()
    {
        var (first, second) = this.SeedTwoPosts();
        await this.TrackLogPostsAsync((first, 35), (second, 35));
        this._bot.Rest.On(HttpMethod.Delete, $"channels/{this.Guild.ModLog.Id}/messages/{first}",
            _ => FakeDiscordRest.Error(System.Net.HttpStatusCode.Forbidden, 50013, "Missing Permissions"));

        await this._bot.RunBackgroundTickAsync<CleanupLogsBackgroundTask>();

        this._bot.Rest.HistoryIds(this.Guild.ModLog).ShouldBe([first]);
        (await this.TrackedPostsAsync()).ShouldHaveSingleItem().TimesTried.ShouldBe(1);
    }

    // ── Saved messages and history ────────────────────────────────────────────

    [Fact]
    public async Task SavedMessages_OlderThan31Days_AreDeleted()
    {
        await using (var seed = this._database.CreateDbContext())
        {
            seed.Messages.AddRange(this.Message(1, 32), this.Message(2, 30));
            await seed.SaveChangesAsync(Token);
        }

        await this._bot.RunBackgroundTickAsync<CleanupLogsBackgroundTask>();

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Messages.Where(message => message.GuildId == this.GuildId).Select(message => message.Id).ToListAsync(Token))
            .ShouldBe([new MessageId(2)]);
    }

    /// <summary>Only the newest three avatar, nickname and username rows are kept per member.</summary>
    [Fact]
    public async Task UserHistory_IsTrimmedToTheNewestThree()
    {
        var user = new UserId(this.Guild.User1.Id);
        await using (var seed = this._database.CreateDbContext())
        {
            for (var i = 1; i <= 5; i++)
            {
                var at = DateTimeOffset.UtcNow.AddDays(-10 + i);
                seed.Avatars.Add(new Avatar { UserId = user, GuildId = this.GuildId, FileName = AvatarFileName.Create($"https://cdn/{i}.png").ShouldSucceed(), Timestamp = at });
                seed.NicknameHistory.Add(new NicknameHistory { UserId = user, GuildId = this.GuildId, Nickname = Nickname.Create($"nick{i}").ShouldSucceed(), Timestamp = at });
                seed.UsernameHistory.Add(new UsernameHistory { UserId = user, Username = Username.Create($"name{i}").ShouldSucceed(), Timestamp = at });
            }

            await seed.SaveChangesAsync(Token);
        }

        await this._bot.RunBackgroundTickAsync<CleanupLogsBackgroundTask>();

        // The startup sync's rows are the newest, so two of the five seeded rows survive.
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Avatars.Where(a => a.UserId == user).OrderBy(a => a.Timestamp).Select(a => a.FileName).ToListAsync(Token))
            .Select(name => name.Value).Take(2).ShouldBe(["https://cdn/4.png", "https://cdn/5.png"]);
        (await dbContext.Avatars.Where(a => a.UserId == user).CountAsync(Token)).ShouldBe(3);
        (await dbContext.NicknameHistory.Where(n => n.UserId == user).CountAsync(Token)).ShouldBe(3);
        (await dbContext.UsernameHistory.Where(n => n.UserId == user).OrderBy(n => n.Timestamp).Select(n => n.Username).ToListAsync(Token))
            .Select(name => name.Value).Take(2).ShouldBe(["name4", "name5"]);
        (await dbContext.Avatars.Where(a => a.UserId == new UserId(this.Guild.Moderator.Id)).CountAsync(Token))
            .ShouldBe(1, "other members' history is untouched");
    }

    private (ulong First, ulong Second) SeedTwoPosts()
    {
        var ids = this._bot.Rest.SeedHistory(this.Guild.ModLog,
            (this.Guild.Bot, DateTimeOffset.UtcNow.AddDays(-35)), (this.Guild.Bot, DateTimeOffset.UtcNow.AddDays(-29)));
        return (ids[0], ids[1]);
    }

    private async Task TrackLogPostsAsync(params (ulong Id, int AgeInDays)[] posts)
    {
        await using var seed = this._database.CreateDbContext();
        seed.OldLogMessages.AddRange(posts.Select(post => new OldLogMessage
        {
            Id = new MessageId(post.Id),
            ChannelId = new ChannelId(this.Guild.ModLog.Id),
            GuildId = this.GuildId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-post.AgeInDays)
        }));
        await seed.SaveChangesAsync(Token);
    }

    private async Task<List<OldLogMessage>> TrackedPostsAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.OldLogMessages.AsNoTracking().ToListAsync(Token);
    }

    private Message Message(ulong id, int ageInDays) => new()
    {
        Id = new MessageId(id),
        UserId = new UserId(this.Guild.User1.Id),
        GuildId = this.GuildId,
        ChannelId = new ChannelId(this.Guild.General.Id),
        CreatedTimestamp = DateTimeOffset.UtcNow.AddDays(-ageInDays)
    };
}
