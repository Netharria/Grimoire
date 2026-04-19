// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;

namespace Grimoire.Features.Shared.Attributes;

/// <summary>
///     Defines that usage of this command is restricted to members with specified permissions.
/// </summary>
/// <param name="permissions">Permissions required to execute this command.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
internal sealed class RequireUserGuildPermissionsAttribute(DiscordPermission permissions)
    : ContextCheckAttribute
{
    /// <summary>
    ///     Gets the permissions required by this attribute.
    /// </summary>
    public DiscordPermission Permissions { get; } = permissions;
}

internal sealed class RequireUserGuildPermissionsCheck : IContextCheck<RequireUserGuildPermissionsAttribute>
{
    public ValueTask<string?> ExecuteCheckAsync(RequireUserGuildPermissionsAttribute attribute, CommandContext context)
    {
        if (context.Guild is null)
            return ValueTask.FromResult<string?>("This command can only be used in a server.");

        var member = context.Member;
        if (member is null)
            return ValueTask.FromResult<string?>("This command can only be used in a server.");

        if (member.Id == context.Guild.OwnerId)
            return ValueTask.FromResult<string?>(null);

        var memberPermissions = member.Permissions;

        if (memberPermissions.HasPermission(DiscordPermission.Administrator))
            return ValueTask.FromResult<string?>(null);

        return memberPermissions.HasPermission(attribute.Permissions)
            ? ValueTask.FromResult<string?>(null)
            : ValueTask.FromResult<string?>("You do not have the required permissions to use this command.");
    }
}
