// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Channels.GuildLog;

/// <summary>Posts the messages queued on <see cref="GuildLog" /> to each guild's configured log channel.</summary>
/// <remarks>
///     The post goes through the Discord edge (spec 1.4) and isn't retried (Q25): a 502 may come after Discord already
///     posted it, and a retry would post it twice. So one failed post loses that log only, and the single reader moves
///     straight on instead of holding every guild's logs for the length of a retry (defect 11).
/// </remarks>
public sealed partial class GuildLogPublisher(
    GuildLog queue,
    GrimoireEnv env,
    GuildIssueReporter issueReporter,
    ILogger<GuildLogPublisher> logger,
    SettingsModule settingsModule,
    IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : BackgroundService
{
    private readonly ILogger<GuildLogPublisher> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(cancellationToken))
            try
            {
                await this.PublishAsync(message, cancellationToken);
            }
            // Only shutdown may end the loop.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogProcessError(this._logger, ex, message.Guild, message.Type);
            }
    }

    private async Task PublishAsync(GuildLogMessage message, CancellationToken cancellationToken)
    {
        // Checked again here: the setting can change while the message waits in the queue.
        if (!await settingsModule.IsModuleEnabled(message.Type.GetLogTypeModule(), message.Guild, cancellationToken)
                .OrDefaultDuringMigration(false))
            return;

        if (await settingsModule.GetLogChannelSetting(message.Type, message.Guild, cancellationToken)
                .OrNullDuringMigration() is not { } logChannel)
            return;

        // No token: DSharpPlus's send can't be cancelled anyway, and registering on the service's stopping token
        // races the host disposing it at shutdown (ObjectDisposedException). Shutdown is still seen below.
        using var envIO = EnvIO.New();
        var posted = await Discord<GrimoireRuntime>.SendMessage(logChannel, message.Message)
            .RunAsync(new GrimoireRuntime(env), envIO);

        switch (posted)
        {
            case Fin<MessageId>.Succ(var id) when ShouldPurgeMessageAfterInterval(message.Type):
                await this.ScheduleMessagePurge(id, logChannel, message.Guild, cancellationToken);
                break;
            case Fin<MessageId>.Succ:
                break;
            // The guild hears why its logs are lost. The daily throttle stops this looping when the channel the bot
            // can't post in is the mod log itself.
            case Fin<MessageId>.Fail(NotFound):
                this.Report(GuildIssueCatalog.LogChannelMissing(message.Guild, message.Type, logChannel),
                    cancellationToken);
                break;
            case Fin<MessageId>.Fail(Forbidden refused):
                LogPostRefused(this._logger, message.Guild, message.Type, refused.Message);
                this.Report(GuildIssueCatalog.CannotSendInChannel(message.Guild, logChannel), cancellationToken);
                break;
            case Fin<MessageId>.Fail(var error) when cancellationToken.IsCancellationRequested:
                LogShutdown(this._logger, message.Guild, message.Type, error.Message);
                break;
            case Fin<MessageId>.Fail(var error):
                LogPostLost(this._logger, Functional.Boundary.Boundary<GrimoireRuntime>.ExceptionOf(error).OrNull(),
                    message.Guild, message.Type, error.Message);
                break;
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

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "A guild log post was lost, not retried (Q25). GuildId={GuildId}, GuildLogType={GuildLogType}: {Reason}")]
    private static partial void LogPostLost(ILogger logger, Exception? exception, GuildId guildId,
        GuildLogType guildLogType, string reason);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Failed to process guild log: Discord refused the post. GuildId={GuildId}, GuildLogType={GuildLogType}: {Reason}")]
    private static partial void LogPostRefused(ILogger logger, GuildId guildId, GuildLogType guildLogType,
        string reason);

    [LoggerMessage(LogLevel.Information,
        "A guild log post was stopped by shutdown. GuildId={GuildId}, GuildLogType={GuildLogType}: {Reason}")]
    private static partial void LogShutdown(ILogger logger, GuildId guildId, GuildLogType guildLogType, string reason);

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
