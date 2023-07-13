// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Shared.Queries.GetModuleAndCommandOverrideStateForGuild;
using Grimoire.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Discord.Attributes;
internal class SlashRequireGuildPermisssionsOrOverrideAttribute : SlashCheckBaseAttribute
{
    public Permissions Permissions { get; }

    public CommandPermissions CommandPermission { get; }

    public SlashRequireGuildPermisssionsOrOverrideAttribute(Permissions permissions, CommandPermissions permissionsCommand)
    {
        this.Permissions = permissions;
        this.CommandPermission = permissionsCommand;
    }

    public async override Task<bool> ExecuteChecksAsync(InteractionContext ctx)
    {

        if (ctx.Guild == null)
            return false;

        var usr = ctx.Member;
        if (usr == null)
            return false;

        if (usr.Id == ctx.Guild.OwnerId)
            return true;

        var pusr = usr.Permissions;

        if ((pusr & Permissions.Administrator) != 0)
            return true;
        if((pusr & this.Permissions) == this.Permissions)
            return true;

        using var scope = ctx.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new GetCommandEnabledQuery
        {
            UserId = ctx.User.Id,
            RoleIds = ctx.Member.Roles.Select(x => x.Id).ToArray(),
            ChannelId = ctx.Channel.Id,
            GuildId = ctx.Guild.Id,
            Permissions = this.CommandPermission
        });
    }
}
