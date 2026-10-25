// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Channels.GuildLog;

/// <summary>Posts the messages queued on <see cref="GuildLog" /> to each guild's configured log channel.</summary>
public sealed partial class GuildLogPublisher(
    GuildLog queue,
    DiscordClient discordClient,
    ILogger<GuildLogPublisher> logger,
    SettingsModule settingsModule,
    IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : BackgroundService
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly DiscordClient _discordClient = discordClient;
    private readonly ILogger<GuildLogPublisher> _logger = logger;
    private readonly GuildLog _queue = queue;
    private readonly SettingsModule _settingsModule = settingsModule;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await foreach (var result in this._queue.Reader.ReadAllAsync(cancellationToken))
            try
            {
                if (!await this._settingsModule.IsModuleEnabled(result.GuildLogType.GetLogTypeModule(), result.GuildId,
                            cancellationToken)
                        .GetOrElse(() => false))
                    continue;

                var logChannelId = await this._settingsModule.GetLogChannelSetting(
                    result.GuildLogType,
                    result.GuildId,
                    cancellationToken);

                if (logChannelId.GetOrElse(() => null) is not { } logChannel)
                    continue;

                var channel = await this._discordClient.GetChannelOrDefaultAsync(logChannel, cancellationToken);

                if (channel is null)
                    continue;

                var message = await DiscordRetryPolicy.RetryDiscordCall(
                    async _ => await channel.SendMessageAsync(result.GetMessageBuilder()), cancellationToken);

                if (ShouldPurgeMessageAfterInterval(result.GuildLogType))
                    await this.ScheduleMessagePurge(
                        message.GetMessageId(),
                        channel.GetChannelId(),
                        result.GuildId,
                        cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogProcessError(this._logger, ex, result.GuildId, result.GuildLogType);
            }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this._queue.Complete();
        await base.StopAsync(cancellationToken);
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Failed to process guild log. GuildId={GuildId}, GuildLogType={GuildLogType}")]
    private static partial void LogProcessError(
        ILogger logger,
        Exception exception,
        GuildId guildId,
        GuildLogType guildLogType);

    private static bool ShouldPurgeMessageAfterInterval(GuildLogType guildLogType)
    {
        return guildLogType switch
        {
            GuildLogType.Moderation => false,
            GuildLogType.Leveling => false,
            GuildLogType.BulkMessageDeleted => true,
            GuildLogType.MessageEdited => true,
            GuildLogType.MessageDeleted => true,
            GuildLogType.UserJoined => false,
            GuildLogType.UserLeft => false,
            GuildLogType.AvatarUpdated => false,
            GuildLogType.NicknameUpdated => false,
            GuildLogType.UsernameUpdated => false,
            GuildLogType.PublicModeration => false,
            _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null)
        };
    }

    private async Task ScheduleMessagePurge(MessageId messageId, ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var logMessage = new OldLogMessage
        {
            ChannelId = channelId,
            GuildId = guildId,
            Id = messageId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await dbContext.OldLogMessages.AddAsync(logMessage, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
