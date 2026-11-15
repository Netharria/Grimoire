// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;
using LanguageExt;
using Npgsql;
using NpgsqlTypes;
using static LanguageExt.Prelude;

namespace Grimoire.Data.Dapper;

/// <summary>
///     Dapper's type handlers for the domain's value types (spec 1.3, section 3.7). They live here, not in the bot,
///     because the domain's <c>FromDatabase</c> factories are internal to it and visible to this project.
/// </summary>
/// <remarks>
///     Not covered by handlers, on purpose: enums (Dapper sends their number and ignores handlers for them, so SQL
///     takes <c>ToString()</c>), <see cref="ModerationActor" /> (Dapper never hands a <c>NULL</c> to a class's
///     handler, so it's read as <c>long?</c> through <see cref="DatabaseColumns" />), and arrays (handlers don't
///     apply to elements, so <c>= ANY(@ids)</c> takes <see cref="DatabaseColumns" />' <c>long[]</c>).
/// </remarks>
public static class DapperTypeHandlers
{
    private static int _registered;

    /// <summary>Registers every handler once. Dapper's handler table is process-wide, so later calls do nothing.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        // Ids are sent as bigint even while the columns are numeric(20,0): Postgres casts the parameter, so the
        // index is still used, and P-9's change to bigint needs no change here. Snowflakes stay below 2^63 until
        // about 2084, so the cast is exact.
        AddWithOption(new SnowflakeHandler<GuildId>(id => id.Value, value => new GuildId(value)));
        AddWithOption(new SnowflakeHandler<UserId>(id => id.Value, value => new UserId(value)));
        AddWithOption(new SnowflakeHandler<ChannelId>(id => id.Value, value => new ChannelId(value)));
        AddWithOption(new SnowflakeHandler<MessageId>(id => id.Value, value => new MessageId(value)));
        AddWithOption(new SnowflakeHandler<RoleId>(id => id.Value, value => new RoleId(value)));
        AddWithOption(new SnowflakeHandler<ModeratorId>(id => id.Value, value => new ModeratorId(value)));
        AddWithOption(new SnowflakeHandler<AttachmentId>(id => id.Value, value => new AttachmentId(value)));
        AddWithOption(new SnowflakeHandler<EmojiId>(id => id.Value, value => new EmojiId(value)));

        AddWithOption(new Int64Handler<SinId>(id => id.Value, value => new SinId(value)));
        AddWithOption(new Int64Handler<PositiveXpAmount>(amount => amount.Value, PositiveXpAmount.FromDatabase));
        AddWithOption(new Int64Handler<NegativeXpAmount>(amount => amount.Value, NegativeXpAmount.FromDatabase));

        AddWithOption(new TextHandler<Nickname>(value => value.Value, Nickname.FromDatabase));
        AddWithOption(new TextHandler<Username>(value => value.Value, Username.Create));
        AddWithOption(new TextHandler<ModerationReason>(value => value.Value, ModerationReason.FromDatabase));
        AddWithOption(new TextHandler<MessageContent>(value => value.Content, MessageContent.FromDatabase));
        AddWithOption(new TextHandler<CustomCommandName>(value => value.Value, CustomCommandName.FromDatabase));
        AddWithOption(new TextHandler<CustomCommandContent>(value => value.Value, CustomCommandContent.FromDatabase));
        AddWithOption(new TextHandler<CustomCommandEmbedColor>(value => value.Value,
            CustomCommandEmbedColor.FromDatabase));
        AddWithOption(new TextHandler<AttachmentUrl>(value => value.Value, AttachmentUrl.Create));
        AddWithOption(new TextHandler<AvatarFileName>(value => value.Value, AvatarFileName.Create));
        AddWithOption(new TextHandler<PluralKitSystemId>(value => value.Value, PluralKitSystemId.FromDatabase));
        AddWithOption(new TextHandler<PluralKitMemberId>(value => value.Value, PluralKitMemberId.FromDatabase));
        AddWithOption(new TextHandler<InviteCode>(value => value.Value, InviteCode.Create));
        AddWithOption(new TextHandler<InviteUrl>(value => value.Value, InviteUrl.Create));
        AddWithOption(new TextHandler<ReactionEmoji>(value => value.ToString(), ReactionEmoji.ParseFromDatabase));

        AddWithOption(new DateTimeOffsetHandler());
    }

    /// <summary>
    ///     Registers the handler for one <c>jsonb</c> document type (TD-6). Unknown fields are ignored and missing
    ///     ones take the record's defaults, which is System.Text.Json's behaviour with <paramref name="options" />'
    ///     defaults. A document that doesn't parse is a defect.
    /// </summary>
    public static void RegisterDocument<T>(JsonSerializerOptions options) =>
        SqlMapper.AddTypeHandler(new JsonbHandler<T>(options));

    private static void AddWithOption<T>(ValueHandler<T> handler) where T : notnull
    {
        SqlMapper.AddTypeHandler(handler);
        SqlMapper.AddTypeHandler(new OptionHandler<T>(handler));
    }

    internal static ulong ReadSnowflake(object value) => value switch
    {
        long signed => unchecked((ulong)signed),
        _ => Convert.ToUInt64(value, CultureInfo.InvariantCulture)
    };

    /// <summary>A handler that knows its column's type, so <see cref="OptionHandler{T}" /> can type a NULL.</summary>
    internal abstract class ValueHandler<T> : SqlMapper.TypeHandler<T>
    {
        public abstract DbType DbType { get; }
    }

    internal sealed class SnowflakeHandler<T>(Func<T, ulong> unwrap, Func<ulong, T> wrap) : ValueHandler<T>
    {
        public override DbType DbType => DbType.Int64;

        public override void SetValue(IDbDataParameter parameter, T? value)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = unchecked((long)unwrap(value!));
        }

        public override T Parse(object value) => wrap(ReadSnowflake(value));
    }

    internal sealed class Int64Handler<T>(Func<T, long> unwrap, Func<long, T> wrap) : ValueHandler<T>
    {
        public override DbType DbType => DbType.Int64;

        public override void SetValue(IDbDataParameter parameter, T? value)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = unwrap(value!);
        }

        public override T Parse(object value) => wrap(Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }

    internal sealed class TextHandler<T>(Func<T, string> unwrap, Func<string, T> wrap) : ValueHandler<T>
    {
        public override DbType DbType => DbType.String;

        public override void SetValue(IDbDataParameter parameter, T? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = unwrap(value!);
        }

        public override T Parse(object value) => wrap((string)value);
    }

    /// <summary>Npgsql refuses a <c>timestamptz</c> with an offset other than zero, and reads one as a UTC <c>DateTime</c>.</summary>
    internal sealed class DateTimeOffsetHandler : ValueHandler<DateTimeOffset>
    {
        public override DbType DbType => DbType.DateTimeOffset;

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.DateTimeOffset;
            parameter.Value = value;
        }

        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new InvalidCastException($"Cannot read {value.GetType().Name} as a DateTimeOffset.")
        };
    }

    /// <summary>
    ///     <c>None</c> is <c>NULL</c>. Dapper passes a <c>NULL</c> to <see cref="Parse" /> when the option is the whole
    ///     row, and leaves the member at its default (which is <c>None</c>) when it's a column of a row record.
    /// </summary>
    internal sealed class OptionHandler<T>(ValueHandler<T> inner) : SqlMapper.TypeHandler<Option<T>>
    {
        public override void SetValue(IDbDataParameter parameter, Option<T> value)
        {
            if (value.Case is T some)
            {
                inner.SetValue(parameter, some);
                return;
            }

            // Still give the parameter the inner type, so Postgres doesn't have to guess the type of a NULL.
            parameter.DbType = inner.DbType;
            parameter.Value = DBNull.Value;
        }

        public override Option<T> Parse(object value) =>
            value is null or DBNull ? None : Some(inner.Parse(value)!);
    }

    internal sealed class JsonbHandler<T>(JsonSerializerOptions options) : SqlMapper.TypeHandler<Jsonb<T>>
    {
        public override void SetValue(IDbDataParameter parameter, Jsonb<T> value)
        {
            if (parameter is NpgsqlParameter npgsql)
                npgsql.NpgsqlDbType = NpgsqlDbType.Jsonb;
            parameter.Value = JsonSerializer.Serialize(value.Value, options);
        }

        public override Jsonb<T> Parse(object value) =>
            new(JsonSerializer.Deserialize<T>((string)value, options)
                ?? throw new JsonException($"A stored {typeof(T).Name} document is null."));
    }
}

/// <summary>A value stored as a <c>jsonb</c> document. The wrapper is what Dapper's handler binds to.</summary>
public readonly record struct Jsonb<T>(T Value);

/// <summary>Column values that handlers can't cover (see <see cref="DapperTypeHandlers" />' remarks).</summary>
public static class DatabaseColumns
{
    /// <summary>Reads an actor column: <c>NULL</c> is the system, anything else a moderator.</summary>
    public static ModerationActor Actor(long? value) =>
        value is { } id
            ? new ModerationActor.Moderator(new ModeratorId(unchecked((ulong)id)))
            : new ModerationActor.System();

    /// <summary>The value an actor column stores.</summary>
    public static long? ActorColumn(ModerationActor actor) => actor switch
    {
        ModerationActor.Moderator moderator => unchecked((long)moderator.Id.Value),
        _ => null
    };

    /// <summary>An id array for <c>= ANY(@ids)</c>.</summary>
    public static long[] Ids(IEnumerable<GuildId> ids) => ids.Select(id => unchecked((long)id.Value)).ToArray();

    /// <inheritdoc cref="Ids(IEnumerable{GuildId})" />
    public static long[] Ids(IEnumerable<UserId> ids) => ids.Select(id => unchecked((long)id.Value)).ToArray();

    /// <inheritdoc cref="Ids(IEnumerable{GuildId})" />
    public static long[] Ids(IEnumerable<ChannelId> ids) => ids.Select(id => unchecked((long)id.Value)).ToArray();

    /// <inheritdoc cref="Ids(IEnumerable{GuildId})" />
    public static long[] Ids(IEnumerable<MessageId> ids) => ids.Select(id => unchecked((long)id.Value)).ToArray();

    /// <inheritdoc cref="Ids(IEnumerable{GuildId})" />
    public static long[] Ids(IEnumerable<RoleId> ids) => ids.Select(id => unchecked((long)id.Value)).ToArray();

    /// <inheritdoc cref="Ids(IEnumerable{GuildId})" />
    public static long[] Ids(IEnumerable<SinId> ids) => ids.Select(id => id.Value).ToArray();
}
