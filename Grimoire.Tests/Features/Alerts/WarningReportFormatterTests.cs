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

        Describe(occurrences).ShouldContain("…and 3 more warning types.");
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
}
