// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace Grimoire.Features.Shared.Alerts;

public static class WarningReportFormatter
{
    public const int MaxEntries = 15;
    private const int MaxSampleLength = 150;

    /// <summary>Discord's limit for an embed description. DSharpPlus throws above it.</summary>
    private const int MaxDescriptionLength = 4096;

    /// <summary>Room kept for the "…and N more" line.</summary>
    private const int MoreLineLength = 64;

    /// <summary>
    ///     The report as a message: the embed, and when it couldn't show every warning, a text file with all of them in
    ///     full, so nothing is cut from the report.
    /// </summary>
    public static DiscordMessageBuilder BuildMessage(IReadOnlyList<AlertOccurrence> occurrences, DateOnly date)
    {
        var (embed, shown) = BuildEmbed(occurrences);
        var message = new DiscordMessageBuilder().AddEmbed(embed);
        return shown < occurrences.Count
            ? message.AddFile($"warning-report-{date:yyyy-MM-dd}.txt",
                new MemoryStream(Encoding.UTF8.GetBytes(BuildFullReport(occurrences))))
            : message;
    }

    public static DiscordEmbedBuilder Build(IReadOnlyList<AlertOccurrence> occurrences) => BuildEmbed(occurrences).Embed;

    private static (DiscordEmbedBuilder Embed, int Shown) BuildEmbed(IReadOnlyList<AlertOccurrence> occurrences)
    {
        var ordered = occurrences.OrderByDescending(x => x.Count).ToList();
        var description = new StringBuilder();

        // Stops at the first entry that wouldn't fit. A report that's too long would fail every minute, because the
        // rows it reports stay open until it's sent.
        var shown = 0;
        foreach (var entry in ordered.Take(MaxEntries).Select(FormatEntry))
        {
            if (description.Length + entry.Length > MaxDescriptionLength - MoreLineLength)
                break;
            description.Append(entry);
            shown++;
        }

        if (ordered.Count > shown)
            description.Append("…and ").Append(ordered.Count - shown).Append(" more warning types, in the attached file.");

        return (new DiscordEmbedBuilder()
            .WithColor(GrimoireColor.Yellow)
            .WithTitle("Daily warning report")
            .WithDescription(description.ToString())
            .WithFooter($"{ordered.Count} warning types, {ordered.Sum(x => x.Count)} occurrences"), shown);
    }

    /// <summary>Every warning, most frequent first, with its whole sample message and every guild.</summary>
    internal static string BuildFullReport(IReadOnlyList<AlertOccurrence> occurrences)
    {
        var report = new StringBuilder();
        foreach (var occurrence in occurrences.OrderByDescending(x => x.Count))
        {
            report.Append(occurrence.Count).Append("× ").Append(occurrence.AlertType);
            if (occurrence.ExceptionType is not null) report.Append(" (").Append(occurrence.ExceptionType).Append(')');
            report.Append('\n')
                .Append("First seen: ").Append(occurrence.FirstSeen.ToString("u", CultureInfo.InvariantCulture))
                .Append("   Last seen: ").Append(occurrence.LastSeen.ToString("u", CultureInfo.InvariantCulture)).Append('\n');
            if (occurrence.GuildIds.Length > 0)
                report.Append("Guilds: ").AppendJoin(", ", occurrence.GuildIds).Append('\n');
            report.Append(occurrence.SampleMessage).Append("\n\n");
        }

        return report.ToString();
    }

    private static string FormatEntry(AlertOccurrence occurrence)
    {
        var entry = new StringBuilder();
        entry.Append("**").Append(occurrence.Count).Append("×** ").Append(occurrence.AlertType);
        if (occurrence.ExceptionType is not null) entry.Append(" `").Append(occurrence.ExceptionType).Append('`');
        entry.Append("\nLast <t:").Append(occurrence.LastSeen.ToUnixTimeSeconds()).Append(":R>");
        if (occurrence.GuildIds.Length > 0) entry.Append(" · ").Append(occurrence.GuildIds.Length).Append(" guild(s)");
        entry.Append("\n> ").AppendLine(Truncate(occurrence.SampleMessage).ReplaceLineEndings(" "));
        return entry.ToString();
    }

    private static string Truncate(string value)
        => value.Length <= MaxSampleLength ? value : value[..MaxSampleLength] + "...";
}
