// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Data.Conventions;

namespace Grimoire;

/// <summary>
///     The <c>public</c> schema's model, built the same way for <see cref="GrimoireDbContext" /> and for the
///     migrations context, so the two can't drift apart (spec 2.0).
/// </summary>
public static class GrimoireModel
{
    public static void Configure(ModelBuilder modelBuilder)
        => modelBuilder.AddEntitySetsOf(typeof(GrimoireDbContext))
            .ApplyConfigurationsFromAssembly(typeof(GrimoireDbContext).Assembly)
            .NameTablesAfterEntitySetsOf(typeof(GrimoireDbContext))
            .HasPostgresExtension("fuzzystrmatch")
            // Every type in the model so far is a public-schema one: the migrations context adds Settings after this.
            .FixConstraintNames(_ => true);
}
