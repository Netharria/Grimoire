// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Logging.UserLogging;

namespace Grimoire.Features.Shared.Events;

internal sealed class UpdateAllGuilds
{
    internal sealed class EventHandler(IMediator mediator) : IEventHandler<GuildDownloadCompletedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, GuildDownloadCompletedEventArgs eventArgs)
        {
            await sender.UpdateStatusAsync(new DiscordActivity($"{sender.Guilds.Count} servers.",
                DiscordActivityType.Watching));
            await this._mediator.Send(new Command
            {
                Guilds = eventArgs.Guilds.Keys.ToArray().AsReadOnly(),
                Users = eventArgs.Guilds.Values.SelectMany(x => x.Members)
                    .DistinctBy(x => x.Value.Id)
                    .Select(x =>
                        new UserDto { Id = x.Key, Username = x.Value.GetUsernameWithDiscriminator() }).ToArray()
                    .AsReadOnly(),
                Members = eventArgs.Guilds.Values.SelectMany(x => x.Members)
                    .Select(x => x.Value).Select(x =>
                        new MemberDto
                        {
                            GuildId = x.Guild.Id,
                            UserId = x.Id,
                            Nickname = x.Nickname,
                            AvatarUrl = x.GetGuildAvatarUrl(MediaFormat.Auto, 128)
                        }).ToArray().AsReadOnly(),
                Roles = eventArgs.Guilds.Values.Select(x => new { x.Id, x.Roles })
                    .Select(x => x.Roles.Select(y =>
                        new RoleDto { GuildId = x.Id, Id = y.Value.Id }))
                    .SelectMany(x => x).ToArray().AsReadOnly(),
                Channels = eventArgs.Guilds.Values.SelectMany(x => x.Channels)
                    .Select(x =>
                        new ChannelDto { Id = x.Value.Id, GuildId = x.Value.GuildId.GetValueOrDefault() }).Concat(
                        eventArgs
                            .Guilds.Values.SelectMany(x => x.Threads)
                            .Select(x =>
                                new ChannelDto { Id = x.Value.Id, GuildId = x.Value.GuildId.GetValueOrDefault() })
                    ).ToArray().AsReadOnly(),
                Invites = await eventArgs.Guilds.Values
                    .ToAsyncEnumerable()
                    .Where(x => x.CurrentMember.Permissions.HasPermission(DiscordPermission.ManageGuild))
                    .SelectManyAwait(async guild =>
                        (await DiscordRetryPolicy.RetryDiscordCall(async _ => await guild.GetInvitesAsync()))
                        .ToAsyncEnumerable())
                    .Select(x =>
                        new Invite
                        {
                            Code = x.Code,
                            Inviter = x.Inviter.GetUsernameWithDiscriminator(),
                            Url = x.ToString(),
                            Uses = x.Uses,
                            MaxUses = x.MaxUses
                        }).ToListAsync()
            });
        }
    }

    public sealed record Command : IRequest
    {
        public IReadOnlyCollection<ulong> Guilds { get; init; } = [];
        public IReadOnlyCollection<UserDto> Users { get; init; } = [];
        public IReadOnlyCollection<MemberDto> Members { get; init; } = [];
        public IReadOnlyCollection<RoleDto> Roles { get; init; } = [];
        public IReadOnlyCollection<ChannelDto> Channels { get; init; } = [];
        public IReadOnlyCollection<Invite> Invites { get; init; } = [];
    }

    public sealed class Handler(
        IDbContextFactory<GrimoireDbContext> dbContextFactory,
        IInviteService inviteService)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        private readonly IInviteService _inviteService = inviteService;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var usersAdded = await dbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);

            var guildsAdded = await dbContext.Guilds.AddMissingGuildsAsync(command.Guilds, cancellationToken);

            var rolesAdded = await dbContext.Roles.AddMissingRolesAsync(command.Roles, cancellationToken);

            var channelsAdded =
                await dbContext.Channels.AddMissingChannelsAsync(command.Channels, cancellationToken);

            var membersAdded =
                await dbContext.Members.AddMissingMembersAsync(command.Members, cancellationToken);

            var usernamesUpdated =
                await dbContext.UsernameHistory.AddMissingUsernameHistoryAsync(command.Users,
                    cancellationToken);

            var nicknamesUpdated =
                await dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(command.Members,
                    cancellationToken);

            var avatarsUpdated =
                await dbContext.Avatars.AddMissingAvatarsHistoryAsync(command.Members, cancellationToken);

            this._inviteService.UpdateAllInvites(command.Guilds.Select(guild =>
                new GuildInviteDto
                {
                    GuildId = guild,
                    Invites = new ConcurrentDictionary<string, Invite>(command.Invites.ToDictionary(x => x.Code))
                }).ToList());

            if (usersAdded || guildsAdded || rolesAdded || channelsAdded || membersAdded || usernamesUpdated ||
                nicknamesUpdated || avatarsUpdated)
                await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
