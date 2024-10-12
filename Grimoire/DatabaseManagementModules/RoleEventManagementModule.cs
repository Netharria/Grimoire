// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Commands;

namespace Grimoire.DatabaseManagementModules;

internal sealed class RoleEventManagementModule(IMediator mediator)
{
    private readonly IMediator _mediator = mediator;

    public async Task DiscordOnGuildRoleCreated(DiscordClient sender, GuildRoleCreatedEventArgs args)
        => await this._mediator.Send(new AddRoleCommand { RoleId = args.Role.Id, GuildId = args.Guild.Id });
    public async Task DiscordOnGuildRoleDeleted(DiscordClient sender, GuildRoleDeletedEventArgs args)
        => await this._mediator.Send(new DeleteRoleCommand { RoleId = args.Role.Id });

}
