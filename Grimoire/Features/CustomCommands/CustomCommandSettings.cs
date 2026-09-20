// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.CustomCommands;

[Command("Commands")]
public sealed partial class CustomCommandSettings(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    GuildLog guildLog,
    IAlertSender alerts,
    ILogger<CustomCommandSettings> logger)
{
    /// <summary>Database failures are reported to the user as a friendly error, so record them here.</summary>
    private void ReportDatabaseFailure(Exception exception, string command)
    {
        LogDatabaseFailure(logger, exception, command);
        alerts.Send(new Alert
        {
            Severity = AlertSeverity.Warning,
            Type = "DatabaseError",
            Discriminator = command,
            Message = $"Database error while running {command}: {exception.Message}",
            Exception = exception
        });
    }

    [LoggerMessage(LogLevel.Error, "Database error while running custom command settings command {Command}")]
    private static partial void LogDatabaseFailure(ILogger logger, Exception exception, string command);
}
