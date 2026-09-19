// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;

namespace Grimoire.Tests.Features.Alerts;

[Collection("Test collection")]
public sealed class AlertOccurrenceStoreTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset _t0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    private static PendingWarning Pending(string key = "key", int count = 1, DateTimeOffset? first = null,
        DateTimeOffset? last = null, params ulong[] guilds)
        => new(key, "SlowCommand (level)", null, "sample", count, first ?? _t0, last ?? first ?? _t0,
            guilds.Select(g => new GuildId(g)).ToHashSet());

    private async Task UpsertAsync(params PendingWarning[] warnings)
    {
        await using var db = factory.CreateDbContext();
        await AlertOccurrenceStore.UpsertAsync(db, warnings, TestContext.Current.CancellationToken);
    }

    private async Task<List<AlertOccurrence>> AllAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.AlertOccurrences.OrderBy(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Upsert_creates_a_row_for_a_new_key()
    {
        await this.UpsertAsync(Pending(count: 3, guilds: 7));

        var row = (await this.AllAsync()).ShouldHaveSingleItem();
        row.AlertKey.ShouldBe("key");
        row.AlertType.ShouldBe("SlowCommand (level)");
        row.Count.ShouldBe(3);
        row.GuildIds.ShouldBe([new GuildId(7)]);
        row.ReportedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Upsert_merges_into_the_open_row_for_the_same_key()
    {
        await this.UpsertAsync(Pending(count: 2, first: _t0, last: _t0, guilds: 1));
        await this.UpsertAsync(Pending(count: 3, first: _t0.AddHours(1), last: _t0.AddHours(2), guilds: [1, 2]));

        var row = (await this.AllAsync()).ShouldHaveSingleItem();
        row.Count.ShouldBe(5);
        row.FirstSeen.ShouldBe(_t0);
        row.LastSeen.ShouldBe(_t0.AddHours(2));
        row.GuildIds.ShouldBe([new GuildId(1), new GuildId(2)], true);
    }

    [Fact]
    public async Task Upsert_keeps_different_keys_in_separate_rows()
    {
        await this.UpsertAsync(Pending("a"), Pending("b"));

        (await this.AllAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Upsert_starts_a_new_row_once_the_previous_one_was_reported()
    {
        await this.UpsertAsync(Pending(count: 2));
        await using (var db = factory.CreateDbContext())
        {
            var id = (await this.AllAsync()).Single().Id;
            await AlertOccurrenceStore.MarkReportedAsync(db, [id], _t0.AddDays(1),
                TestContext.Current.CancellationToken);
        }

        await this.UpsertAsync(Pending(count: 1));

        var rows = await this.AllAsync();
        rows.Count.ShouldBe(2);
        rows.Count(x => x.ReportedAt is null).ShouldBe(1);
        rows.Single(x => x.ReportedAt is null).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Open_rows_are_unique_per_key_at_the_database_level()
    {
        await this.UpsertAsync(Pending());
        await using var db = factory.CreateDbContext();
        db.AlertOccurrences.Add(new AlertOccurrence
        {
            AlertKey = "key", AlertType = "x", Count = 1, FirstSeen = _t0, LastSeen = _t0, SampleMessage = "s",
            GuildIds = []
        });

        await Should.ThrowAsync<Exception>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOpen_returns_only_unreported_rows_ordered_by_count()
    {
        await this.UpsertAsync(Pending("low", 1), Pending("high", 9), Pending("done", 50));
        await using var db = factory.CreateDbContext();
        var done = (await this.AllAsync()).Single(x => x.AlertKey == "done").Id;
        await AlertOccurrenceStore.MarkReportedAsync(db, [done], _t0, TestContext.Current.CancellationToken);

        var open = await AlertOccurrenceStore.GetOpenAsync(db, TestContext.Current.CancellationToken);

        open.Select(x => x.AlertKey).ShouldBe(["high", "low"]);
    }

    [Fact]
    public async Task MarkReported_only_touches_the_given_open_rows()
    {
        await this.UpsertAsync(Pending("a"), Pending("b"));
        var rows = await this.AllAsync();
        await using var db = factory.CreateDbContext();

        var updated = await AlertOccurrenceStore.MarkReportedAsync(db, [rows[0].Id], _t0.AddDays(1),
            TestContext.Current.CancellationToken);

        updated.ShouldBe(1);
        var after = await this.AllAsync();
        after.Single(x => x.AlertKey == "a").ReportedAt.ShouldBe(_t0.AddDays(1));
        after.Single(x => x.AlertKey == "b").ReportedAt.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteOlderThan_removes_rows_last_seen_before_the_cutoff()
    {
        await this.UpsertAsync(
            Pending("old", first: _t0, last: _t0),
            Pending("recent", first: _t0.AddDays(40), last: _t0.AddDays(40)));
        await using var db = factory.CreateDbContext();

        var deleted = await AlertOccurrenceStore.DeleteOlderThanAsync(db, _t0.AddDays(30),
            TestContext.Current.CancellationToken);

        deleted.ShouldBe(1);
        (await this.AllAsync()).ShouldHaveSingleItem().AlertKey.ShouldBe("recent");
    }
}
