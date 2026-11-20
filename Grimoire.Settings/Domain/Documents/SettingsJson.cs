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
            new SnowflakeConverter<UserId>(id => id.Value, value => new UserId(value)),
            new RewardMessageConverter(),
            new JsonStringEnumConverter<MessageLogOverrideOption>(),
            new JsonStringEnumConverter<SpamFilterOverrideOption>(),
            new MapConverterFactory(),
            new SetConverterFactory(),
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

        // A snowflake keys a Map (spec 2.5): an object's property names are strings, like the values.
        public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
            => wrap(ulong.Parse(reader.GetString()!, NumberStyles.None, CultureInfo.InvariantCulture));

        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WritePropertyName(unwrap(value).ToString(CultureInfo.InvariantCulture));
    }

    private sealed class RewardMessageConverter : JsonConverter<RewardMessage>
    {
        // FromDatabase: a stored message outside today's limits still reads (S-1).
        public override RewardMessage Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
            => RewardMessage.FromDatabase(reader.GetString())
               ?? throw new JsonException("A reward message is null.");

        public override void Write(Utf8JsonWriter writer, RewardMessage value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    /// <summary>
    ///     LanguageExt's <c>Map</c> as a JSON object (spec 2.5). The key's converter writes it as a property name.
    ///     <c>Map</c> keeps its keys sorted and compares by value, so the same content always writes the same JSON
    ///     and a document's "unchanged" check works.
    /// </summary>
    private sealed class MapConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Map<,>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            => (JsonConverter)Activator.CreateInstance(
                typeof(MapConverter<,>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
    }

    private sealed class MapConverter<TKey, TValue> : JsonConverter<Map<TKey, TValue>> where TKey : notnull
    {
        public override Map<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expected an object for a map, found {reader.TokenType}.");
            var keys = (JsonConverter<TKey>)options.GetConverter(typeof(TKey));
            var map = LanguageExt.Map<TKey, TValue>.Empty;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var key = keys.ReadAsPropertyName(ref reader, typeof(TKey), options);
                reader.Read();
                var value = JsonSerializer.Deserialize<TValue>(ref reader, options)
                            ?? throw new JsonException("A map holds a null value.");
                map = map.AddOrUpdate(key, value);
            }

            return map;
        }

        public override void Write(Utf8JsonWriter writer, Map<TKey, TValue> value, JsonSerializerOptions options)
        {
            var keys = (JsonConverter<TKey>)options.GetConverter(typeof(TKey));
            writer.WriteStartObject();
            foreach (var (key, item) in value)
            {
                keys.WriteAsPropertyName(writer, key, options);
                JsonSerializer.Serialize(writer, item, options);
            }

            writer.WriteEndObject();
        }
    }

    /// <summary>LanguageExt's <c>Set</c> as a JSON array, in its sorted order (spec 2.5).</summary>
    private sealed class SetConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Set<>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            => (JsonConverter)Activator.CreateInstance(
                typeof(SetConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
    }

    private sealed class SetConverter<T> : JsonConverter<Set<T>>
    {
        public override Set<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => toSet(JsonSerializer.Deserialize<T[]>(ref reader, options)
                     ?? throw new JsonException("A set is null."));

        public override void Write(Utf8JsonWriter writer, Set<T> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value.ToArray(), options);
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
