// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Discord.Attributes;

/// <summary>
/// Defines that usage of this command is restricted to members with specified permissions.
/// </summary>
/// <param name="permissions">Permissions required to execute this command.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class SlashRequireUserGuildPermissionsAttribute(Permissions permissions) : SlashCheckBaseAttribute
{
    /// <summary>
    /// Gets the permissions required by this attribute.
    /// </summary>
    public Permissions Permissions { get; } = permissions;

    /// <summary>
    /// Runs checks.
    /// </summary>
    public override Task<bool> ExecuteChecksAsync(InteractionContext ctx)
    {
        if (ctx.Guild is null)
            return Task.FromResult(false);

        var usr = ctx.Member;
        if (usr is null)
            return Task.FromResult(false);

        if (usr.Id == ctx.Guild.OwnerId)
            return Task.FromResult(true);

        var pusr = usr.Permissions;

        if ((pusr & Permissions.Administrator) != 0)
            return Task.FromResult(true);

        return (pusr & this.Permissions) == this.Permissions ? Task.FromResult(true) : Task.FromResult(false);
    }
}
