// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.Common;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Logging.MessageLogging;

/// <summary>
///     Records reactions on logged messages. Removing a reaction soft deletes it by setting
///     <see cref="Reaction.RemovedAt" />, and reacting again revives it. Reactions are deleted with their message when
///     the log cleanup purges old messages, through the cascading foreign key.
/// </summary>
public sealed partial class ReactionEvents(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    TimeProvider timeProvider,
    ILogger<ReactionEvents> logger)
    : IEventHandler<MessageReactionAddedEventArgs>,
        IEventHandler<MessageReactionRemovedEventArgs>,
        IEventHandler<MessageReactionRemovedEmojiEventArgs>,
        IEventHandler<MessageReactionsClearedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<ReactionEvents> _logger = logger;
    private readonly SettingsModule _settingsModule = settingsModule;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task HandleEventAsync(DiscordClient sender, MessageReactionAddedEventArgs args)
    {
        if (args.Guild is null || !await this.IsMessageLogEnabledAsync(args.Guild))
            return;
        if (this.ToReactionEmoji(args.Emoji) is not { } emoji)
            return;

        await this.SaveReactionAsync(new Reaction
        {
            MessageId = new MessageId(args.Message.Id),
            UserId = args.User.GetUserId(),
            GuildId = args.Guild.GetGuildId(),
            Emoji = emoji,
            ReactedAt = this._timeProvider.GetUtcNow()
        });
    }

    public async Task HandleEventAsync(DiscordClient sender, MessageReactionRemovedEventArgs args)
    {
        if (args.Guild is null || !await this.IsMessageLogEnabledAsync(args.Guild))
            return;
        if (this.ToReactionEmoji(args.Emoji) is not { } emoji)
            return;

        var messageId = new MessageId(args.Message.Id);
        var userId = args.User.GetUserId();
        await this.SoftDeleteAsync(reactions => reactions.Where(reaction =>
            reaction.MessageId == messageId && reaction.UserId == userId && reaction.Emoji == emoji));
    }

    public async Task HandleEventAsync(DiscordClient sender, MessageReactionRemovedEmojiEventArgs args)
    {
        if (args.Guild is null || !await this.IsMessageLogEnabledAsync(args.Guild))
            return;
        if (this.ToReactionEmoji(args.Emoji) is not { } emoji)
            return;

        var messageId = new MessageId(args.Message.Id);
        await this.SoftDeleteAsync(reactions => reactions.Where(reaction =>
            reaction.MessageId == messageId && reaction.Emoji == emoji));
    }

    /// <summary>This event carries only the message, so the guild comes from the cached channel.</summary>
    public async Task HandleEventAsync(DiscordClient sender, MessageReactionsClearedEventArgs args)
    {
        if (args.Message.Channel?.Guild is not { } guild || !await this.IsMessageLogEnabledAsync(guild))
            return;

        var messageId = new MessageId(args.Message.Id);
        await this.SoftDeleteAsync(reactions => reactions.Where(reaction => reaction.MessageId == messageId));
    }

    private Task<bool> IsMessageLogEnabledAsync(DiscordGuild guild)
        => this._settingsModule.IsModuleEnabled(Module.MessageLog, guild.GetGuildId()).GetOrElse(() => false);

    private ReactionEmoji? ToReactionEmoji(DiscordEmoji emoji)
        => emoji.ToReactionEmoji().Match<ReactionEmoji?>(
            valid => valid,
            errors =>
            {
                LogUnrecognizedEmoji(this._logger, emoji.Id, emoji.Name, string.Join("; ", errors.Select(e => e.Message)));
                return null;
            });

    /// <summary>
    ///     Revives the reaction if it was recorded before, otherwise adds it. Reactions are only kept for messages
    ///     the message log saved, so a reaction on any other message is skipped.
    /// </summary>
    private async Task SaveReactionAsync(Reaction reaction)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        if (await ReviveAsync(dbContext, reaction) > 0
            || !await dbContext.Messages.AnyAsync(message => message.Id == reaction.MessageId))
            return;

        dbContext.Reactions.Add(reaction);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        // Either the same reaction arrived twice at once, or the message was purged in between.
        catch (Exception ex) when (ex is UniqueConstraintException or ReferenceConstraintException)
        {
            LogReactionRace(this._logger, reaction.MessageId, ex.GetType().Name);
        }
    }

    private static Task<int> ReviveAsync(GrimoireDbContext dbContext, Reaction reaction)
        => dbContext.Reactions
            .Where(existing => existing.MessageId == reaction.MessageId
                               && existing.UserId == reaction.UserId
                               && existing.Emoji == reaction.Emoji)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(existing => existing.ReactedAt, reaction.ReactedAt)
                .SetProperty(existing => existing.RemovedAt, (DateTimeOffset?)null));

    private async Task SoftDeleteAsync(Func<IQueryable<Reaction>, IQueryable<Reaction>> select)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var removedAt = this._timeProvider.GetUtcNow();
        await select(dbContext.Reactions)
            .Where(reaction => reaction.RemovedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(reaction => reaction.RemovedAt, removedAt));
    }

    [LoggerMessage(LogLevel.Warning, "Skipped a reaction with an emoji Grimoire cannot store: id {EmojiId}, name {EmojiName}. {Errors}")]
    static partial void LogUnrecognizedEmoji(ILogger logger, ulong emojiId, string? emojiName, string errors);

    [LoggerMessage(LogLevel.Debug, "Skipped saving a reaction on message {MessageId}: {Reason}")]
    static partial void LogReactionRace(ILogger logger, MessageId messageId, string reason);
}
