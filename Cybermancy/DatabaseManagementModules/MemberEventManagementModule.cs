// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.DatabaseManagementModules
{
    [DiscordGuildMemberAddedEventSubscriber]
    [DiscordGuildMemberRemovedEventSubscriber]
    [DiscordGuildMemberUpdatedEventSubscriber]
    [DiscordUserUpdatedEventSubscriber]
    internal class MemberEventManagementModule :
        IDiscordGuildMemberAddedEventSubscriber,
        IDiscordGuildMemberRemovedEventSubscriber,
        IDiscordGuildMemberUpdatedEventSubscriber,
        IDiscordUserUpdatedEventSubscriber
    {
        private readonly IMediator _mediator;

        public MemberEventManagementModule(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddEventArgs args) => throw new NotImplementedException();

        public Task DiscordOnGuildMemberRemoved(DiscordClient sender, GuildMemberRemoveEventArgs args) => throw new NotImplementedException();

        public Task DiscordOnGuildMemberUpdated(DiscordClient sender, GuildMemberUpdateEventArgs args) => throw new NotImplementedException();

        public Task DiscordOnUserUpdated(DiscordClient sender, UserUpdateEventArgs args) => throw new NotImplementedException();
    }
}
