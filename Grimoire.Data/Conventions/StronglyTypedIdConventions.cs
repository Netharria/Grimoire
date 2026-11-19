// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Grimoire.Data.Conventions;

/// <summary>
///     Maps every strongly-typed id to its underlying number, once, instead of a <c>HasConversion</c> on each property.
///     Nullable id properties get the same conversion. Compiled into both <c>Grimoire.Data</c> and
///     <c>Grimoire.Settings</c>, which share no project, as a linked file.
/// </summary>
internal static class StronglyTypedIdConventions
{
    public static ModelConfigurationBuilder ConfigureStronglyTypedIds(this ModelConfigurationBuilder builder)
    {
        builder.Properties<MessageId>().HaveConversion<MessageIdConverter>();
        builder.Properties<UserId>().HaveConversion<UserIdConverter>();
        builder.Properties<ModeratorId>().HaveConversion<ModeratorIdConverter>();
        builder.Properties<GuildId>().HaveConversion<GuildIdConverter>();
        builder.Properties<ChannelId>().HaveConversion<ChannelIdConverter>();
        builder.Properties<RoleId>().HaveConversion<RoleIdConverter>();
        builder.Properties<SinId>().HaveConversion<SinIdConverter>();
        builder.Properties<AttachmentId>().HaveConversion<AttachmentIdConverter>();
        return builder;
    }

    private static readonly Dictionary<Type, ValueConverter> _bigintConverters = new()
    {
        [typeof(MessageId)] = new ValueConverter<MessageId, long>(id => (long)id.Value, value => new MessageId((ulong)value)),
        [typeof(UserId)] = new ValueConverter<UserId, long>(id => (long)id.Value, value => new UserId((ulong)value)),
        [typeof(ModeratorId)] = new ValueConverter<ModeratorId, long>(id => (long)id.Value, value => new ModeratorId((ulong)value)),
        [typeof(GuildId)] = new ValueConverter<GuildId, long>(id => (long)id.Value, value => new GuildId((ulong)value)),
        [typeof(ChannelId)] = new ValueConverter<ChannelId, long>(id => (long)id.Value, value => new ChannelId((ulong)value)),
        [typeof(RoleId)] = new ValueConverter<RoleId, long>(id => (long)id.Value, value => new RoleId((ulong)value)),
        [typeof(AttachmentId)] = new ValueConverter<AttachmentId, long>(id => (long)id.Value, value => new AttachmentId((ulong)value)),
        [typeof(ulong)] = new CastingConverter<ulong, long>()
    };

    /// <summary>
    ///     P-9 for one schema at a time: stores the snowflake ids of the entity types <paramref name="include" /> picks
    ///     as <c>bigint</c>, overriding the convention's <c>numeric(20,0)</c>. Spec 2.2 uses it for Settings; 2.3 moves
    ///     <c>public</c> to <c>bigint</c> too, makes these the convention's converters and deletes this. Snowflakes stay
    ///     below 2^63 until about 2084, so the cast keeps order and equality. Call after the configurations.
    /// </summary>
    public static ModelBuilder UseBigintIds(this ModelBuilder modelBuilder, Func<Type, bool> include)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => include(type.ClrType)).ToList())
            foreach (var property in entityType.GetDeclaredProperties())
                if (_bigintConverters.TryGetValue(Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType,
                        out var converter))
                    property.SetValueConverter(converter);

        return modelBuilder;
    }
}

internal sealed class MessageIdConverter() : ValueConverter<MessageId, ulong>(id => id.Value, value => new MessageId(value));

internal sealed class UserIdConverter() : ValueConverter<UserId, ulong>(id => id.Value, value => new UserId(value));

internal sealed class ModeratorIdConverter() : ValueConverter<ModeratorId, ulong>(id => id.Value, value => new ModeratorId(value));

internal sealed class GuildIdConverter() : ValueConverter<GuildId, ulong>(id => id.Value, value => new GuildId(value));

internal sealed class ChannelIdConverter() : ValueConverter<ChannelId, ulong>(id => id.Value, value => new ChannelId(value));

internal sealed class RoleIdConverter() : ValueConverter<RoleId, ulong>(id => id.Value, value => new RoleId(value));

internal sealed class SinIdConverter() : ValueConverter<SinId, long>(id => id.Value, value => new SinId(value));

internal sealed class AttachmentIdConverter() : ValueConverter<AttachmentId, ulong>(id => id.Value, value => new AttachmentId(value));
