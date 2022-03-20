// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus;
using DSharpPlus.EventArgs;
using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.DatabaseManagementModules
{
    [DiscordWebSocketEventSubscriber]
    public class SharedManagementModule : IDiscordWebSocketEventSubscriber
    {


        public Task DiscordOnReady(DiscordClient sender, ReadyEventArgs args) => Task.CompletedTask;

        #region UnusedEvents

        public Task DiscordOnHeartbeated(DiscordClient sender, HeartbeatEventArgs args) => Task.CompletedTask;

        public Task DiscordOnResumed(DiscordClient sender, ReadyEventArgs args) => Task.CompletedTask;

        public Task DiscordOnSocketClosed(DiscordClient sender, SocketCloseEventArgs args) => Task.CompletedTask;

        public Task DiscordOnSocketOpened(DiscordClient sender, SocketEventArgs args) => Task.CompletedTask;

        public Task DiscordOnZombied(DiscordClient sender, ZombiedEventArgs args) => Task.CompletedTask;

        public Task DiscordOSocketErrored(DiscordClient sender, SocketErrorEventArgs args) => Task.CompletedTask;

        #endregion
    }
}
