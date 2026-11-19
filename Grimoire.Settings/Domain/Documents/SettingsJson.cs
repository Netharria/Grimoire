// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Domain.Documents;

/// <summary>
///     The one JSON encoding of the settings documents (spec 2.2, section 3.1), shared by EF now and by Dapper's
///     <c>RegisterDocument</c> in 3.1. Snowflakes are strings, as in Discord's API, since JavaScript readers round
///     numbers above 2^53 (OQ-3). Durations are whole seconds. A default (null, or a false flag) is left out, so a
///     document holds only what a guild chose. Unknown fields are ignored on read.
/// </summary>
public static class SettingsJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters =
        {
            new SnowflakeConverter<ChannelId>(id => id.Value, value => new ChannelId(value)),
            new SnowflakeConverter<RoleId>(id => id.Value, value => new RoleId(value)),
            new NumberConverter<XpGainAmount>(value => value.Value, XpGainAmount.FromDatabase),
            new NumberConverter<LevelScalingBase>(value => value.Value, LevelScalingBase.FromDatabase),
            new NumberConverter<LevelScalingModifier>(value => value.Value, LevelScalingModifier.FromDatabase),
            new SecondsConverter<XpTimeoutPeriod>(value => value.TotalSeconds, XpTimeoutPeriod.FromDatabase),
            new SecondsConverter<TimeSpan>(value => (long)value.TotalSeconds, TimeSpan.FromSeconds)
        }
    };

    public static string Serialize<T>(T document) where T : class, ISettingsDocument<T>
        => JsonSerializer.Serialize(document, Options);

    /// <summary>
    ///     Parses a stored document. One that doesn't parse throws: reading it as defaults would quietly turn a guild's
    ///     modules and logs off, so it's a defect (spec 2.2, section 3.2).
    /// </summary>
    public static T Parse<T>(string json) where T : class, ISettingsDocument<T>
        => JsonSerializer.Deserialize<T>(json, Options)
           ?? throw new InvariantViolationException($"A stored {T.Kind} settings document is null.");

    private sealed class SnowflakeConverter<T>(Func<T, ulong> unwrap, Func<ulong, T> wrap) : JsonConverter<T>
        where T : struct
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => wrap(reader.TokenType == JsonTokenType.String
                ? ulong.Parse(reader.GetString()!, NumberStyles.None, CultureInfo.InvariantCulture)
                : reader.GetUInt64());

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(unwrap(value).ToString(CultureInfo.InvariantCulture));
    }

    private sealed class NumberConverter<T>(Func<T, int> unwrap, Func<int, T> wrap) : JsonConverter<T>
        where T : struct
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => wrap(reader.GetInt32());

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteNumberValue(unwrap(value));
    }

    private sealed class SecondsConverter<T>(Func<T, long> unwrap, Func<long, T> wrap) : JsonConverter<T>
        where T : struct
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => wrap(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteNumberValue(unwrap(value));
    }
}
