// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Logging.MessageLogging;

public sealed class BulkMessageDeletedEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog)
    : IEventHandler<MessagesBulkDeletedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, MessagesBulkDeletedEventArgs args)
    {
        // DSharpPlus hasn't finished implementing nullable notations
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Guild is null) return;

        if (!await this._settingsModule.IsModuleEnabled(Module.MessageLog, args.Guild.GetGuildId())
                .GetOrElse(() => false))
            return;

        var messageIds = args.Messages.Select(x => x.GetMessageId()).ToHashSet();
        var guildId = args.Guild.GetGuildId();

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var messages = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.GuildId == guildId && messageIds.Contains(m.Id))
            .OrderBy(m => m.Id)
            .Select(m => new MessageDto
            {
                MessageId = m.Id,
                UserId = m.UserId,
                // ReSharper disable once AccessToDisposedClosure
                Username = dbContext.UsernameHistory
                    .Where(h => h.UserId == m.UserId)
                    .OrderByDescending(h => h.Timestamp)
                    .Select(h => (Username?)h.Username)
                    .FirstOrDefault(),
                Content = m.MessageHistory
                    .OfType<MessageHistoryContentEntry>()
                    .OrderByDescending(h => h.Timestamp)
                    .Select(h => (MessageContent?)h.Content)
                    .FirstOrDefault(),
                Attachments = m.Attachments.Select(a => a.AttachmentUrl.Value).ToArray()
            })
            .ToArrayAsync();

        if (messages.Length == 0)
            return;

        await dbContext.MessageHistory.AddRangeAsync(
            messages.Select(x =>
                new MessageDeletedEntry
                {
                    MessageId = x.MessageId, GuildId = guildId, Timestamp = DateTimeOffset.UtcNow
                }));
        await dbContext.SaveChangesAsync();

        if (!await this._settingsModule.IsLogChannelSet(GuildLogType.BulkMessageDeleted, guildId))
            return;

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Bulk Message Delete")
            .WithDescription($"**Message Count:** {messages.Length}\n" +
                             $"**Channel:** {args.Channel.Mention}\n" +
                             "Full message dump attached.")
            .WithColor(GrimoireColor.Red);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomMessage
        {
            GuildId = guildId,
            GuildLogType = GuildLogType.BulkMessageDeleted,
            Message = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddFile($"{DateTime.UtcNow:r}.txt", BuildBulkMessageLogFile(messages))
        });
    }

    /// <summary>A text dump in message order. The author is named from history, so it needs no lookups.</summary>
    private static MemoryStream BuildBulkMessageLogFile(IEnumerable<MessageDto> messages)
    {
        var stringBuilder = new StringBuilder();
        foreach (var msg in messages)
        {
            stringBuilder.Append("Author: ").Append(msg.Username?.Value ?? "Unknown User")
                .Append(" (").Append(msg.UserId).Append(")\n")
                .Append("Id: ").Append(msg.MessageId).Append('\n')
                .Append("Content: ").Append(msg.Content).Append('\n');
            if (msg.Attachments.Length > 0)
                stringBuilder.Append("Attachments: ").Append(string.Join("\n", msg.Attachments)).Append('\n');
            stringBuilder.AppendLine();
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(stringBuilder.ToString()));
    }

    private sealed record MessageDto
    {
        public required UserId UserId { get; init; }
        public required Username? Username { get; init; }
        public required MessageId MessageId { get; init; }
        public required MessageContent? Content { get; init; }
        public required string[] Attachments { get; init; }
    }
}
