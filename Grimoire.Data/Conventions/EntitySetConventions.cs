// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Grimoire.Data.Conventions;

/// <summary>
///     Lets a context without a runtime context's <c>DbSet</c>s build the same model (spec 2.0). EF registers each
///     <c>DbSet</c>'s entity type and names its table after the property, at convention level, so an explicit
///     <c>ToTable</c> still wins. The migrations context has no <c>DbSet</c>s, so it does both here, for each runtime
///     context it covers.
/// </summary>
/// <remarks>Linked into Grimoire.Settings and Grimoire.Migrations, which don't reference each other.</remarks>
internal static class EntitySetConventions
{
    /// <summary>Registers the entity type of every <c>DbSet</c> property on <paramref name="contextType" />.</summary>
    public static ModelBuilder AddEntitySetsOf(this ModelBuilder modelBuilder, Type contextType)
    {
        foreach (var (_, entityType) in EntitySets(contextType))
            modelBuilder.Entity(entityType);
        return modelBuilder;
    }

    /// <summary>
    ///     Names each root entity type's table after its <c>DbSet</c> property, as EF's table-name-from-DbSet
    ///     convention does. A type mapped to a view keeps no table, as with the convention. Call after the
    ///     configurations.
    /// </summary>
    public static ModelBuilder NameTablesAfterEntitySetsOf(this ModelBuilder modelBuilder, Type contextType)
    {
        foreach (var (name, clrType) in EntitySets(contextType))
            if (modelBuilder.Model.FindEntityType(clrType) is IConventionEntityType { BaseType: null } entityType
                && entityType.GetViewName() is null)
                entityType.Builder.ToTable(name);

        return modelBuilder;
    }

    /// <summary>
    ///     Names every key, index and foreign key of the entity types <paramref name="include" /> picks explicitly,
    ///     with the name it has at this point: its configuration's, or else EF's default. EF makes these names unique across the whole model, ignoring schemas,
    ///     so in the migrations context a Settings table that shares a name with a legacy public table (Mutes, since
    ///     spec 2.5 dropped the Settings Rewards and SpamFilterOverrides) would get a "1" on one side that the
    ///     database doesn't have. An explicit
    ///     name is left alone; and on its own each runtime model has the same names either way. Call last.
    /// </summary>
    public static ModelBuilder FixConstraintNames(this ModelBuilder modelBuilder, Func<Type, bool> include)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(entityType => include(entityType.ClrType) && entityType.GetTableName() is not null)
                     .ToList())
        {
            // Each keeps the name it has now: a configuration's explicit name, or else EF's default.
            foreach (var key in entityType.GetDeclaredKeys())
                key.SetName(key.GetName()!);
            foreach (var index in entityType.GetDeclaredIndexes())
                index.SetDatabaseName(index.GetDatabaseName()!);
            foreach (var foreignKey in entityType.GetDeclaredForeignKeys())
                foreignKey.SetConstraintName(foreignKey.GetConstraintName()!);
        }

        return modelBuilder;
    }

    private static IEnumerable<(string Name, Type EntityType)> EntitySets(Type contextType)
        => contextType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType.IsGenericType
                               && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(property => (property.Name, property.PropertyType.GetGenericArguments()[0]));
}
