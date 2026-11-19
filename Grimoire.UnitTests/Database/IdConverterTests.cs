// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Grimoire.UnitTests.Database;

/// <summary>
///     P-9 (spec 2.3): ids are stored as bigint. Read from the model, so the test sees the converter EF actually
///     uses for each kind of id, not a copy.
/// </summary>
public sealed class IdConverterTests
{
    private const ulong Snowflake = 1_234_567_890_123_456_789;

    public static TheoryData<ulong> Values => [1UL, Snowflake, long.MaxValue];

    [Theory]
    [MemberData(nameof(Values))]
    public void EverySnowflakeId_RoundTripsThroughBigint(ulong value)
    {
        RoundTrip(Converter<Message>(nameof(Message.Id)), new MessageId(value)).ShouldBe(new MessageId(value));
        RoundTrip(Converter<Message>(nameof(Message.GuildId)), new GuildId(value)).ShouldBe(new GuildId(value));
        RoundTrip(Converter<Message>(nameof(Message.ChannelId)), new ChannelId(value)).ShouldBe(new ChannelId(value));
        RoundTrip(Converter<Message>(nameof(Message.UserId)), new UserId(value)).ShouldBe(new UserId(value));
        RoundTrip(Converter<Pardon>(nameof(Pardon.ModeratorId)), new ModeratorId(value)).ShouldBe(new ModeratorId(value));
        RoundTrip(Converter<CustomCommandRole>(nameof(CustomCommandRole.RoleId)), new RoleId(value)).ShouldBe(new RoleId(value));
        RoundTrip(Converter<Attachment>(nameof(Attachment.Id)), new AttachmentId(value)).ShouldBe(new AttachmentId(value));
    }

    [Fact]
    public void ASnowflake_IsStoredAsTheSameNumber_SoOrderIsKept()
    {
        var converter = Converter<Message>(nameof(Message.GuildId));

        converter.ProviderClrType.ShouldBe(typeof(long));
        converter.ConvertToProvider(new GuildId(Snowflake)).ShouldBe((long)Snowflake);
        ((long)converter.ConvertToProvider(new GuildId(Snowflake))!)
            .ShouldBeLessThan((long)converter.ConvertToProvider(new GuildId(Snowflake + 1))!);
    }

    [Fact]
    public void AnActor_IsAModeratorIdOrNull()
    {
        var converter = Converter<Sin>(nameof(Sin.Actor));

        converter.ProviderClrType.ShouldBe(typeof(long?));
        converter.ConvertToProvider(new ModerationActor.Moderator(new ModeratorId(Snowflake))).ShouldBe((long)Snowflake);
        converter.ConvertToProvider(new ModerationActor.System()).ShouldBeNull();
        converter.ConvertFromProvider((long)Snowflake).ShouldBe(new ModerationActor.Moderator(new ModeratorId(Snowflake)));
        converter.ConvertFromProvider(null).ShouldBe(new ModerationActor.System());
    }

    private static object? RoundTrip(ValueConverter converter, object value)
        => converter.ConvertFromProvider(converter.ConvertToProvider(value));

    private static ValueConverter Converter<TEntity>(string property)
    {
        // No service provider caching: EF caches a built model per context type for the whole process, and a fresh
        // model is what proves the convention (mutation testing changes it under a running process).
        using var context = new GrimoireDbContext(
            new DbContextOptionsBuilder<GrimoireDbContext>().UseNpgsql("Host=unused")
                .EnableServiceProviderCaching(false).Options);
        return context.Model.FindEntityType(typeof(TEntity))!.FindProperty(property)!.GetValueConverter()!;
    }

    [Fact]
    public void ASinId_IsStoredAsItsOwnBigint()
        => RoundTrip(Converter<Sin>(nameof(Sin.Id)), new SinId(42)).ShouldBe(new SinId(42));

    /// <summary>The Settings items keep a plain <c>ulong</c> id, stored as bigint by the same convention.</summary>
    [Fact]
    public void ASettingsItemsPlainId_IsStoredAsBigint()
    {
        using var context = new Grimoire.Settings.SettingsDbContext(
            new DbContextOptionsBuilder<Grimoire.Settings.SettingsDbContext>().UseNpgsql("Host=unused")
                .EnableServiceProviderCaching(false).Options);
        var id = context.Model.FindEntityType(typeof(Grimoire.Settings.Domain.XpTrackedItem))!.FindProperty("Id")!;

        id.GetColumnType().ShouldBe("bigint");
        id.GetValueConverter()!.ConvertToProvider((ulong)long.MaxValue).ShouldBe(long.MaxValue);
    }
}
