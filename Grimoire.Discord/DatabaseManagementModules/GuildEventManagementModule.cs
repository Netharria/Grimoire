// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Shared.Commands.GuildCommands.AddGuild;
using Grimoire.Core.Features.Shared.Commands.GuildCommands.UpdateAllGuilds;
using Grimoire.Domain;
using Serilog;

namespace Grimoire.Discord.DatabaseManagementModules
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
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedManagementModule"/> class.
        /// </summary>
        /// <param name="guildService"></param>
        public GuildEventManagementModule(IMediator mediator, IInviteService inviteService, ILogger logger)
        {
            this._mediator = mediator;
            this._inviteService = inviteService;
            this._logger = logger;
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
                    }),
                Members = args.Guilds.Values.SelectMany(x => x.Members)
                    .Select(x => x.Value).Select(x =>
                    new MemberDto
                    {
                        GuildId = x.Guild.Id,
                        UserId = x.Id,
                        Nickname = x.Nickname,
                        AvatarUrl = x.GetGuildAvatarUrl(ImageFormat.Auto, 128)
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
                        MaxUses = x.MaxUses
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
                            Nickname = x.Value.Nickname,
                            AvatarUrl = x.Value.GetGuildAvatarUrl(ImageFormat.Auto)
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
                            MaxUses = x.MaxUses
                        }))
            });



        public Task DiscordOnInviteCreated(DiscordClient sender, InviteCreateEventArgs args)
        {
            this._inviteService.UpdateInvite(args.Guild.Id,
                new Invite
                {
                    Code = args.Invite.Code,
                    Inviter = args.Invite.Inviter.GetUsernameWithDiscriminator(),
                    Url = args.Invite.ToString(),
                    Uses = args.Invite.Uses,
                    MaxUses = args.Invite.MaxUses
                });
            return Task.CompletedTask;
        }
        public async Task DiscordOnInviteDeleted(DiscordClient sender, InviteDeleteEventArgs args)
        {
            if (args.Invite.ExpiresAt < DateTime.UtcNow)
            {
                if (this._inviteService.DeleteInvite(args.Guild.Id, args.Invite.Code))
                    return;
                else
                    throw new Exception("Was not able to delete expired invite");
            }

            var deletedInviteEntry = await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogInviteEntry>(AuditLogActionType.InviteDelete, 1500);
            if (deletedInviteEntry == null)
            {
                if (args.Invite.MaxUses != args.Invite.Uses + 1)
                    this._logger.Warning("Was not able to retrieve audit log entry for deleted invite.");
                return;
            }
            if (deletedInviteEntry.Target.Code == args.Invite.Code)
                if (!this._inviteService.DeleteInvite(args.Guild.Id, args.Invite.Code))
                    throw new Exception("Was not able to delete expired invite");
        }
    }
}
