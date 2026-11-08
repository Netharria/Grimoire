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
}

internal sealed class MessageIdConverter() : ValueConverter<MessageId, ulong>(id => id.Value, value => new MessageId(value));

internal sealed class UserIdConverter() : ValueConverter<UserId, ulong>(id => id.Value, value => new UserId(value));

internal sealed class ModeratorIdConverter() : ValueConverter<ModeratorId, ulong>(id => id.Value, value => new ModeratorId(value));

internal sealed class GuildIdConverter() : ValueConverter<GuildId, ulong>(id => id.Value, value => new GuildId(value));

internal sealed class ChannelIdConverter() : ValueConverter<ChannelId, ulong>(id => id.Value, value => new ChannelId(value));

internal sealed class RoleIdConverter() : ValueConverter<RoleId, ulong>(id => id.Value, value => new RoleId(value));

internal sealed class SinIdConverter() : ValueConverter<SinId, long>(id => id.Value, value => new SinId(value));

internal sealed class AttachmentIdConverter() : ValueConverter<AttachmentId, ulong>(id => id.Value, value => new AttachmentId(value));
