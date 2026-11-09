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
public abstract record CustomCommand
{
    public required CustomCommandName Name { get; init; }
    public required GuildId GuildId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required CustomCommandContent Content { get; init; }
    public ModeratorId? ModeratorId { get; init; }
    public ICollection<CustomCommandRole> Roles { get; protected init; } = [];

    /// <summary>
    ///     A copy to save as the newest version: the same type, content and roles, set by <paramref name="moderatorId" />
    ///     at <paramref name="createdAt" />. Used by /Commands Revert.
    /// </summary>
    public CustomCommand AsNewVersion(ModeratorId moderatorId, DateTimeOffset createdAt)
        => this with
        {
            CreatedAt = createdAt,
            ModeratorId = moderatorId,
            // A role row belongs to one version, so each is copied without the link to the version it came from.
            Roles = [.. this.Roles.Select(role => role with { CreatedAt = createdAt, CustomCommand = null })]
        };

    /// <summary>The roles a new command may be used by. An empty list means everyone may use it.</summary>
    protected static List<CustomCommandRole> AllowRoles(IEnumerable<RoleId> roleIds, CustomCommandName name,
        GuildId guildId, DateTimeOffset createdAt)
        =>
        [
            .. roleIds.Select(CustomCommandRole (roleId) => new CustomCommandAllowRole
            {
                RoleId = roleId, Name = name, GuildId = guildId, CreatedAt = createdAt
            })
        ];
}

public readonly record struct CustomCommandName
{
    private CustomCommandName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>Stored data is trusted. Rechecking it would let one legacy row break every query that loads it.</summary>
    internal static CustomCommandName FromDatabase(string value) => new(value);

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

    /// <summary>Stored data is trusted. Rechecking it would let one legacy row break every query that loads it.</summary>
    internal static CustomCommandEmbedColor FromDatabase(string value) => new(value);

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
    public static TextCustomCommand Create(
        CustomCommandName name,
        GuildId guildId,
        CustomCommandContent content,
        IEnumerable<RoleId> allowRoleIds,
        ModeratorId moderatorId,
        DateTimeOffset createdAt)
        => new()
        {
            Name = name,
            GuildId = guildId,
            CreatedAt = createdAt,
            Content = content,
            ModeratorId = moderatorId,
            Roles = AllowRoles(allowRoleIds, name, guildId, createdAt)
        };
}

public sealed record EmbedCustomCommand : CustomCommand
{
    public CustomCommandEmbedColor? EmbedColor { get; init; }

    public static EmbedCustomCommand Create(
        CustomCommandName name,
        GuildId guildId,
        CustomCommandContent content,
        CustomCommandEmbedColor? embedColor,
        IEnumerable<RoleId> allowRoleIds,
        ModeratorId moderatorId,
        DateTimeOffset createdAt)
        => new()
        {
            Name = name,
            GuildId = guildId,
            CreatedAt = createdAt,
            Content = content,
            EmbedColor = embedColor,
            ModeratorId = moderatorId,
            Roles = AllowRoles(allowRoleIds, name, guildId, createdAt)
        };
}

public readonly record struct CustomCommandContent
{
    private CustomCommandContent(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    ///     Stored data is trusted, and read as stored: rerunning <see cref="Create" /> would unescape it again on every
    ///     read, and one legacy row that fails it would break every query that loads it.
    /// </summary>
    internal static CustomCommandContent FromDatabase(string value) => new(value);

    public static Validation<CustomCommandContent> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<CustomCommandContent>.Fail(new Error("custom-command-content.empty",
                "Command content cannot be empty."));
        // Slash options can't hold a newline, so "\n" stands for one. Nothing else is unescaped: a backslash is
        // usually meant, as in C:\temp.
        var unescaped = value.Replace(@"\n", "\n");
        if (string.IsNullOrWhiteSpace(unescaped))
            return Validation<CustomCommandContent>.Fail(new Error("custom-command-content.empty",
                "Command content cannot be empty."));
        return unescaped.Length > 2000
            ? Validation<CustomCommandContent>.Fail(new Error("custom-command-content.too-long",
                "Command content cannot exceed 2000 characters."))
            : Validation<CustomCommandContent>.Succeed(new CustomCommandContent(unescaped));
    }

    public override string ToString() => Value;
}
