// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Data.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Grimoire.Settings;

/// <summary>
///     The <c>Settings</c> schema's model, built the same way for <see cref="SettingsDbContext" /> and for the
///     migrations context, so the two can't drift apart (spec 2.0).
/// </summary>
public static class SettingsModel
{
    public const string Schema = "Settings";

    /// <summary>
    ///     Builds the settings entities and puts each of their tables in <see cref="Schema" /> explicitly, since the
    ///     migrations context's default schema is <c>public</c>. Settings has no sequences or owned types, the other
    ///     things a default schema would place.
    /// </summary>
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.AddEntitySetsOf(typeof(SettingsDbContext))
            .ApplyConfigurationsFromAssembly(typeof(SettingsDbContext).Assembly)
            .NameTablesAfterEntitySetsOf(typeof(SettingsDbContext));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Cast<IConventionEntityType>().ToList())
            if (entityType.ClrType.Assembly == typeof(SettingsDbContext).Assembly && entityType.GetTableName() is not null)
                entityType.Builder.ToSchema(Schema);

        modelBuilder.FixConstraintNames(type => type.Assembly == typeof(SettingsDbContext).Assembly);
    }
}
