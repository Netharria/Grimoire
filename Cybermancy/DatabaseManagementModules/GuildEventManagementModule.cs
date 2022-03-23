// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.Commands.GuildCommands.AddGuild;
using Cybermancy.Core.Features.Shared.Commands.GuildCommands.UpdateAllGuilds;
using Cybermancy.Core.Features.Shared.SharedDtos;
using DSharpPlus;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.DatabaseManagementModules
{
    [DiscordGuildDownloadCompletedEventSubscriber]
    [DiscordGuildCreatedEventSubscriber]
    public class GuildEventManagementModule :
        IDiscordGuildDownloadCompletedEventSubscriber,
        IDiscordGuildCreatedEventSubscriber
    {
        private readonly IMediator _mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedManagementModule"/> class.
        /// </summary>
        /// <param name="guildService"></param>
        public GuildEventManagementModule(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public Task DiscordOnGuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs args)
            => this._mediator.Send(new UpdateAllGuildsCommand
            {
                Guilds = args.Guilds.Keys.Select(x => new GuildDto { Id = x }),
                Users = args.Guilds.Values.SelectMany(x => x.Members)
                    .DistinctBy(x => x.Value.Id).Select(x => x.Value)
                    .Select(x =>
                    new UserDto {
                        AvatarUrl = x.AvatarUrl,
                        Id = x.Id,
                        UserName = $"{x.Username}#{x.Discriminator}"
                    }),
                GuildUsers = args.Guilds.Values.SelectMany(x => x.Members)
                    .Select(x => x.Value).Select(x =>
                    new GuildUserDto {
                        GuildId = x.Guild.Id,
                        UserId = x.Id,
                        DisplayName = x.DisplayName,
                        GuildAvatarUrl = x.GuildAvatarUrl,
                        Nickname = x.Nickname
                    }),
                Roles = args.Guilds.Values.Select(x => new { x.Id, x.Roles })
                    .Select(x => x.Roles.Select(y =>
                    new RoleDto {
                        GuildId = x.Id,
                        Id = y.Value.Id
                    }))
                    .SelectMany(x => x),
                Channels = args.Guilds.Values.SelectMany(x => x.Channels)
                    .Select(x =>
                    new ChannelDto {
                        Id = x.Value.Id,
                        GuildId = x.Value.GuildId.GetValueOrDefault(),
                        Name = x.Value.Name
                    })
            });


        public Task DiscordOnGuildCreated(DiscordClient sender, GuildCreateEventArgs args)
            => this._mediator.Send(new AddGuildCommand
            {
                GuildId = args.Guild.Id,
                Users = args.Guild.Members
                    .Select(x => x.Value)
                    .Select(x =>
                    new UserDto
                    {
                        AvatarUrl = x.AvatarUrl,
                        Id = x.Id,
                        UserName = $"{x.Username}#{x.Discriminator}"
                    }),
                GuildUsers = args.Guild.Members
                    .Select(x => x.Value)
                .Select(x =>
                    new GuildUserDto
                    {
                        GuildId = x.Guild.Id,
                        UserId = x.Id,
                        DisplayName = x.DisplayName,
                        GuildAvatarUrl = x.GuildAvatarUrl,
                        Nickname = x.Nickname
                    }),
                Roles = args.Guild.Roles
                    .Select(x => 
                    new RoleDto
                    {
                        GuildId = args.Guild.Id,
                        Id = x.Value.Id
                    }),
                Channels = args.Guild.Channels
                    .Select(x =>
                    new ChannelDto
                    {
                        Id = x.Value.Id,
                        GuildId = args.Guild.Id,
                        Name = x.Value.Name
                    })
            });
    }
}
