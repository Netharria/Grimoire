// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Npgsql;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Indexes in the migrated schema, both the main and the settings one. Every index is written on each insert, so
///     one that no query needs only slows writes.
/// </summary>
[Collection("Test collection")]
public sealed class SchemaIndexTests(GrimoireCoreFactory database)
{
    /// <summary>
    ///     Plain indexes whose leading columns, in order, are the primary key's, or the other way round. Postgres
    ///     serves those queries from the key, scanning it backwards for a descending column. Unique and partial indexes
    ///     don't count: they constrain or cover something the key doesn't.
    /// </summary>
    private const string IndexesRepeatingThePrimaryKey = """
        SELECT n.nspname || '.' || i.relname
        FROM pg_index x
        JOIN pg_class i ON i.oid = x.indexrelid
        JOIN pg_namespace n ON n.oid = i.relnamespace
        JOIN pg_constraint c ON c.conrelid = x.indrelid AND c.contype = 'p'
        CROSS JOIN LATERAL (SELECT least(x.indnkeyatts, cardinality(c.conkey)) AS k) m
        WHERE NOT x.indisprimary
          AND NOT x.indisunique
          AND x.indpred IS NULL
          AND n.nspname NOT IN ('pg_catalog', 'information_schema')
          AND ARRAY(SELECT unnest((x.indkey::int2[])[0:m.k - 1])) = ARRAY(SELECT unnest(c.conkey[1:m.k]))
        """;

    private readonly GrimoireCoreFactory _database = database;

    [Fact]
    public async Task NoIndexRepeatsThePrimaryKey()
        => (await this.QueryNamesAsync(IndexesRepeatingThePrimaryKey)).ShouldBeEmpty();

    /// <summary>
    ///     The indexes the review found no query for. Most repeat the primary key, which the test above catches, but
    ///     <c>IX_XpHistory_GuildId_Xp</c> and <c>IX_CustomCommandUsages_GuildId_Name_UserId</c> don't, so they are named.
    /// </summary>
    [Fact]
    public async Task TheRedundantIndexesAreDropped()
        => (await this.QueryNamesAsync("SELECT indexname FROM pg_indexes WHERE indexname = ANY(@names)",
                new NpgsqlParameter("names", (string[])
                [
                    "IX_XpHistory_GuildId_Xp", "IX_Sin_Id_GuildId", "IX_Pardons_SinId_SetAt",
                    "IX_SinReasonHistory_SinId_SetAt", "IX_CustomCommandUsages_GuildId_Name_UserId",
                    "IX_ChannelLocks_ChannelId_GuildId_SetAt", "IX_ThreadLocks_ChannelId_GuildId_SetAt",
                    "IX_Mutes_UserId_GuildId_SetAt", "IX_Rewards_GuildId_RoleId_SetAt",
                    "IX_XpTrackedItems_GuildId_Id_SetAt", "IX_MessageLogChannelOverrides_ChannelId_GuildId_SetAt",
                    "IX_SpamFilterOverrides_ChannelId_GuildId_SetAt"
                ])))
            .ShouldBeEmpty();

    private async Task<List<string>> QueryNamesAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(this._database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<string> names = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            names.Add(reader.GetString(0));
        return names;
    }
}
