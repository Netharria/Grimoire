// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.DatabaseManagementModules
{
    [DiscordGuildEventsSubscriber]
    public class GuildEventMangementModule : IDiscordGuildEventsSubscriber
    {
        private readonly IMediator _mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedManagementModule"/> class.
        /// </summary>
        /// <param name="guildService"></param>
        public GuildEventMangementModule(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public Task DiscordOnGuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs args) =>
            Task.CompletedTask;

        public Task DiscordOnGuildCreated(DiscordClient sender, GuildCreateEventArgs args) => Task.CompletedTask;

        public Task DiscordOnGuildUpdated(DiscordClient sender, GuildUpdateEventArgs args) => Task.CompletedTask;

        public Task DiscordOnGuildDeleted(DiscordClient sender, GuildDeleteEventArgs args) => Task.CompletedTask;

        #region UnusedEvents
        public Task DiscordOnGuildAvailable(DiscordClient sender, GuildCreateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnGuildEmojisUpdated(DiscordClient sender, GuildEmojisUpdateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnGuildIntegrationsUpdated(DiscordClient sender, GuildIntegrationsUpdateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnGuildStickersUpdated(DiscordClient sender, GuildStickersUpdateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnGuildUnavailable(DiscordClient sender, GuildDeleteEventArgs args) => Task.CompletedTask;
        #endregion
    }
}
