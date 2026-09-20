// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.GuildIssues;

public sealed partial class GuildIssueReporter(
    GuildLog guildLog,
    IAlertSender alerts,
    ILogger<GuildIssueReporter> logger,
    TimeProvider timeProvider)
{
    private readonly GuildIssueThrottle _throttle = new(timeProvider);

    /// <summary>
    ///     Logs the problem and counts it in the daily warning report every time, but only posts to the guild's
    ///     Moderation log once a day per problem so a repeating failure does not spam it.
    /// </summary>
    public async Task ReportAsync(GuildIssue issue, CancellationToken cancellationToken = default)
    {
        LogIssue(logger, issue.GuildId, issue.Code);
        alerts.Send(new Alert
        {
            Severity = AlertSeverity.Warning,
            Type = "GuildIssue",
            Discriminator = issue.Code,
            Message = issue.Summary,
            GuildId = issue.GuildId
        });

        if (this._throttle.TryAcquire(issue.GuildId, issue.Code) is not { } repeats)
            return;

        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = issue.GuildId,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Yellow,
            Title = "Setup problem",
            Description = $"{issue.Summary}\n**Suggested fix:** {issue.SuggestedFix}"
                          + (repeats > 0 ? $"\nThis happened {repeats} more time(s) since it was last shown." : string.Empty),
            Footer = "Shown at most once a day for each problem. Run /Diagnostics Check to review mutes and locks."
        }, cancellationToken);
    }

    [LoggerMessage(LogLevel.Warning, "Guild {GuildId} has a setup problem: {Code}")]
    private static partial void LogIssue(ILogger logger, GuildId guildId, string code);
}
