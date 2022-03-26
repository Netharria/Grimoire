// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.MarkMessageAsDeleted;
using DSharpPlus;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.DatabaseManagementModules
{
    [DiscordMessageDeletedEventSubscriber]
    [DiscordMessagesBulkDeletedEventSubscriber]
    internal class MessageEventManagementModule :
        IDiscordMessageDeletedEventSubscriber,
        IDiscordMessagesBulkDeletedEventSubscriber
    {
        private readonly IMediator _mediator;

        public MessageEventManagementModule(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args)
            => this._mediator.Send(new MarkMessageAsDeletedCommand { Ids = new ulong[] { args.Message.Id }, GuildId = args.Guild.Id });
        public Task DiscordOnMessagesBulkDeleted(DiscordClient sender, MessageBulkDeleteEventArgs args)
            => this._mediator.Send(new MarkMessageAsDeletedCommand { Ids = args.Messages.Select(x => x.Id).ToArray(), GuildId = args.Guild.Id });
    }
}
