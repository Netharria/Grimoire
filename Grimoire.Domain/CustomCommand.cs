// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public abstract record CustomCommand
{
    public required CustomCommandName Name { get; init; }
    public required GuildId GuildId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required CustomCommandContent Content { get; init; }
    public ModeratorId? ModeratorId { get; init; }
    public ICollection<CustomCommandRole> Roles { get; protected init; } = [];
}

public readonly record struct CustomCommandName
{
    private CustomCommandName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static CustomCommandName ParseFromDatabase(string value)
        => Create(value)
            .Match(
                result => result,
                _ => throw new ArgumentException($"'{value}' is not a valid command name.", nameof(value)));

    public override string ToString() => Value;

    public static Validation<CustomCommandName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<CustomCommandName>.Fail(new Error("custom-command-name.validation.is-null-or-empty",
                "A value for command name must be provided."));
        var trimmed = value.Trim();
        if (trimmed.Any(char.IsWhiteSpace))
            return Validation<CustomCommandName>.Fail(new Error("custom-command-name.validation.contains-space",
                "A command name cannot contain spaces."));
        return trimmed.Length > 24
            ? Validation<CustomCommandName>.Fail(new Error("custom-command-name.validation.length",
                "A command name cannot be more than 24 characters in length."))
            : Validation<CustomCommandName>.Succeed(new CustomCommandName(trimmed));
    }
}

public readonly partial record struct CustomCommandEmbedColor
{
    private CustomCommandEmbedColor(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static CustomCommandEmbedColor ParseFromDatabase(string value)
        => Create(value)
            .Match(
                result => result,
                _ => throw new ArgumentException($"'{value}' is not a valid embed color.", nameof(value)));

    public override string ToString() => Value;

    [GeneratedRegex(@"^[0-9A-Fa-f]{6}$", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();

    public static Validation<CustomCommandEmbedColor> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<CustomCommandEmbedColor>.Fail(new Error(
                "custom-command-embed-color.validation.is-null-or-empty",
                "A value for command embed color must be provided."));
        var str = value.Trim();
        if (str.StartsWith('#')) str = str[1..];
        return ValidHexColor().IsMatch(str)
            ? Validation<CustomCommandEmbedColor>.Succeed(new CustomCommandEmbedColor(str.ToUpperInvariant()))
            : Validation<CustomCommandEmbedColor>.Fail(new Error(
                "custom-command-embed-color.validation.does-not-contain-hex",
                "A command embed color must be a 6 character hex color code."));
    }
}

public sealed record TextCustomCommand : CustomCommand
{
    public static Validation<CustomCommand> Create(
        CustomCommandName name,
        GuildId guildId,
        CustomCommandContent content,
        ICollection<CustomCommandRole> customCommandRoles,
        ModeratorId? moderatorId)
    {
        var now = DateTimeOffset.UtcNow;
        return Validation<CustomCommand>.Succeed(new TextCustomCommand
        {
            Name = name,
            GuildId = guildId,
            CreatedAt = now,
            Content = content,
            ModeratorId = moderatorId,
            Roles = customCommandRoles.Select<CustomCommandRole, CustomCommandRole>(r => r switch
            {
                CustomCommandAllowRole allow => allow with { CreatedAt = now },
                CustomCommandDenyRole deny => deny with { CreatedAt = now },
                _ => throw new UnreachableException()
            }).ToList()
        });
    }
}

public sealed record EmbedCustomCommand : CustomCommand
{
    public CustomCommandEmbedColor? EmbedColor { get; init; }

    public static Validation<CustomCommand> Create(
        CustomCommandName name,
        GuildId guildId,
        CustomCommandContent content,
        CustomCommandEmbedColor? embedColor,
        ICollection<CustomCommandRole> customCommandRoles,
        ModeratorId? moderatorId)
    {
        var now = DateTimeOffset.UtcNow;
        return Validation<CustomCommand>.Succeed(new EmbedCustomCommand
        {
            Name = name,
            GuildId = guildId,
            CreatedAt = now,
            Content = content,
            EmbedColor = embedColor,
            ModeratorId = moderatorId,
            Roles = customCommandRoles.Select<CustomCommandRole, CustomCommandRole>(r => r switch
            {
                CustomCommandAllowRole allow => allow with { CreatedAt = now },
                CustomCommandDenyRole deny => deny with { CreatedAt = now },
                _ => throw new UnreachableException()
            }).ToList()
        });
    }
}

public readonly record struct CustomCommandContent
{
    private CustomCommandContent(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static CustomCommandContent ParseFromDatabase(string value)
        => Create(value).Match(
            v => v,
            _ => throw new ArgumentException($"'{value}' is not valid command content.", nameof(value)));

    public static Validation<CustomCommandContent> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<CustomCommandContent>.Fail(new Error("custom-command-content.empty",
                "Command content cannot be empty."));
        return value.Length > 2000
            ? Validation<CustomCommandContent>.Fail(new Error("custom-command-content.too-long",
                "Command content cannot exceed 2000 characters."))
            : Validation<CustomCommandContent>.Succeed(new CustomCommandContent(value));
    }

    public override string ToString() => Value;
}
