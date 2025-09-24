// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.LogCleanup;

internal sealed class CleanupLogsBackgroundTask(
    IServiceProvider serviceProvider,
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    ILogger<CleanupLogsBackgroundTask> logger)
    : GenericBackgroundService(serviceProvider, dbContextFactory, logger, TimeSpan.FromMinutes(1))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();
        await using var dbContext = await this.DbContextFactory.CreateDbContextAsync(stoppingToken);
        var oldDate = DateTime.UtcNow - TimeSpan.FromDays(30);
        var oldLogMessages = await dbContext.OldLogMessages
            .AsNoTracking()
            .Where(oldLogMessage => oldLogMessage.CreatedAt < oldDate)
            .GroupBy(oldLogMessage => new { oldLogMessage.ChannelId })
            .Select(oldLogMessages => new
            {
                oldLogMessages.Key.ChannelId, MessageIds = oldLogMessages.Select(x => x.Id).ToArray()
            }).AsAsyncEnumerable()
            .SelectAwait(async channel =>
                new
                {
                    DiscordChannel = await discordClient.GetChannelOrDefaultAsync(channel.ChannelId),
                    DatabaseChannel = channel
                })
            .SelectMany(channelInfo =>
                channelInfo.DatabaseChannel.MessageIds
                    .ToAsyncEnumerable()
                    .SelectAwait(async messageId =>
                        await DeleteMessageAsync(channelInfo.DiscordChannel, messageId, stoppingToken))
            ).ToArrayAsync(stoppingToken);

        await DeleteOldMessageAndUserLogs(dbContext, stoppingToken);
        await UpdateOldGrimoireLogEntries(dbContext, oldLogMessages, stoppingToken);
    }

    private static async Task<DeleteMessageResult> DeleteMessageAsync(DiscordChannel? channel, ulong messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (channel is null)
                return new DeleteMessageResult { WasSuccessful = false, MessageId = messageId };
            var message = await channel.GetMessageAsync(messageId).WaitAsync(cancellationToken);
            await message.DeleteAsync().WaitAsync(cancellationToken);
            return new DeleteMessageResult { WasSuccessful = true, MessageId = messageId };
        }
        catch (Exception)
        {
            return new DeleteMessageResult { WasSuccessful = false, MessageId = messageId };
        }
    }

    private static async Task UpdateOldGrimoireLogEntries(GrimoireDbContext grimoireDbContext,
        ICollection<DeleteMessageResult> deleteMessageResults, CancellationToken cancellationToken)
    {
        var successMessages = deleteMessageResults
            .Where(x => x.WasSuccessful)
            .Select(x => x.MessageId)
            .ToArray();

        if (successMessages.Length != 0)
            await grimoireDbContext.OldLogMessages
                .Where(message => successMessages.Contains(message.Id))
                .ExecuteDeleteAsync(cancellationToken);


        var erroredMessages = deleteMessageResults
            .Where(x => !x.WasSuccessful)
            .Select(x => x.MessageId)
            .ToArray();

        if (erroredMessages.Length != 0)
        {
            await grimoireDbContext.OldLogMessages
                .Where(message => erroredMessages.Contains(message.Id))
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.TimesTried, p => p.TimesTried + 1),
                    cancellationToken);

            await grimoireDbContext.OldLogMessages
                .Where(x => x.TimesTried >= 3)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static async Task DeleteOldMessageAndUserLogs(GrimoireDbContext grimoireDbContext,
        CancellationToken stoppingToken)
    {
        var oldDate = DateTimeOffset.UtcNow - TimeSpan.FromDays(31);
        await grimoireDbContext.Messages
            .Where(x => x.CreatedTimestamp <= oldDate)
            .ExecuteDeleteAsync(stoppingToken);

        await grimoireDbContext.Avatars
            .Select(x => new { x.GuildId, x.UserId })
            .Distinct()
            .SelectMany(x => grimoireDbContext.Avatars
                .Where(avatar => avatar.UserId == x.UserId && avatar.GuildId == x.GuildId)
                .OrderByDescending(avatar => avatar.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(stoppingToken);

        await grimoireDbContext.NicknameHistory
            .Select(x => new { x.GuildId, x.UserId })
            .Distinct()
            .SelectMany(x => grimoireDbContext.NicknameHistory
                .Where(y => y.UserId == x.UserId && y.GuildId == x.GuildId)
                .OrderByDescending(nicknameHistory => nicknameHistory.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(stoppingToken);

        await grimoireDbContext.UsernameHistory
            .Select(x => x.UserId)
            .Distinct()
            .SelectMany(x => grimoireDbContext.UsernameHistory
                .Where(y => y.UserId == x)
                .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(stoppingToken);
    }

    private readonly struct DeleteMessageResult
    {
        public bool WasSuccessful { get; init; }
        public ulong MessageId { get; init; }
    }
}
