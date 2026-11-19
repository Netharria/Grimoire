// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Grimoire.UnitTests.Database;

/// <summary>
///     Spec 2.3, section 6.2: <c>GrimoireSchema</c> converts main's id columns to bigint by hand, one statement per
///     table. Its list must be exactly the snowflake columns of the public tables that came from main, read from the
///     model, so an id column added to one of those tables can't be left numeric.
/// </summary>
public sealed class GrimoireSchemaMigrationTests
{
    /// <summary>Created after main, already with bigint (ModelRedesign, or GrimoireSchema itself).</summary>
    private static readonly string[] _createdAfterMain = ["SinReasonHistory", "CustomCommandUsages", "PardonReasonHistory"];

    private static readonly Type[] _snowflakes =
    [
        typeof(MessageId), typeof(UserId), typeof(ModeratorId), typeof(GuildId), typeof(ChannelId), typeof(RoleId),
        typeof(AttachmentId), typeof(ModerationActor)
    ];

    [Fact]
    public void TheConvertedColumns_AreExactlyMainsSnowflakeColumns()
    {
        using var merged = new GrimoireMigrationsDbContext(
            new DbContextOptionsBuilder<GrimoireMigrationsDbContext>().UseNpgsql("Host=unused")
                .EnableServiceProviderCaching(false).Options);
        var model = merged.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        var expected = model.Tables
            .Where(table => table.Schema is null && !_createdAfterMain.Contains(table.Name))
            .SelectMany(table => table.Columns
                .Where(column => column.PropertyMappings.Any(mapping => IsSnowflake(mapping.Property)))
                .Select(column => $"{ByMainsName(table.Name)}.{column.Name}"))
            .Order();

        var converted = GrimoireSchema.BigintColumns
            .SelectMany(entry => entry.Columns.Select(column => $"{entry.Table}.{column}"))
            .Order();

        converted.ShouldBe(expected);
    }

    /// <summary>No id in either schema is numeric any more: public since 2.3, Settings since 2.2 (P-9).</summary>
    [Fact]
    public void NoColumnInEitherSchema_IsNumeric()
    {
        using var merged = new GrimoireMigrationsDbContext(
            new DbContextOptionsBuilder<GrimoireMigrationsDbContext>().UseNpgsql("Host=unused")
                .EnableServiceProviderCaching(false).Options);
        var model = merged.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        model.Tables
            .SelectMany(table => table.Columns.Where(column => column.StoreType.StartsWith("numeric", StringComparison.Ordinal))
                .Select(column => $"{table.Schema}.{table.Name}.{column.Name}"))
            .ShouldBeEmpty();
    }

    private static bool IsSnowflake(IProperty property)
        => _snowflakes.Contains(Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);

    /// <summary>The migration converts before it renames (TD-9), so it names the table as main did.</summary>
    private static string ByMainsName(string table) => table == "ServerDisplayNameHistory" ? "NicknameHistory" : table;
}
