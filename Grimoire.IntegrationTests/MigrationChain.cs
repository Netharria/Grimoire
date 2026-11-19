// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Migrations;

namespace Grimoire.IntegrationTests;

/// <summary>
///     The one migration chain of both schemas (spec 2.0), for tests that build or step a database. The runtime
///     contexts have no migrations, so this is the only way a test database gets its schema.
/// </summary>
internal static class MigrationChain
{
    public static GrimoireMigrationsDbContext Context(string connectionString)
        => new(new DbContextOptionsBuilder<GrimoireMigrationsDbContext>().UseNpgsql(connectionString).Options);

    public static async Task ApplyAsync(string connectionString)
    {
        await using var context = Context(connectionString);
        await context.Database.MigrateAsync();
    }
}
