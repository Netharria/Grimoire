// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Features.Shared.Alerts;

public static class WarningReportFormatter
{
    public const int MaxEntries = 15;
    private const int MaxSampleLength = 150;

    public static DiscordEmbedBuilder Build(IReadOnlyList<AlertOccurrence> occurrences)
    {
        var ordered = occurrences.OrderByDescending(x => x.Count).ToList();
        var description = new StringBuilder();

        foreach (var occurrence in ordered.Take(MaxEntries))
        {
            description.Append("**").Append(occurrence.Count).Append("×** ").Append(occurrence.AlertType);
            if (occurrence.ExceptionType is not null) description.Append(" `").Append(occurrence.ExceptionType).Append('`');
            description.Append("\nLast <t:").Append(occurrence.LastSeen.ToUnixTimeSeconds()).Append(":R>");
            if (occurrence.GuildIds.Length > 0) description.Append(" · ").Append(occurrence.GuildIds.Length).Append(" guild(s)");
            description.Append("\n> ").AppendLine(Truncate(occurrence.SampleMessage).ReplaceLineEndings(" "));
        }

        if (ordered.Count > MaxEntries)
            description.Append("…and ").Append(ordered.Count - MaxEntries).Append(" more warning types.");

        return new DiscordEmbedBuilder()
            .WithColor(GrimoireColor.Yellow)
            .WithTitle("Daily warning report")
            .WithDescription(description.ToString())
            .WithFooter($"{ordered.Count} warning types, {ordered.Sum(x => x.Count)} occurrences");
    }

    private static string Truncate(string value)
        => value.Length <= MaxSampleLength ? value : value[..MaxSampleLength] + "...";
}
