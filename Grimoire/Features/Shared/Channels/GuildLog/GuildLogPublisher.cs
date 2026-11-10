// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Channels.GuildLog;

/// <summary>Posts the messages queued on <see cref="GuildLog" /> to each guild's configured log channel.</summary>
public sealed partial class GuildLogPublisher(
    GuildLog queue,
    DiscordClient discordClient,
    GuildIssueReporter issueReporter,
    ILogger<GuildLogPublisher> logger,
    SettingsModule settingsModule,
    IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : BackgroundService
{
    private readonly ILogger<GuildLogPublisher> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await foreach (var result in queue.Reader.ReadAllAsync(cancellationToken))
            try
            {
                if (!await settingsModule.IsModuleEnabled(result.GuildLogType.GetLogTypeModule(), result.GuildId,
                            cancellationToken)
                        .GetOrElse(() => false))
                    continue;

                var logChannelId = await settingsModule.GetLogChannelSetting(
                    result.GuildLogType,
                    result.GuildId,
                    cancellationToken);

                if (logChannelId.GetOrElse(() => null) is not { } logChannel)
                    continue;

                var channel = await discordClient.GetChannelOrDefaultAsync(logChannel);

                if (channel is null)
                {
                    this.Report(GuildIssueCatalog.LogChannelMissing(result.GuildId, result.GuildLogType, logChannel),
                        cancellationToken);
                    continue;
                }

                var builder = result.GetMessageBuilder();
                var message = await DiscordRetryPolicy.RetryDiscordCall(async _ =>
                {
                    // A failed attempt may have read part of a file, so each attempt starts the uploads over.
                    foreach (var file in builder.Files.Where(file => file.Stream.CanSeek))
                        file.Stream.Position = 0;
                    return await channel.SendMessageAsync(builder);
                }, cancellationToken);

                if (ShouldPurgeMessageAfterInterval(result.GuildLogType))
                    await this.ScheduleMessagePurge(
                        message.GetMessageId(),
                        channel.GetChannelId(),
                        result.GuildId,
                        cancellationToken);
            }
            // A timed-out request also throws OperationCanceledException; only shutdown may end the loop.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            // The log is lost, but the guild hears why. The daily throttle stops this looping when the channel the
            // bot can't post in is the mod log itself.
            catch (UnauthorizedException ex)
            {
                LogProcessError(this._logger, ex, result.GuildId, result.GuildLogType);
                if (await settingsModule.GetLogChannelSetting(result.GuildLogType, result.GuildId, cancellationToken)
                        .GetOrElse(() => null) is { } channelId)
                    this.Report(GuildIssueCatalog.CannotSendInChannel(result.GuildId, channelId), cancellationToken);
            }
            catch (Exception ex)
            {
                LogProcessError(this._logger, ex, result.GuildId, result.GuildLogType);
            }
    }

    /// <summary>
    ///     Reports without waiting: the report is posted through this same queue, and if the queue were full, waiting
    ///     for room would wait on this loop, the only reader.
    /// </summary>
    private void Report(GuildIssue issue, CancellationToken cancellationToken)
        => _ = issueReporter.ReportAsync(issue, cancellationToken).ContinueWith(
            task => LogReportFailure(this._logger, task.Exception!, issue.GuildId, issue.Code),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    [LoggerMessage(LogLevel.Error, "Could not report setup problem {Code} for guild {GuildId}")]
    private static partial void LogReportFailure(ILogger logger, Exception exception, GuildId guildId, string code);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
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

    /// <summary>Message log posts quote message content, so they are deleted after a while; the rest are kept.</summary>
    private static bool ShouldPurgeMessageAfterInterval(GuildLogType guildLogType)
        => guildLogType is GuildLogType.BulkMessageDeleted or GuildLogType.MessageEdited or GuildLogType.MessageDeleted;

    private async Task ScheduleMessagePurge(MessageId messageId, ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
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
