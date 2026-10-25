// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.Lock;
using Grimoire.Features.Shared.SharedDtos;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     Lock expiry (<c>LockBackgroundTasks</c>), cases MOD-178 to MOD-189 in
///     <c>docs/testing/02-moderation-actions.md</c>. Tests run one expiry tick on demand. Expired locks are made with
///     a zero-length <c>/Lock</c> or seeded straight into the settings database, which also stands in for MOD-187 (the
///     bot starting after a lock's end). MOD-185 (a retry after 30 seconds of backoff) stays manual.
/// </summary>
[Collection("Test collection")]
public sealed class LockExpiryTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const ulong SendMessages = 1UL << 11;
    private const ulong TextLock = SendMessages | (1UL << 38) | (1UL << 12) | (1UL << 6);
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>MOD-178 and MOD-189: an expired lock is reverted, announced in the channel and mod log, and closed.</summary>
    [Fact]
    public async Task ExpiredChannelLock_IsReverted_Announced_AndClosed()
    {
        await this.LockForZeroMinutesAsync(this.Guild.General);
        this.Guild.OverwriteOf(this.Guild.General.Id, this.Guild.Id).ShouldBe((0UL, TextLock));

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        this.Guild.OverwriteOf(this.Guild.General.Id, this.Guild.Id).ShouldBe((0UL, 0UL));
        var expired = $"Lock on {this.Guild.General.Mention} has expired.";
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>() == expired);
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                            && request.Path == $"channels/{this.Guild.General.Id}/messages"
                                                            && request.Json?["embeds"]?[0]?["description"]?.GetValue<string>() == expired);
        var unlock = await this.SingleChannelUnlockAsync();
        unlock.ModeratorId.ShouldBe(new ModeratorId(this.Guild.Bot.Id));
        unlock.Reason!.Value.Value.ShouldBe("Channel lock expired.");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-179: an expired thread lock is lifted and announced.</summary>
    [Fact]
    public async Task ExpiredThreadLock_IsLifted_AndAnnounced()
    {
        var thread = this.Guild.PublicThread;
        await this.SeedExpiredThreadLockAsync(thread);

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        (await this.Settings.IsThreadLocked(new ChannelId(thread.Id), this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeFalse();
        var expired = $"Lock on {thread.Mention} has expired.";
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>() == expired);
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                            && request.Path == $"channels/{thread.Id}/messages");
        (await this.SingleThreadUnlockAsync()).Reason!.Value.Value.ShouldBe("Thread lock expired.");
    }

    /// <summary>MOD-180: a lock already unlocked by hand is left alone at its end time.</summary>
    [Fact]
    public async Task UnlockedBeforeExpiry_NothingMoreHappens()
    {
        await this.LockForZeroMinutesAsync(this.Guild.General);
        await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "unlock", this.Guild.Moderator);
        await this._bot.WaitForModLogEmbedAsync(embed =>
            embed["description"]?.GetValue<string>()?.Contains("unlocked", StringComparison.Ordinal) == true);

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        (await this._bot.Rest.ReceivesRequestWithinAsync(request =>
                request.Json?["embeds"]?[0]?["description"]?.ToString().EndsWith("has expired.", StringComparison.Ordinal) == true,
            NothingHappensWindow)).ShouldBeFalse();
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.ChannelLocks.OfType<ChannelUnlocked>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    /// <summary>MOD-181: when a newer lock ends later, the channel stays locked past the first end time.</summary>
    [Fact]
    public async Task LockedAgainForLonger_StaysLocked()
    {
        await this.LockForZeroMinutesAsync(this.Guild.General);
        await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "lock", this.Guild.Moderator,
            SlashOption.Enum("duration_type", DurationType.Hours), SlashOption.Integer("duration_amount", 1));

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        this.Guild.OverwriteOf(this.Guild.General.Id, this.Guild.Id).ShouldBe((0UL, TextLock));
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.ChannelLocks.OfType<ChannelUnlocked>().AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>MOD-182 and MOD-183: a deleted channel or thread is closed with no messages.</summary>
    [Theory]
    [InlineData("channel")]
    [InlineData("thread")]
    public async Task ExpiredLockOnADeletedChannel_IsClosedQuietly(string kind)
    {
        if (kind == "channel")
        {
            await this.SeedExpiredChannelLockAsync(this.Guild.Forum);
            this._bot.DeleteChannel(this.Guild.Forum);
        }
        else
        {
            await this.SeedExpiredThreadLockAsync(this.Guild.PublicThread);
            this._bot.DeleteChannel(this.Guild.PublicThread);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        if (kind == "channel")
            (await this.SingleChannelUnlockAsync()).Reason!.Value.Value.ShouldBe("Channel no longer exists.");
        else
            (await this.SingleThreadUnlockAsync()).Reason!.Value.Value.ShouldBe("Thread no longer exists.");
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>MOD-184: without Manage Roles the revert is refused, an issue is raised, and the lock stays.</summary>
    [Fact]
    public async Task ExpiredLock_ThatCannotBeReverted_ReportsAnIssue_AndStaysLocked()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild =>
        {
            guild.BotPermissions = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission
                                   | FakeGuild.EmbedLinksPermission | FakeGuild.ManageChannelsPermission;
            guild.SetOverwrite(guild.General.Id, guild.Id, 0, 0, TextLock);
        });
        await this._bot.ConfigureModerationAsync();
        await this.SeedExpiredChannelLockAsync(this.Guild.General, setOverwrite: false);

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        await this._bot.WaitForModLogEmbedAsync(embed =>
            embed["description"]?.GetValue<string>()?.Contains(this.Guild.General.Mention, StringComparison.Ordinal) == true);
        this.Guild.OverwriteOf(this.Guild.General.Id, this.Guild.Id).ShouldBe((0UL, TextLock));
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.ChannelLocks.OfType<ChannelUnlocked>().AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>MOD-186: with the @everyone overwrite deleted by hand, expiry puts back the recorded state.</summary>
    [Fact]
    public async Task ExpiredLock_WhoseOverwriteWasDeleted_RestoresTheRecordedState()
    {
        await this.SeedExpiredChannelLockAsync(this.Guild.General, previouslyAllowed: SendMessages, setOverwrite: false);

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        this.Guild.OverwriteOf(this.Guild.General.Id, this.Guild.Id).ShouldBe((SendMessages, 0UL));
        (await this.SingleChannelUnlockAsync()).ShouldNotBeNull();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-188: a lock in a guild the bot isn't in is kept, and Discord isn't asked about it.</summary>
    [Fact]
    public async Task ExpiredLock_InAGuildTheBotLeft_IsKept()
    {
        await this.SeedExpiredChannelLockAsync(this.Guild.General, guildId: new GuildId(1_100_000_000_000_000_999), setOverwrite: false);
        var requestsBefore = this._bot.Rest.Requests.Count;

        await this._bot.RunBackgroundTickAsync<LockBackgroundTasks>();

        this._bot.Rest.Requests.Count.ShouldBe(requestsBefore);
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.ChannelLocks.OfType<ChannelUnlocked>().AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    private Task<FakeReply> LockForZeroMinutesAsync(FakeChannel channel)
        => this._bot.RunSlashCommandAndWaitAsync(channel, "lock", this.Guild.Moderator,
            SlashOption.Enum("duration_type", DurationType.Minutes), SlashOption.Integer("duration_amount", 0));

    /// <summary>
    ///     Writes an expired channel lock straight to the settings database, as if the bot was down when it ended. The
    ///     overwrite is set to its locked state unless the test wants it missing.
    /// </summary>
    private async Task SeedExpiredChannelLockAsync(FakeChannel channel, ulong previouslyAllowed = 0, GuildId? guildId = null,
        bool setOverwrite = true)
    {
        var now = DateTimeOffset.UtcNow;
        var channelLock = ChannelLocked.Create(new ModeratorId(this.Guild.Moderator.Id), null, new ChannelId(channel.Id),
            guildId ?? this.GuildId, now.AddMinutes(-10), new PreviouslyAllowedPermissions((long)previouslyAllowed),
            new PreviouslyDeniedPermissions(0), now.AddMinutes(-1)).ShouldSucceed();
        await using (var settingsContext = this._database.CreateSettingsDbContext())
        {
            settingsContext.ChannelLocks.Add(channelLock);
            await settingsContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        if (!setOverwrite)
            return;
        this.Guild.SetOverwrite(channel.Id, this.Guild.Id, 0, 0, TextLock);
        this._bot.Gateway.Dispatch("CHANNEL_UPDATE", this.Guild.ChannelJson(channel));
        await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
    }

    private async Task SeedExpiredThreadLockAsync(FakeChannel thread)
    {
        var now = DateTimeOffset.UtcNow;
        await using var settingsContext = this._database.CreateSettingsDbContext();
        settingsContext.ThreadLocks.Add(ThreadLocked.Create(new ModeratorId(this.Guild.Moderator.Id), null, new ChannelId(thread.Id),
            this.GuildId, now.AddMinutes(-10), now.AddMinutes(-1)).ShouldSucceed());
        await settingsContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ChannelUnlocked> SingleChannelUnlockAsync()
    {
        await using var settingsContext = this._database.CreateSettingsDbContext();
        return await settingsContext.ChannelLocks.OfType<ChannelUnlocked>().SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ThreadUnlocked> SingleThreadUnlockAsync()
    {
        await using var settingsContext = this._database.CreateSettingsDbContext();
        return await settingsContext.ThreadLocks.OfType<ThreadUnlocked>().SingleAsync(TestContext.Current.CancellationToken);
    }
}
