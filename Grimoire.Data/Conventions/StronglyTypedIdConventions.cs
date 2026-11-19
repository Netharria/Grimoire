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
    /// <summary>
    ///     Every id is stored as <c>bigint</c> (P-9, spec 2.3). Snowflakes stay below 2^63 until about 2084, so the cast
    ///     keeps order and equality. A plain <c>ulong</c> id (a Settings item's) is stored the same way.
    /// </summary>
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
        builder.Properties<ulong>().HaveConversion<SnowflakeConverter>();
        return builder;
    }
}

internal sealed class MessageIdConverter()
    : ValueConverter<MessageId, long>(id => unchecked((long)id.Value), value => new MessageId(unchecked((ulong)value)));

internal sealed class UserIdConverter()
    : ValueConverter<UserId, long>(id => unchecked((long)id.Value), value => new UserId(unchecked((ulong)value)));

internal sealed class ModeratorIdConverter()
    : ValueConverter<ModeratorId, long>(id => unchecked((long)id.Value), value => new ModeratorId(unchecked((ulong)value)));

internal sealed class GuildIdConverter()
    : ValueConverter<GuildId, long>(id => unchecked((long)id.Value), value => new GuildId(unchecked((ulong)value)));

internal sealed class ChannelIdConverter()
    : ValueConverter<ChannelId, long>(id => unchecked((long)id.Value), value => new ChannelId(unchecked((ulong)value)));

internal sealed class RoleIdConverter()
    : ValueConverter<RoleId, long>(id => unchecked((long)id.Value), value => new RoleId(unchecked((ulong)value)));

internal sealed class SinIdConverter() : ValueConverter<SinId, long>(id => id.Value, value => new SinId(value));

internal sealed class AttachmentIdConverter()
    : ValueConverter<AttachmentId, long>(id => unchecked((long)id.Value), value => new AttachmentId(unchecked((ulong)value)));

internal sealed class SnowflakeConverter() : ValueConverter<ulong, long>(id => unchecked((long)id), value => unchecked((ulong)value));
