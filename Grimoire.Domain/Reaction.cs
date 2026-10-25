// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Grimoire.Domain;

/// <summary>
///     One member's reaction on a logged message. A member can react once with each emoji, so reacting again after
///     removing it revives the same reaction. Reactions are soft deleted and are purged with their message.
/// </summary>
[UsedImplicitly]
public sealed record Reaction
{
    public required MessageId MessageId { get; init; }
    public Message? Message { get; init; }
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }
    public required ReactionEmoji Emoji { get; init; }

    /// <summary>When the member most recently added this reaction.</summary>
    public required DateTimeOffset ReactedAt { get; init; }

    /// <summary>When the reaction was taken off the message, or <c>null</c> while it is still there.</summary>
    public DateTimeOffset? RemovedAt { get; init; }

    public bool IsRemoved => this.RemovedAt is not null;
}

/// <summary>
///     The emoji of a reaction: either a server's custom emoji or a standard Unicode emoji. The hierarchy is
///     closed, so these two cases are the only ones that can exist.
/// </summary>
public abstract partial record ReactionEmoji
{
    /// <summary>The longest stored form: <c>&lt;a:</c> plus a 32-character name, a colon, a 20-digit id and <c>&gt;</c>.</summary>
    internal const int MaxLength = 64;

    private ReactionEmoji()
    {
    }

    /// <summary>Reads the stored form written by <see cref="ToString" />.</summary>
    internal static ReactionEmoji ParseFromDatabase(string value)
        => CustomMarkup().Match(value) is { Success: true } markup
            ? Custom.ParseStored(markup)
            : Standard.ParseStored(value);

    [GeneratedRegex(@"^<(a?):([A-Za-z0-9_]*):([0-9]+)>$", RegexOptions.None, 1000)]
    private static partial Regex CustomMarkup();

    /// <summary>A server emoji. Discord leaves the name out when the emoji has since been deleted.</summary>
    public sealed partial record Custom : ReactionEmoji
    {
        private Custom(EmojiId id, string? name, bool isAnimated)
        {
            this.Id = id;
            this.Name = name;
            this.IsAnimated = isAnimated;
        }

        public EmojiId Id { get; }
        public string? Name { get; }
        public bool IsAnimated { get; }

        public static Validation<Custom> Create(EmojiId id, string? name, bool isAnimated)
            => (id.Value, name) switch
            {
                (0, _) => Validation<Custom>.Fail(new Error("reaction-emoji.custom.id-missing",
                    "A custom emoji must have an id.")),
                (_, not null) when !ValidName().IsMatch(name) => Validation<Custom>.Fail(new Error(
                    "reaction-emoji.custom.name-invalid",
                    "A custom emoji name must be 2 to 32 letters, digits or underscores.")),
                _ => Validation<Custom>.Succeed(new Custom(id, name, isAnimated))
            };

        internal static Custom ParseStored(Match markup)
            => Create(
                    new EmojiId(ulong.Parse(markup.Groups[3].ValueSpan, CultureInfo.InvariantCulture)),
                    markup.Groups[2].Length == 0 ? null : markup.Groups[2].Value,
                    markup.Groups[1].Length != 0)
                .Match(
                    emoji => emoji,
                    _ => throw new ArgumentException($"'{markup.Value}' is not a valid custom emoji.", nameof(markup)));

        /// <summary>Discord's message markup, which is also the stored form.</summary>
        public override string ToString()
            => $"<{(this.IsAnimated ? "a" : "")}:{this.Name}:{this.Id.Value.ToString(CultureInfo.InvariantCulture)}>";

        [GeneratedRegex(@"^[A-Za-z0-9_]{2,32}$", RegexOptions.None, 1000)]
        private static partial Regex ValidName();
    }

    /// <summary>A standard Unicode emoji, such as 👍, stored as the emoji itself.</summary>
    public sealed record Standard : ReactionEmoji
    {
        private Standard(string value) => this.Value = value;

        public string Value { get; }

        public static Validation<Standard> Create(string? value)
            => value switch
            {
                null or "" => Validation<Standard>.Fail(new Error("reaction-emoji.standard.empty",
                    "A standard emoji cannot be empty.")),
                { Length: > MaxLength } => Validation<Standard>.Fail(new Error("reaction-emoji.standard.too-long",
                    $"A standard emoji cannot be longer than {MaxLength} characters.")),
                // Keeps the stored form unambiguous: only custom emoji start with '<'.
                _ when value.StartsWith('<') || value.Any(char.IsWhiteSpace) => Validation<Standard>.Fail(
                    new Error("reaction-emoji.standard.invalid", "A standard emoji cannot contain markup or spaces.")),
                _ => Validation<Standard>.Succeed(new Standard(value))
            };

        internal static Standard ParseStored(string value)
            => Create(value).Match(
                emoji => emoji,
                _ => throw new ArgumentException($"'{value}' is not a valid standard emoji.", nameof(value)));

        public override string ToString() => this.Value;
    }
}
