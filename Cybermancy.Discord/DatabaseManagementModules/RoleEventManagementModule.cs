// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.Commands.RoleCommands.AddRole;
using Cybermancy.Core.Features.Shared.Commands.RoleCommands.DeleteRole;
using DSharpPlus;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.Discord.DatabaseManagementModules
{
    [DiscordGuildRoleCreatedEventSubscriber]
    [DiscordGuildRoleDeletedEventSubscriber]
    public class RoleEventManagementModule :
        IDiscordGuildRoleCreatedEventSubscriber,
        IDiscordGuildRoleDeletedEventSubscriber
    {
        private readonly IMediator _mediator;

        public RoleEventManagementModule(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public Task DiscordOnGuildRoleCreated(DiscordClient sender, GuildRoleCreateEventArgs args)
            => this._mediator.Send(new AddRoleCommand { RoleId = args.Role.Id, GuildId = args.Guild.Id });
        public Task DiscordOnGuildRoleDeleted(DiscordClient sender, GuildRoleDeleteEventArgs args)
            => this._mediator.Send(new DeleteRoleCommand { RoleId = args.Role.Id });

    }
}
