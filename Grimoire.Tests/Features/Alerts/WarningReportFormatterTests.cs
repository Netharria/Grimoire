// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;

namespace Grimoire.Tests.Features.Alerts;

public sealed class WarningReportFormatterTests
{
    private static readonly DateTimeOffset _at = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AlertOccurrence Occurrence(string type, int count, string message = "message")
        => new()
        {
            AlertKey = type,
            AlertType = type,
            Count = count,
            FirstSeen = _at,
            LastSeen = _at,
            SampleMessage = message,
            GuildIds = [new GuildId(1)]
        };

    private static string Describe(IReadOnlyList<AlertOccurrence> occurrences)
        => WarningReportFormatter.Build(occurrences).Build().Description!;

    [Fact]
    public void Entries_are_ordered_by_count_descending()
    {
        var description = Describe([Occurrence("Rare", 1), Occurrence("Common", 9)]);

        description.IndexOf("Common", StringComparison.Ordinal)
            .ShouldBeLessThan(description.IndexOf("Rare", StringComparison.Ordinal));
    }

    [Fact]
    public void Footer_summarizes_types_and_occurrences()
        => WarningReportFormatter.Build([Occurrence("A", 3), Occurrence("B", 4)])
            .Build().Footer!.Text.ShouldBe("2 warning types, 7 occurrences");

    [Fact]
    public void Entries_beyond_the_limit_are_summarized()
    {
        var occurrences = Enumerable.Range(0, WarningReportFormatter.MaxEntries + 3)
            .Select(i => Occurrence($"Type{i}", 100 - i))
            .ToList();

        Describe(occurrences).ShouldContain("…and 3 more warning types, in the attached file.");
    }

    [Fact]
    public void Long_multiline_samples_are_flattened_and_truncated()
    {
        var description = Describe([Occurrence("A", 1, "line1\nline2" + new string('x', 500))]);

        description.ShouldContain("line1 line2");
        description.ShouldContain("...");
        description.Length.ShouldBeLessThan(4096);
    }

    [Fact]
    public void Report_stays_within_the_embed_description_limit_at_max_entries()
    {
        var occurrences = Enumerable.Range(0, 50)
            .Select(i => Occurrence($"Type{i}", 100 - i, new string('x', 1000)))
            .ToList();

        Describe(occurrences).Length.ShouldBeLessThanOrEqualTo(4096);
    }

    /// <summary>
    ///     I-12: 15 entries with long types and exception names reach about 4,700 characters. DSharpPlus throws above
    ///     4,096, and the report then fails every minute.
    /// </summary>
    [Fact]
    public void Report_with_long_types_stays_within_the_limit_and_says_how_many_were_left_out()
    {
        var occurrences = Enumerable.Range(0, WarningReportFormatter.MaxEntries)
            .Select(i => Occurrence($"UnhandledException in command handler number {i:D2} of the moderation feature", 100 - i,
                new string('x', 1000)) with { ExceptionType = "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException" })
            .ToList();

        var description = Describe(occurrences);

        description.Length.ShouldBeLessThanOrEqualTo(4096);
        description.ShouldContain("more warning types, in the attached file.");
    }

    [Fact]
    public void A_report_that_fits_has_no_file()
        => WarningReportFormatter.BuildMessage([Occurrence("A", 3), Occurrence("B", 4)], DateOnly.FromDateTime(_at.UtcDateTime))
            .Files.ShouldBeEmpty();

    /// <summary>Whatever the embed leaves out is in the attached file, in full: every type, whole samples, every guild.</summary>
    [Fact]
    public void A_report_that_does_not_fit_attaches_every_warning_in_full()
    {
        var occurrences = Enumerable.Range(0, WarningReportFormatter.MaxEntries + 3)
            .Select(i => Occurrence($"Type{i:D2}", 100 - i, $"sample {i} " + new string('x', 500)))
            .ToList();

        var message = WarningReportFormatter.BuildMessage(occurrences, new DateOnly(2026, 11, 8));

        var file = message.Files.ShouldHaveSingleItem();
        file.FileName.ShouldBe("warning-report-2026-11-08.txt");
        var text = new StreamReader(file.Stream).ReadToEnd();
        foreach (var i in Enumerable.Range(0, occurrences.Count))
            text.ShouldContain($"Type{i:D2}");
        text.ShouldContain("sample 0 " + new string('x', 500));
        text.ShouldContain("Guilds: 1");
        text.IndexOf("Type00", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("Type17", StringComparison.Ordinal));
    }
}
