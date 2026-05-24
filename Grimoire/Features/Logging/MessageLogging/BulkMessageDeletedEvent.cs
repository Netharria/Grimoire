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
            .Select(m => new MessageDto
            {
                MessageId = m.Id,
                UserId = m.UserId,
                // ReSharper disable AccessToDisposedClosure
                Content = dbContext.MessageHistory
                    .OfType<MessageHistoryContentEntry>()
                    .Where(h => h.MessageId == m.Id)
                    .OrderByDescending(h => h.Timestamp)
                    .Select(h => (MessageContent?)h.Content)
                    .FirstOrDefault(),
                Attachments = m.Attachments
                    .Select(a => new AttachmentDto { Id = a.Id, FileName = a.FileName })
                // ReSharper restore AccessToDisposedClosure
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
                .AddFile($"{DateTime.UtcNow:r}.txt",
                    await BuildBulkMessageLogFile(messages, args.Guild))
        });
    }

    private static async Task<MemoryStream> BuildBulkMessageLogFile(
        IEnumerable<MessageDto> messages, DiscordGuild guild)
    {
        var stringBuilder = new StringBuilder();
        foreach (var msg in messages)
        {
            var author = await guild.GetMemberOrDefaultAsync(msg.UserId);
            stringBuilder.AppendFormat(
                    "Author: {0} ({1})\nId: {2}\nContent: {3}\n"
                    + (msg.Attachments.Any() ? "Attachments: {4}\n" : string.Empty),
                    author?.Mention ?? "Unknown User",
                    msg.UserId,
                    msg.MessageId,
                    msg.Content,
                    string.Join("\n", msg.Attachments.Select(x => x.FileName.Value)))
                .AppendLine();
        }

        var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream);
        await writer.WriteAsync(stringBuilder);
        await writer.FlushAsync();
        memoryStream.Position = 0;
        return memoryStream;
    }

    private sealed record MessageDto
    {
        public required UserId UserId { get; init; }
        public required MessageId MessageId { get; init; }
        public required MessageContent? Content { get; init; }
        public required IEnumerable<AttachmentDto> Attachments { get; init; }
    }
}
