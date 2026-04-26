// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed record CustomCommand
{
    public required CustomCommandName Name { get; init; }
    public required GuildId GuildId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string Content { get; init; }
    public required bool IsEmbedded { get; init; }
    public CustomCommandEmbedColor? EmbedColor { get; init; }
    public required bool RestrictedUse { get; init; }
    public ModeratorId? ModeratorId { get; init; }

    public ICollection<CustomCommandRole> Roles { get; init; } = [];
}

public readonly record struct CustomCommandName
{
    public string Value { get; }

    private CustomCommandName(string value) => Value = value;

    public static CustomCommandName? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Any(char.IsWhiteSpace) || trimmed.Length > 24
            ? null
            : new CustomCommandName(trimmed);
    }

    public static CustomCommandName Parse(string value)
        => TryParse(value) ?? throw new ArgumentException($"'{value}' is not a valid command name.", nameof(value));

    public override string ToString() => Value;
}

public readonly partial record struct CustomCommandEmbedColor
{
    public string Value { get; }

    private CustomCommandEmbedColor(string value) => Value = value;

    public static CustomCommandEmbedColor? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var str = value.Trim();
        if (str.StartsWith('#')) str = str[1..];
        return ValidHexColor().IsMatch(str)
            ? new CustomCommandEmbedColor(str.ToUpperInvariant())
            : null;
    }

    public static CustomCommandEmbedColor Parse(string value)
        => TryParse(value) ?? throw new ArgumentException($"'{value}' is not a valid embed color.", nameof(value));

    public override string ToString() => Value;

    [GeneratedRegex(@"^[0-9A-Fa-f]{6}$", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();
}

public abstract record CommandOutputFormat
{
    public sealed record PlainText : CommandOutputFormat;
    public sealed record Embedded(CustomCommandEmbedColor? Color) : CommandOutputFormat;
}

public abstract record CommandAccess
{
    public sealed record Open : CommandAccess;
    public sealed record Allowlist(IReadOnlyCollection<RoleId> Roles) : CommandAccess;
    public sealed record Blocklist(IReadOnlyCollection<RoleId> Roles) : CommandAccess;
}
