// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     Thread locks, cases MOD-159 to MOD-168 in <c>docs/testing/02-moderation-actions.md</c>. Threads inherit their
///     parent channel's permissions, so a lock can't use permission overwrites. Instead the bot deletes new messages
///     and reactions from members without ManageMessages until the lock is lifted. The <c>/Lock</c> and
///     <c>/Unlock</c> replies are covered in backlog item A-10, and lifting at expiry in A-11.
/// </summary>
[Collection("Test collection")]
public sealed class ThreadLockTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string LockReason = "Thread is locked.";
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);
    private static readonly FakeEmoji ThumbsUp = new FakeEmoji.Standard("👍");

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>MOD-159, MOD-165 and MOD-166: public, private and forum threads all enforce the lock.</summary>
    [Theory]
    [InlineData("public")]
    [InlineData("private")]
    [InlineData("forum")]
    public async Task MemberMessageInALockedThread_IsDeleted(string kind)
    {
        var thread = this.Thread(kind);
        await this._bot.LockThreadAsync(thread, TimeSpan.FromMinutes(2));

        var messageId = this._bot.SendMessage(this.Guild.User1, thread, "can I still post?");

        var delete = await this._bot.Rest.WaitForRequestAsync(this.IsMessageDelete(thread, messageId));
        delete.AuditLogReason.ShouldBe(LockReason);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-160.</summary>
    [Fact]
    public async Task MemberReactionInALockedThread_IsRemoved()
    {
        await this._bot.LockThreadAsync(this.Guild.PublicThread, TimeSpan.FromMinutes(2));
        const ulong messageId = 1_100_000_000_000_007_001;

        this._bot.AddReaction(this.Guild.User1, this.Guild.PublicThread, messageId, ThumbsUp);

        var removal = await this._bot.Rest.WaitForRequestAsync(request =>
            request.Method == HttpMethod.Delete
            && request.Path == $"channels/{this.Guild.PublicThread.Id}/messages/{messageId}/reactions/👍/{this.Guild.User1.Id}");
        removal.AuditLogReason.ShouldBe(LockReason);
    }

    /// <summary>MOD-161: members with ManageMessages, whether from a role or Administrator, are not affected.</summary>
    [Theory]
    [InlineData("mod")]
    [InlineData("admin")]
    public async Task StaffMessagesAndReactions_StayInALockedThread(string who)
    {
        var staff = who == "mod" ? this.Guild.Moderator : this.Guild.Admin;
        var thread = this.Guild.PublicThread;
        await this._bot.LockThreadAsync(thread, TimeSpan.FromMinutes(2));

        var messageId = this._bot.SendMessage(staff, thread, "staff message");
        this._bot.AddReaction(staff, thread, messageId, ThumbsUp);

        (await this.AnythingDeletedAsync(thread)).ShouldBeFalse();
    }

    /// <summary>MOD-162: the bot's own messages stay, because its role has Administrator.</summary>
    [Fact]
    public async Task BotsOwnMessage_StaysInALockedThread()
    {
        var thread = this.Guild.PublicThread;
        await this._bot.LockThreadAsync(thread, TimeSpan.FromMinutes(2));

        this._bot.SendMessage(this.Guild.Bot, thread, "a command reply");

        (await this.AnythingDeletedAsync(thread)).ShouldBeFalse();
    }

    [Fact]
    public async Task MemberMessageInAThreadThatIsNotLocked_Stays()
    {
        var thread = this.Guild.PublicThread;

        this._bot.SendMessage(this.Guild.User1, thread, "normal message");

        (await this.AnythingDeletedAsync(thread)).ShouldBeFalse();
    }

    /// <summary>
    ///     MOD-163 and MOD-167: an unlock row lifts the lock, and the newest row wins when a thread is locked again.
    ///     <c>/Unlock</c> writes that row, and so does the lock background task when the timer runs out.
    /// </summary>
    [Fact]
    public async Task UnlockingLiftsTheLock_AndLockingAgainRestoresIt()
    {
        var thread = this.Guild.PublicThread;
        await this._bot.LockThreadAsync(thread, TimeSpan.FromMinutes(2));
        await this._bot.UnlockThreadAsync(thread);

        this._bot.SendMessage(this.Guild.User1, thread, "unlocked now");
        (await this.AnythingDeletedAsync(thread)).ShouldBeFalse("the thread was unlocked");

        await this._bot.LockThreadAsync(thread, TimeSpan.FromMinutes(5));
        var messageId = this._bot.SendMessage(this.Guild.User1, thread, "locked again");
        await this._bot.Rest.WaitForRequestAsync(this.IsMessageDelete(thread, messageId));
    }

    /// <summary>MOD-168.</summary>
    [Fact(Skip = "Fails: LockedTreadEvents never checks the Moderation module, so it still deletes messages " +
                 "when the module is off. KI-12")]
    public async Task WithModerationDisabled_MessagesInALockedThreadStay()
    {
        var thread = this.Guild.PublicThread;
        await this._bot.LockThreadAsync(thread, TimeSpan.FromMinutes(2));
        await this._bot.SetModuleAsync(Module.Moderation, false);

        this._bot.SendMessage(this.Guild.User1, thread, "module is off");

        (await this.AnythingDeletedAsync(thread)).ShouldBeFalse();
    }

    private FakeChannel Thread(string kind) => kind switch
    {
        "public" => this.Guild.PublicThread,
        "private" => this.Guild.PrivateThread,
        "forum" => this.Guild.ForumPost,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private Func<RecordedRequest, bool> IsMessageDelete(FakeChannel thread, ulong messageId)
        => request => request.Method == HttpMethod.Delete
                      && request.Path == $"channels/{thread.Id}/messages/{FakeUser.Snowflake(messageId)}";

    /// <summary>Handlers run in the background, so absence is checked over a window.</summary>
    private Task<bool> AnythingDeletedAsync(FakeChannel thread)
        => this._bot.Rest.ReceivesRequestWithinAsync(request =>
                request.Method == HttpMethod.Delete
                && request.Path.StartsWith($"channels/{thread.Id}/messages/", StringComparison.Ordinal),
            NothingHappensWindow);
}
