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

    public CommandOutputFormat OutputFormat
        => IsEmbedded ? new CommandOutputFormat.Embedded(EmbedColor) : new CommandOutputFormat.PlainText();

    public CommandAccess Access
        => RestrictedUse
            ? new CommandAccess.Allowlist(Roles.Select(r => r.RoleId).ToList())
            : new CommandAccess.Open();
}

public readonly record struct CustomCommandName
{
    public string Value { get; }

    private CustomCommandName(string value)
    {
        Value = value;
    }

    public static CustomCommandName ParseFromDatabase(string value)
        => Create(value)
            .Match(
                onValid: result => result,
                onInvalid: _ => throw new ArgumentException($"'{value}' is not a valid command name.", nameof(value)));

    public override string ToString() => Value;

    public static Validation<CustomCommandName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<CustomCommandName>.Fail(new Error("custom-command-name.validation.is-null-or-empty",
                "A value for command name must be provided."));
        var trimmed = value.Trim();
        if(trimmed.Any(char.IsWhiteSpace))
            return Validation<CustomCommandName>.Fail(new Error("custom-command-name.validation.contains-space",
                "A command name cannot contain spaces."));;
        return trimmed.Length > 24
            ? Validation<CustomCommandName>.Fail(new Error("custom-command-name.validation.length",
                "A command name cannot be more than 24 characters in length."))
            : Validation<CustomCommandName>.Succeed(new CustomCommandName(trimmed));
    }
}

public readonly partial record struct CustomCommandEmbedColor
{
    public string Value { get; }

    private CustomCommandEmbedColor(string value) => Value = value;

    public static CustomCommandEmbedColor ParseFromDatabase(string value)
        => Create(value)
            .Match(
                onValid: result => result,
                onInvalid: _ => throw new ArgumentException($"'{value}' is not a valid embed color.", nameof(value)));

    public override string ToString() => Value;

    [GeneratedRegex(@"^[0-9A-Fa-f]{6}$", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();

    public static Validation<CustomCommandEmbedColor> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<CustomCommandEmbedColor>.Fail(new Error("custom-command-embed-color.validation.is-null-or-empty",
                "A value for command embed color must be provided."));
        var str = value.Trim();
        if (str.StartsWith('#')) str = str[1..];
        return ValidHexColor().IsMatch(str)
            ? Validation<CustomCommandEmbedColor>.Succeed(new CustomCommandEmbedColor(str.ToUpperInvariant()))
            : Validation<CustomCommandEmbedColor>.Fail(new Error("custom-command-embed-color.validation.does-not-contain-hex",
            "A command embed color must be a 6 character hex color code."));
    }
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
