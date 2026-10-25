// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.LogCleanup;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>Reactions on logged messages are saved, soft deleted when removed, and purged with their message.</summary>
[Collection("Test collection")]
public sealed class ReactionTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly FakeEmoji ThumbsUp = new FakeEmoji.Standard("👍");
    private static readonly FakeEmoji Party = new FakeEmoji.Custom(1_100_000_000_000_000_301, "party", true);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private FakeChannel Channel => this._bot.Guild.General;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureMessageLogAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    [Fact]
    public async Task ReactingToALoggedMessage_SavesTheReaction()
    {
        var before = DateTimeOffset.UtcNow;
        var messageId = await this.SendLoggedMessageAsync();

        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, Party);

        var reaction = (await this.WaitForReactionsAsync(messageId, reactions => reactions.Count == 1))
            .ShouldHaveSingleItem();
        reaction.UserId.ShouldBe(new UserId(this.Guild.User1.Id));
        reaction.GuildId.ShouldBe(new GuildId(this.Guild.Id));
        reaction.Emoji.ShouldBe(ReactionEmoji.Custom.Create(new EmojiId(1_100_000_000_000_000_301), "party", true)
            .ShouldSucceed());
        reaction.ReactedAt.ShouldBeGreaterThanOrEqualTo(before);
        reaction.RemovedAt.ShouldBeNull();

        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemovingAReaction_SoftDeletesIt_AndReactingAgainRevivesIt()
    {
        var messageId = await this.SendLoggedMessageAsync();
        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, ThumbsUp);
        await this.WaitForReactionsAsync(messageId, reactions => reactions.Count == 1);

        this._bot.RemoveReaction(this.Guild.User1, this.Channel, messageId, ThumbsUp);
        var removed = (await this.WaitForReactionsAsync(messageId, reactions => reactions.All(r => r.IsRemoved)))
            .ShouldHaveSingleItem();
        removed.RemovedAt.ShouldNotBeNull();

        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, ThumbsUp);
        var revived = (await this.WaitForReactionsAsync(messageId, reactions => reactions.All(r => !r.IsRemoved)))
            .ShouldHaveSingleItem();
        revived.RemovedAt.ShouldBeNull();
        revived.ReactedAt.ShouldBeGreaterThan(removed.ReactedAt);
    }

    [Fact]
    public async Task RemovingAnEmojiForEveryone_SoftDeletesOnlyThatEmoji()
    {
        var messageId = await this.SendLoggedMessageAsync();
        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, ThumbsUp);
        this._bot.AddReaction(this.Guild.Moderator, this.Channel, messageId, ThumbsUp);
        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, Party);
        await this.WaitForReactionsAsync(messageId, reactions => reactions.Count == 3);

        this._bot.RemoveEmoji(this.Channel, messageId, ThumbsUp);

        var reactions = await this.WaitForReactionsAsync(messageId,
            current => current.Count(reaction => reaction.IsRemoved) == 2);
        reactions.Where(reaction => reaction.IsRemoved).Select(reaction => reaction.Emoji.ToString())
            .ShouldAllBe(emoji => emoji == "👍");
        reactions.Single(reaction => !reaction.IsRemoved).Emoji.ToString().ShouldBe("<a:party:1100000000000000301>");
    }

    [Fact]
    public async Task ClearingAllReactions_SoftDeletesEveryReactionOnTheMessage()
    {
        var messageId = await this.SendLoggedMessageAsync();
        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, ThumbsUp);
        this._bot.AddReaction(this.Guild.Moderator, this.Channel, messageId, Party);
        (await this.WaitForReactionsAsync(messageId, reactions => reactions.Count == 2)).Count.ShouldBe(2);

        this._bot.RemoveAllReactions(this.Channel, messageId);

        var reactions = await this.WaitForReactionsAsync(messageId, current => current.All(r => r.IsRemoved));
        reactions.Count.ShouldBe(2);
        reactions.ShouldAllBe(reaction => reaction.IsRemoved);
    }

    [Fact]
    public async Task ReactingToAMessageTheLogNeverSaved_SavesNothing()
    {
        const ulong unloggedMessageId = 1_100_000_000_000_009_999;

        this._bot.AddReaction(this.Guild.User1, this.Channel, unloggedMessageId, ThumbsUp);

        // Handlers run concurrently in the background, so absence can only be checked over a window.
        (await this.WaitForReactionsAsync(unloggedMessageId, reactions => reactions.Count > 0, TimeSpan.FromSeconds(1)))
            .ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>With the message log off, reactions on a logged message are neither saved nor marked removed.</summary>
    [Fact]
    public async Task WithTheMessageLogOff_ReactionsAreIgnored()
    {
        var messageId = await this.SendLoggedMessageAsync();
        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, ThumbsUp);
        await this.WaitForReactionsAsync(messageId, reactions => reactions.Count == 1);
        await this._bot.SetModuleAsync(Grimoire.Settings.Enums.Module.MessageLog, false);

        this._bot.AddReaction(this.Guild.Moderator, this.Channel, messageId, Party);
        this._bot.RemoveReaction(this.Guild.User1, this.Channel, messageId, ThumbsUp);

        var reactions = await this.WaitForReactionsAsync(messageId,
            current => current.Count > 1 || current.Any(reaction => reaction.IsRemoved), TimeSpan.FromSeconds(2));
        reactions.ShouldHaveSingleItem().IsRemoved.ShouldBeFalse();
    }

    [Fact]
    public async Task PurgingOldMessages_DeletesTheirReactions_RemovedOrNot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var seed = this._database.CreateDbContext())
        {
            seed.Messages.AddRange(this.Message(1, DateTimeOffset.UtcNow.AddDays(-40)), this.Message(2, DateTimeOffset.UtcNow));
            seed.Reactions.AddRange(
                this.Reaction(1, "👍", null),
                this.Reaction(1, "🎉", DateTimeOffset.UtcNow.AddDays(-39)),
                this.Reaction(2, "👍", null));
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using (var cleanup = this._database.CreateDbContext())
            await CleanupLogsBackgroundTask.DeleteOldMessageAndUserLogs(cleanup, cancellationToken);

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Reactions.Select(reaction => reaction.MessageId).ToListAsync(cancellationToken))
            .ShouldBe([new MessageId(2)]);
    }

    private async Task<ulong> SendLoggedMessageAsync()
    {
        var messageId = this._bot.SendMessage(this.Guild.Moderator, this.Channel, "react to this");
        var logged = await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId));
        }, isLogged => isLogged);
        logged.ShouldBeTrue("the message log should save the message before anyone reacts to it");
        return messageId;
    }

    private Task<List<Reaction>> WaitForReactionsAsync(ulong messageId, Func<List<Reaction>, bool> isDone,
        TimeSpan? timeout = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Reactions.AsNoTracking()
                .Where(reaction => reaction.MessageId == new MessageId(messageId))
                .ToListAsync();
        }, isDone, timeout);

    private Message Message(ulong id, DateTimeOffset createdAt) => new()
    {
        Id = new MessageId(id),
        UserId = new UserId(this.Guild.User1.Id),
        GuildId = new GuildId(this.Guild.Id),
        ChannelId = new ChannelId(this.Channel.Id),
        CreatedTimestamp = createdAt
    };

    private Reaction Reaction(ulong messageId, string emoji, DateTimeOffset? removedAt) => new()
    {
        MessageId = new MessageId(messageId),
        UserId = new UserId(this.Guild.User1.Id),
        GuildId = new GuildId(this.Guild.Id),
        Emoji = ReactionEmoji.Standard.Create(emoji).ShouldSucceed(),
        ReactedAt = DateTimeOffset.UtcNow.AddDays(-39),
        RemovedAt = removedAt
    };
}
