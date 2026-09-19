// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Features.Shared.Alerts;

public static class AlertMessageFormatter
{
    private const int DiscordMessageLimit = 2000;

    public static string FormatUrgent(Alert alert, int suppressedRepeats)
    {
        var header = new StringBuilder("**[URGENT]** ").Append(alert.Type);
        if (alert.Discriminator is not null) header.Append(" `").Append(alert.Discriminator).Append('`');
        if (alert.GuildId is { } guildId) header.Append(" (guild ").Append(guildId).Append(')');
        if (suppressedRepeats > 0) header.Append(" — repeated ").Append(suppressedRepeats).Append(" more times since the last alert");

        var body = new StringBuilder(header.ToString()).Append('\n').Append(alert.Message);
        if (alert.Exception is not null)
            body.Append("\n```csharp\n").Append(FormatException(alert.Exception)).Append("\n```");

        return body.Length <= DiscordMessageLimit ? body.ToString() : body.ToString()[..(DiscordMessageLimit - 3)] + "...";
    }

    private static string FormatException(Exception exception)
    {
        var messages = new StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
            messages.AppendLine(current.Message);

        var shortStackTrace = exception.StackTrace is null
            ? string.Empty
            : string.Join('\n', exception.StackTrace.Split('\n')
                .Where(x => x.StartsWith("   at Grimoire", StringComparison.OrdinalIgnoreCase))
                .Select(x => x[(x.IndexOf(" in ", StringComparison.OrdinalIgnoreCase) + 4)..])
                .Select(x => '\"' + x.Replace(":line", "\" line")));

        return $"{messages}\n{shortStackTrace}".Replace("```", "'''");
    }
}
