// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Logging;
using Cybermancy.Core.Features.Shared.Commands.GuildCommands.AddGuild;
using Cybermancy.Core.Features.Shared.Commands.GuildCommands.UpdateAllGuilds;
using Cybermancy.Core.Features.Shared.SharedDtos;
using Cybermancy.Discord.Extensions;
using Cybermancy.Domain;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Mediator;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.Discord.DatabaseManagementModules
{
    [DiscordGuildDownloadCompletedEventSubscriber]
    [DiscordGuildCreatedEventSubscriber]
    [DiscordInviteCreatedEventSubscriber]
    [DiscordInviteDeletedEventSubscriber]
    public class GuildEventManagementModule :
        IDiscordGuildDownloadCompletedEventSubscriber,
        IDiscordGuildCreatedEventSubscriber,
        IDiscordInviteCreatedEventSubscriber,
        IDiscordInviteDeletedEventSubscriber
    {
        private readonly IMediator _mediator;
        private readonly IInviteService _inviteService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedManagementModule"/> class.
        /// </summary>
        /// <param name="guildService"></param>
        public GuildEventManagementModule(IMediator mediator, IInviteService inviteService)
        {
            this._mediator = mediator;
            this._inviteService = inviteService;
        }

        public async Task DiscordOnGuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs args)
            => await this._mediator.Send(new UpdateAllGuildsCommand
            {
                Guilds = args.Guilds.Keys.Select(x => new GuildDto { Id = x }),
                Users = args.Guilds.Values.SelectMany(x => x.Members)
                    .DistinctBy(x => x.Value.Id)
                    .Select(x =>
                    new UserDto
                    {
                        Id = x.Key,
                        UserName = x.Value.GetUsernameWithDiscriminator(),
                        Nickname = x.Value.Nickname,
                    }),
                Members = args.Guilds.Values.SelectMany(x => x.Members)
                    .Select(x => x.Value).Select(x =>
                    new MemberDto
                    {
                        GuildId = x.Guild.Id,
                        UserId = x.Id,
                        Nickname = x.Nickname
                    }),
                Roles = args.Guilds.Values.Select(x => new { x.Id, x.Roles })
                    .Select(x => x.Roles.Select(y =>
                    new RoleDto
                    {
                        GuildId = x.Id,
                        Id = y.Value.Id
                    }))
                    .SelectMany(x => x),
                Channels = args.Guilds.Values.SelectMany(x => x.Channels)
                    .Select(x =>
                    new ChannelDto
                    {
                        Id = x.Value.Id,
                        GuildId = x.Value.GuildId.GetValueOrDefault()
                    }).Concat(args.Guilds.Values.SelectMany(x => x.Threads)
                        .Select(x =>
                        new ChannelDto
                        {
                            Id = x.Value.Id,
                            GuildId = x.Value.GuildId.GetValueOrDefault()
                        })
                    ),
                Invites = args.Guilds.Values
                    .ToAsyncEnumerable()
                    .SelectManyAwait(async x => (await x.GetInvitesAsync()).ToAsyncEnumerable())
                    .Select(x => 
                    new Invite
                    {
                        Code = x.Code,
                        Inviter = x.Inviter.GetUsernameWithDiscriminator(),
                        Url = x.ToString(),
                        Uses = x.Uses,
                    }).ToEnumerable()
            });


        public async Task DiscordOnGuildCreated(DiscordClient sender, GuildCreateEventArgs args)
            => await this._mediator.Send(new AddGuildCommand
            {
                GuildId = args.Guild.Id,
                Users = args.Guild.Members
                    .Select(x =>
                    new UserDto
                    {
                        Id = x.Key,
                        UserName = x.Value.GetUsernameWithDiscriminator()
                    }),
                Members = args.Guild.Members
                    .Select(x =>
                        new MemberDto
                        {
                            GuildId = x.Value.Guild.Id,
                            UserId = x.Key,
                            Nickname = x.Value.Nickname
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
                        GuildId = args.Guild.Id
                    }),
                Invites = await args.Guild
                    .GetInvitesAsync()
                    .ContinueWith(x => x.Result
                        .Select(x =>
                        new Invite
                        {
                            Code = x.Code,
                            Inviter = x.Inviter.GetUsernameWithDiscriminator(),
                            Url = x.ToString(),
                            Uses = x.Uses,
                        }))
            });



        public Task DiscordOnInviteCreated(DiscordClient sender, InviteCreateEventArgs args)
        {
            this._inviteService.UpdateInvite(
                new Invite
                {
                    Code = args.Invite.Code,
                    Inviter = args.Invite.Inviter.GetUsernameWithDiscriminator(),
                    Url = args.Invite.ToString(),
                    Uses = args.Invite.Uses,
                }) ;
            return Task.CompletedTask;
        }
        public async Task DiscordOnInviteDeleted(DiscordClient sender, InviteDeleteEventArgs args)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            this._inviteService.DeleteInvite(args.Invite.Code);
        }
    }
}
