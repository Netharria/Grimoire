// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Logging.UserLogging;

namespace Grimoire.Features.Shared.Commands;

public sealed record UpdateAllGuildsCommand : IRequest
{
    public IReadOnlyCollection<ulong> Guilds { get; init; } = [];
    public IReadOnlyCollection<UserDto> Users { get; init; } = [];
    public IReadOnlyCollection<MemberDto> Members { get; init; } = [];
    public IReadOnlyCollection<RoleDto> Roles { get; init; } = [];
    public IReadOnlyCollection<ChannelDto> Channels { get; init; } = [];
    public IReadOnlyCollection<Invite> Invites { get; init; } = [];
}

public sealed class UpdateAllGuildsCommandHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory, IInviteService inviteService)
    : IRequestHandler<UpdateAllGuildsCommand>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    private readonly IInviteService _inviteService = inviteService;

    public async Task Handle(UpdateAllGuildsCommand command, CancellationToken cancellationToken)
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
