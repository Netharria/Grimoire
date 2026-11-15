// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data;
using System.Reflection;
using System.Text.Json;
using Dapper;
using Grimoire.Data.Dapper;
using LanguageExt;
using Npgsql;
using NpgsqlTypes;

namespace Grimoire.UnitTests.Functional.Data;

public sealed class DapperTypeHandlerTests
{
    private const ulong Snowflake = 1_290_123_456_789_012_345;

    static DapperTypeHandlerTests() => DapperTypeHandlers.Register();

    /// <summary>Value types without a <c>FromDatabase</c> factory, read through <c>Create</c>.</summary>
    private static readonly Type[] CreateOnly =
    [
        typeof(Username), typeof(AttachmentUrl), typeof(AvatarFileName), typeof(InviteCode), typeof(InviteUrl)
    ];

    public static TheoryData<Type> StoredTypes()
    {
        var domain = typeof(GuildId).Assembly.GetTypes();
        var ids = domain.Where(type => type is { IsValueType: true, Namespace: "Grimoire.Domain" }
                                       && type.Name.EndsWith("Id", StringComparison.Ordinal)
                                       && type.GetProperty("Value")?.PropertyType is { } value
                                       && (value == typeof(ulong) || value == typeof(long)));
        var factories = domain.Where(type => type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(method => method.Name is "FromDatabase" or "ParseFromDatabase"));
        // The settings values (XpGainAmount and the rest) live in Grimoire.Settings and go away with TD-6 (2.2).
        return new TheoryData<Type>(ids.Concat(factories).Concat(CreateOnly).Distinct());
    }

    [Theory]
    [MemberData(nameof(StoredTypes))]
    public void EveryStoredDomainType_HasAHandler_AndOneForItsOption(Type type)
    {
        SqlMapper.HasTypeHandler(type).ShouldBeTrue();
        SqlMapper.HasTypeHandler(typeof(Option<>).MakeGenericType(type)).ShouldBeTrue();
    }

    [Fact]
    public void TheCoverageList_FindsTheIdsAndTheFactoryTypes()
    {
        var types = StoredTypes().Select(row => row.Data).ToList();

        types.ShouldContain(typeof(GuildId));
        types.ShouldContain(typeof(SinId));
        types.ShouldContain(typeof(ModerationReason));
        types.ShouldContain(typeof(ReactionEmoji));
    }

    [Fact]
    public void AnId_IsSentAsBigint()
    {
        var parameter = new NpgsqlParameter();

        new DapperTypeHandlers.SnowflakeHandler<GuildId>(id => id.Value, value => new GuildId(value))
            .SetValue(parameter, new GuildId(Snowflake));

        parameter.DbType.ShouldBe(DbType.Int64);
        parameter.Value.ShouldBe((long)Snowflake);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(Snowflake)]
    [InlineData((ulong)long.MaxValue)]
    public void AnId_IsReadFromNumericOrBigint(ulong value)
    {
        DapperTypeHandlers.ReadSnowflake((decimal)value).ShouldBe(value);
        DapperTypeHandlers.ReadSnowflake((long)value).ShouldBe(value);
    }

    [Fact]
    public void None_IsATypedNull()
    {
        var inner = new DapperTypeHandlers.SnowflakeHandler<ChannelId>(id => id.Value, value => new ChannelId(value));
        var handler = new DapperTypeHandlers.OptionHandler<ChannelId>(inner);
        var parameter = new NpgsqlParameter();

        handler.SetValue(parameter, None);

        parameter.Value.ShouldBe(DBNull.Value);
        parameter.DbType.ShouldBe(DbType.Int64);
        handler.Parse(DBNull.Value).ShouldBe(None);
        handler.Parse(12m).ShouldBe(Some(new ChannelId(12)));
    }

    [Fact]
    public void ATextValue_UsesItsFactory()
    {
        var handler = new DapperTypeHandlers.TextHandler<ModerationReason>(reason => reason.Value,
            ModerationReason.FromDatabase);
        var parameter = new NpgsqlParameter();

        handler.SetValue(parameter, ModerationReason.FromDatabase("spam"));

        parameter.DbType.ShouldBe(DbType.String);
        parameter.Value.ShouldBe("spam");
        handler.Parse("spam").Value.ShouldBe("spam");
    }

    [Fact]
    public void ATimestamp_IsReadAsUtc()
    {
        var stored = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Unspecified);

        new DapperTypeHandlers.DateTimeOffsetHandler().Parse(stored)
            .ShouldBe(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ModerationActor_NullIsTheSystem()
    {
        DatabaseColumns.Actor(null).ShouldBeOfType<ModerationActor.System>();
        DatabaseColumns.Actor(9).ShouldBeOfType<ModerationActor.Moderator>().Id.ShouldBe(new ModeratorId(9));
        DatabaseColumns.ActorColumn(new ModerationActor.System()).ShouldBeNull();
        DatabaseColumns.ActorColumn(new ModerationActor.Moderator(new ModeratorId(Snowflake))).ShouldBe((long)Snowflake);
    }

    [Fact]
    public void IdArrays_AreBigints() =>
        DatabaseColumns.Ids([new GuildId(1), new GuildId(Snowflake)]).ShouldBe([1L, (long)Snowflake]);

    private sealed record TestDocument(int Version, bool Enabled = true, string? LogChannel = null);

    [Fact]
    public void AJsonbDocument_IsSentAsJsonb_AndReadBack()
    {
        var handler = new DapperTypeHandlers.JsonbHandler<TestDocument>(JsonSerializerOptions.Web);
        var parameter = new NpgsqlParameter();

        handler.SetValue(parameter, new Jsonb<TestDocument>(new TestDocument(2, false, "123")));

        parameter.NpgsqlDbType.ShouldBe(NpgsqlDbType.Jsonb);
        handler.Parse((string)parameter.Value!).Value.ShouldBe(new TestDocument(2, false, "123"));
    }

    [Fact]
    public void AJsonbDocument_IgnoresUnknownFields_AndDefaultsMissingOnes() =>
        new DapperTypeHandlers.JsonbHandler<TestDocument>(JsonSerializerOptions.Web)
            .Parse("""{"version":1,"removedSetting":5}""").Value
            .ShouldBe(new TestDocument(1));

    [Fact]
    public void AJsonbDocument_ThatDoesNotParse_Throws() =>
        Should.Throw<JsonException>(() =>
            new DapperTypeHandlers.JsonbHandler<TestDocument>(JsonSerializerOptions.Web).Parse("""{"version":"x"}"""));
}
