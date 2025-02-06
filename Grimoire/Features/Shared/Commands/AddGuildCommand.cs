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

public sealed record AddGuildCommand : IRequest
{
    public ulong GuildId { get; init; }
    public IReadOnlyCollection<UserDto> Users { get; init; } = [];
    public IReadOnlyCollection<MemberDto> Members { get; init; } = [];
    public IReadOnlyCollection<RoleDto> Roles { get; init; } = [];
    public IReadOnlyCollection<ChannelDto> Channels { get; init; } = [];
    public IReadOnlyCollection<Invite> Invites { get; init; } = [];
}

public sealed class AddGuildCommandHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory, IInviteService inviteService)
    : IRequestHandler<AddGuildCommand>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly IInviteService _inviteService = inviteService;

    public async Task Handle(AddGuildCommand command, CancellationToken cancellationToken)
    {
        var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var usersAdded = await dbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);

        var guildExists = await dbContext.Guilds
            .AsNoTracking()
            .AnyAsync(x => x.Id == command.GuildId, cancellationToken);

        if (!guildExists)
            await dbContext.Guilds.AddAsync(
                new Guild
                {
                    Id = command.GuildId,
                    LevelSettings = new GuildLevelSettings(),
                    ModerationSettings = new GuildModerationSettings(),
                    UserLogSettings = new GuildUserLogSettings(),
                    MessageLogSettings = new GuildMessageLogSettings(),
                    CommandsSettings = new GuildCommandsSettings()
                }, cancellationToken);

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

        this._inviteService.UpdateGuildInvites(
            new GuildInviteDto
            {
                GuildId = command.GuildId,
                Invites = new ConcurrentDictionary<string, Invite>(command.Invites.ToDictionary(x => x.Code))
            });

        if (usersAdded || !guildExists || rolesAdded || channelsAdded || membersAdded || usernamesUpdated ||
            nicknamesUpdated || avatarsUpdated)
            await dbContext.SaveChangesAsync(cancellationToken);
    }
}
