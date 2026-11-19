// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Migrations;
using Grimoire.Settings;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Grimoire.UnitTests.Database;

/// <summary>
///     Spec 2.0: one migrations context covers both schemas, and the two runtime contexts query them until Phase 5.
///     The three must map the same tables, or a query would run against a schema the migrations never built. EF builds
///     each model without a connection.
/// </summary>
public sealed class MigrationModelTests
{
    // Never opened: the models are built from the configuration alone.
    private const string NoDatabase = "Host=unused";

    [Fact]
    public void TheMainModel_IsTheMergedModel_WithoutTheSettingsSchema()
    {
        using var main = new GrimoireDbContext(Options<GrimoireDbContext>());

        var missing = DifferencesToMerged(main);

        missing.ShouldNotBeEmpty();
        missing.Where(operation => !(IsCreate(operation) && SchemaOf(operation) == SettingsModel.Schema))
            .Select(Describe).ShouldBeEmpty();
    }

    [Fact]
    public void TheSettingsModel_IsTheMergedModel_WithoutThePublicSchema()
    {
        using var settings = new SettingsDbContext(Options<SettingsDbContext>());

        var missing = DifferencesToMerged(settings);

        missing.ShouldNotBeEmpty();
        // The public schema's tables, and its fuzzystrmatch extension, which lives on the database.
        missing.Where(operation => !(operation is AlterDatabaseOperation
                                     || (IsCreate(operation) && SchemaOf(operation) == null)))
            .Select(Describe).ShouldBeEmpty();
    }

    [Fact]
    public void TheRuntimeContexts_HaveNoMigrations()
    {
        using var main = new GrimoireDbContext(Options<GrimoireDbContext>());
        using var settings = new SettingsDbContext(Options<SettingsDbContext>());

        main.Database.GetMigrations().ShouldBeEmpty();
        settings.Database.GetMigrations().ShouldBeEmpty();
    }

    private static IReadOnlyList<MigrationOperation> DifferencesToMerged(DbContext runtime)
    {
        using var merged = new GrimoireMigrationsDbContext(Options<GrimoireMigrationsDbContext>());
        return merged.GetService<IMigrationsModelDiffer>().GetDifferences(
            runtime.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            merged.GetService<IDesignTimeModel>().Model.GetRelationalModel());
    }

    private static bool IsCreate(MigrationOperation operation)
        => operation is EnsureSchemaOperation or CreateTableOperation or CreateIndexOperation
            or AddForeignKeyOperation;

    private static string? SchemaOf(MigrationOperation operation)
        => operation switch
        {
            EnsureSchemaOperation schema => schema.Name,
            CreateTableOperation table => table.Schema,
            CreateIndexOperation index => index.Schema,
            AddForeignKeyOperation foreignKey => foreignKey.Schema,
            _ => $"<{operation.GetType().Name}>",
        };

    private static string Describe(MigrationOperation operation)
        => operation switch
        {
            CreateTableOperation table => $"CreateTable {table.Schema}.{table.Name}",
            CreateIndexOperation index => $"CreateIndex {index.Schema}.{index.Table}.{index.Name}",
            AddForeignKeyOperation foreignKey => $"AddForeignKey {foreignKey.Schema}.{foreignKey.Table}.{foreignKey.Name}",
            DropPrimaryKeyOperation key => $"DropPrimaryKey {key.Schema}.{key.Table}.{key.Name}",
            AddPrimaryKeyOperation key => $"AddPrimaryKey {key.Schema}.{key.Table}.{key.Name}",
            RenameIndexOperation index => $"RenameIndex {index.Schema}.{index.Table}.{index.Name} to {index.NewName}",
            ColumnOperation column => $"{operation.GetType().Name} {column.Schema}.{column.Table}.{column.Name}",
            _ => $"{operation.GetType().Name} {SchemaOf(operation)}",
        };

    private static DbContextOptions<TContext> Options<TContext>() where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>().UseNpgsql(NoDatabase).Options;
}
