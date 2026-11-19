// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

/// <summary>Guild settings as versioned documents (TD-6, C13; spec 2.2, section 6.1).</summary>
[ExcludeFromCodeCoverage]
internal sealed class GuildSettingRowConfiguration : IEntityTypeConfiguration<GuildSettingRow>
{
    public void Configure(EntityTypeBuilder<GuildSettingRow> builder)
    {
        // Serves both reads: the latest version of one document, and the latest of each of a guild's documents.
        builder.HasKey(row => new { row.GuildId, row.Document, row.Version });
        builder.Property(row => row.GuildId).ValueGeneratedNever();
        builder.Property(row => row.Document).HasConversion<string>().HasMaxLength(32);
        builder.Property(row => row.Version).ValueGeneratedNever();
        builder.Property(row => row.Value).HasColumnType("jsonb");
        builder.Property(row => row.SetBy);

        // No check on Document's values: a new kind of document then needs no migration.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_GuildSettings_Version", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_GuildSettings_Value", "jsonb_typeof(\"Value\") = 'object'");
        });
    }
}

/// <summary>The columns and rules the three state tables share (TD-1, TD-2).</summary>
internal static class ItemRowConfiguration
{
    public const string InForce = "\"State\" IN ('Pending', 'Active')";

    /// <summary>Which fields each state requires, as <see cref="ItemState" /> holds them.</summary>
    private const string StateRule = """
        ("State" = 'Pending' AND "ActivatedAt" IS NULL AND "EndedAt" IS NULL AND "RemovedBy" IS NULL AND "RemoveReason" IS NULL)
        OR ("State" = 'Active' AND "ActivatedAt" IS NOT NULL AND "EndedAt" IS NULL AND "RemovedBy" IS NULL AND "RemoveReason" IS NULL)
        OR ("State" = 'Expired' AND "EndedAt" IS NOT NULL AND "RemovedBy" IS NULL AND "RemoveReason" IS NULL)
        OR ("State" = 'Removed' AND "EndedAt" IS NOT NULL AND "RemovedBy" IS NOT NULL)
        """;

    public static void ConfigureItem<T>(this EntityTypeBuilder<T> builder, string table) where T : ItemRow
    {
        builder.Property(row => row.GuildId);
        builder.Property(row => row.ModeratorId);
        builder.Property(row => row.State).HasMaxLength(16);
        builder.Property(row => row.RemovedBy);
        builder.Property(row => row.RemoveReason)
            .HasConversion(
                reason => reason == null ? null : reason.Value.Value,
                value => value == null ? null : ModerationReason.FromDatabase(value))
            .HasMaxLength(1000);

        // Q31, widened for TD-4: the expiry poll reads only what is in force, and a Pending item can expire too.
        builder.HasIndex(row => row.EndTime).HasDatabaseName($"IX_{table}_EndTime").HasFilter(InForce);

        builder.ToTable(t => t.HasCheckConstraint($"CK_{table}_State", StateRule));
    }

    public static PropertyBuilder<ModerationReason?> ConfigureReason<T>(this EntityTypeBuilder<T> builder)
        where T : class
        => builder.Property<ModerationReason?>("Reason")
            .HasConversion(
                reason => reason == null ? null : reason.Value.Value,
                value => value == null ? null : ModerationReason.FromDatabase(value))
            .HasMaxLength(1000);
}

[ExcludeFromCodeCoverage]
internal sealed class MuteRowConfiguration : IEntityTypeConfiguration<MuteRow>
{
    public void Configure(EntityTypeBuilder<MuteRow> builder)
    {
        // Each mute comes from exactly one sin, so the sin is its key.
        builder.HasKey(row => row.SinId);
        builder.Property(row => row.SinId).ValueGeneratedNever();
        builder.Property(row => row.UserId);
        builder.Property(row => row.MuteRoleId);
        builder.ConfigureItem("Mutes");

        // A member has at most one mute in force.
        builder.HasIndex(row => new { row.GuildId, row.UserId })
            .HasDatabaseName("IX_Mutes_InForce").IsUnique().HasFilter(ItemRowConfiguration.InForce);
    }
}

[ExcludeFromCodeCoverage]
internal sealed class ChannelLockRowConfiguration : IEntityTypeConfiguration<ChannelLockRow>
{
    public void Configure(EntityTypeBuilder<ChannelLockRow> builder)
    {
        builder.HasKey(row => new { row.GuildId, row.ChannelId, row.SetAt });
        builder.Property(row => row.ChannelId).ValueGeneratedNever();
        builder.ConfigureReason();
        builder.Property(row => row.PreviouslyAllowed)
            .HasConversion(value => value.Permissions, value => new PreviouslyAllowedPermissions(value));
        builder.Property(row => row.PreviouslyDenied)
            .HasConversion(value => value.Permissions, value => new PreviouslyDeniedPermissions(value));
        builder.ConfigureItem("ChannelLocks");

        // At most one lock in force per channel. The GuildId prefix also serves IsChannelLocked's per-guild set.
        builder.HasIndex(row => new { row.GuildId, row.ChannelId })
            .HasDatabaseName("IX_ChannelLocks_InForce").IsUnique().HasFilter(ItemRowConfiguration.InForce);
    }
}

[ExcludeFromCodeCoverage]
internal sealed class ThreadLockRowConfiguration : IEntityTypeConfiguration<ThreadLockRow>
{
    public void Configure(EntityTypeBuilder<ThreadLockRow> builder)
    {
        builder.HasKey(row => new { row.GuildId, row.ChannelId, row.SetAt });
        builder.Property(row => row.ChannelId).ValueGeneratedNever();
        builder.ConfigureReason();
        builder.ConfigureItem("ThreadLocks");

        builder.HasIndex(row => new { row.GuildId, row.ChannelId })
            .HasDatabaseName("IX_ThreadLocks_InForce").IsUnique().HasFilter(ItemRowConfiguration.InForce);
    }
}
