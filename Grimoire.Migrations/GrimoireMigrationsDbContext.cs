// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Data.Conventions;
using Grimoire.Settings;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Migrations;

/// <summary>
///     The one context that migrates the database (spec 2.0, C12): the <c>public</c> and <c>Settings</c> schemas as
///     one ordered chain, so a data copy always runs before the change that depends on it. Its model is the union of
///     the two runtime contexts' models, built by the same code. Never queried: the runtime contexts do that until
///     their features are ported, and they have no migrations of their own.
/// </summary>
public sealed class GrimoireMigrationsDbContext(DbContextOptions<GrimoireMigrationsDbContext> options)
    : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureStronglyTypedIds();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        GrimoireModel.Configure(modelBuilder);
        SettingsModel.Configure(modelBuilder);
    }
}
