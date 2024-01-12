// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.UserLogging;

namespace Grimoire.Core.Features.Shared.Commands;

public sealed record UpdateAllGuildsCommand : ICommand
{
    public IEnumerable<GuildDto> Guilds { get; init; } = Enumerable.Empty<GuildDto>();
    public IEnumerable<UserDto> Users { get; init; } = Enumerable.Empty<UserDto>();
    public IEnumerable<MemberDto> Members { get; init; } = Enumerable.Empty<MemberDto>();
    public IEnumerable<RoleDto> Roles { get; init; } = Enumerable.Empty<RoleDto>();
    public IEnumerable<ChannelDto> Channels { get; init; } = Enumerable.Empty<ChannelDto>();
    public IEnumerable<Invite> Invites { get; init; } = Enumerable.Empty<Invite>();
}

public sealed class UpdateAllGuildsCommandHandler(IGrimoireDbContext grimoireDbContext, IInviteService inviteService) : ICommandHandler<UpdateAllGuildsCommand>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    private readonly IInviteService _inviteService = inviteService;

    public async ValueTask<Unit> Handle(UpdateAllGuildsCommand command, CancellationToken cancellationToken)
    {
        var usersAdded = await this._grimoireDbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);

        var guildsAdded = await this._grimoireDbContext.Guilds.AddMissingGuildsAsync(command.Guilds, cancellationToken);

        var rolesAdded = await this._grimoireDbContext.Roles.AddMissingRolesAsync(command.Roles, cancellationToken);

        var channelsAdded = await this._grimoireDbContext.Channels.AddMissingChannelsAsync(command.Channels, cancellationToken);

        var membersAdded = await this._grimoireDbContext.Members.AddMissingMembersAsync(command.Members, cancellationToken);

        var usernamesUpdated = await this._grimoireDbContext.UsernameHistory.AddMissingUsernameHistoryAsync(command.Users, cancellationToken);

        var nicknamesUpdated = await this._grimoireDbContext.NicknameHistory.AddMissingNickNameHistoryAsync(command.Members, cancellationToken);

        var avatarsUpdated = await this._grimoireDbContext.Avatars.AddMissingAvatarsHistoryAsync(command.Members, cancellationToken);

        this._inviteService.UpdateAllInvites(command.Guilds.Select(guild =>
                new GuildInviteDto
                {
                    GuildId = guild.Id,
                    Invites = new ConcurrentDictionary<string, Invite>(command.Invites.ToDictionary(x => x.Code))
                }).ToList());

        if (usersAdded || guildsAdded || rolesAdded || channelsAdded || membersAdded || usernamesUpdated || nicknamesUpdated || avatarsUpdated)
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
