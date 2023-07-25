// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace Grimoire.Discord.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SlashRequireUserGuildPermissionsAttribute : SlashCheckBaseAttribute
{
    /// <summary>
    /// Gets the permissions required by this attribute.
    /// </summary>
    public Permissions Permissions { get; }

    /// <summary>
    /// Defines that usage of this command is restricted to members with specified permissions.
    /// </summary>
    /// <param name="permissions">Permissions required to execute this command.</param>
    public SlashRequireUserGuildPermissionsAttribute(Permissions permissions)
    {
        this.Permissions = permissions;
    }

    /// <summary>
    /// Runs checks.
    /// </summary>
    public override Task<bool> ExecuteChecksAsync(InteractionContext ctx)
    {
        if (ctx.Guild == null)
            return Task.FromResult(false);

        var usr = ctx.Member;
        if (usr == null)
            return Task.FromResult(false);

        if (usr.Id == ctx.Guild.OwnerId)
            return Task.FromResult(true);

        var pusr = usr.Permissions;

        if ((pusr & Permissions.Administrator) != 0)
            return Task.FromResult(true);

        return (pusr & this.Permissions) == this.Permissions ? Task.FromResult(true) : Task.FromResult(false);
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class RequireUserGuildPermissionsAttribute : CheckBaseAttribute
{
    /// <summary>
    /// Gets the permissions required by this attribute.
    /// </summary>
    public Permissions Permissions { get; }

    /// <summary>
    /// Defines that usage of this command is restricted to members with specified permissions.
    /// </summary>
    /// <param name="permissions">Permissions required to execute this command.</param>
    public RequireUserGuildPermissionsAttribute(Permissions permissions)
    {
        this.Permissions = permissions;
    }

    /// <summary>
    /// Runs checks.
    /// </summary>
    public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
    {
        if (ctx.Guild == null)
            return Task.FromResult(false);

        var usr = ctx.Member;
        if (usr == null)
            return Task.FromResult(false);

        if (usr.Id == ctx.Guild.OwnerId)
            return Task.FromResult(true);

        var pusr = usr.Permissions;

        if ((pusr & Permissions.Administrator) != 0)
            return Task.FromResult(true);

        return (pusr & this.Permissions) == this.Permissions ? Task.FromResult(true) : Task.FromResult(false);
    }
}
