// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Settings;



public sealed class AddIgnoreForXpGain
{
    public sealed record Command : IgnoreCommandGroup.IUpdateIgnoreForXpGain
    {
        public required ulong GuildId { get; init; }
        public IReadOnlyCollection<UserDto> Users { get; set; } = [];
        public IReadOnlyCollection<RoleDto> Roles { get; set; } = [];
        public IReadOnlyCollection<ChannelDto> Channels { get; set; } = [];
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, IgnoreCommandGroup.Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<IgnoreCommandGroup.Response> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);
            await dbContext.Members.AddMissingMembersAsync(
                command.Users.Select(user =>
                    new MemberDto
                    {
                        UserId = user.Id,
                        GuildId = command.GuildId,
                        Nickname = user.Nickname,
                        AvatarUrl = user.AvatarUrl
                    }).ToArray(), cancellationToken);
            await dbContext.Channels.AddMissingChannelsAsync(command.Channels, cancellationToken);
            await dbContext.Roles.AddMissingRolesAsync(command.Roles, cancellationToken);
            var response = new IgnoreCommandGroup.Response();

            if (command.Users.Count != 0)
            {
                var incomingIgnoreUserIds = command.Users.Select(x => x.Id);

                var existingIgnoredUsersIds = await dbContext.IgnoredMembers
                    .AsNoTracking()
                    .Where(x => x.GuildId == command.GuildId)
                    .Where(x => incomingIgnoreUserIds.Contains(x.UserId))
                    .Select(x => x.UserId)
                    .AsAsyncEnumerable()
                    .ToHashSetAsync(cancellationToken);

                var allUsersToIgnore = command.Users
                    .Where(x => !existingIgnoredUsersIds.Contains(x.Id))
                    .Select(x => new IgnoredMember { UserId = x.Id, GuildId = command.GuildId }).ToArray();

                if (allUsersToIgnore.Length != 0)
                    await dbContext.IgnoredMembers.AddRangeAsync(allUsersToIgnore);
                response.IgnoredMembers = allUsersToIgnore;
            }

            if (command.Roles.Count != 0)
            {
                var incomingIgnoreRoleIds = command.Roles.Select(x => x.Id);

                var existingIgnoredRoleIds = await dbContext.IgnoredRoles
                    .AsNoTracking()
                    .Where(x => x.GuildId == command.GuildId)
                    .Where(x => incomingIgnoreRoleIds.Contains(x.RoleId))
                    .Select(x => x.RoleId)
                    .AsAsyncEnumerable()
                    .ToHashSetAsync(cancellationToken);

                var allRolesToIgnore = command.Roles
                    .Where(x => !existingIgnoredRoleIds.Contains(x.Id))
                    .Select(x => new IgnoredRole { RoleId = x.Id, GuildId = command.GuildId }).ToArray();

                if (allRolesToIgnore.Length != 0)
                    await dbContext.IgnoredRoles.AddRangeAsync(allRolesToIgnore);
                response.IgnoredRoles = allRolesToIgnore;
            }

            if (command.Channels.Count != 0)
            {
                var incomingIgnoreChannelIds = command.Channels.Select(x => x.Id);

                var existingIgnoreChannelIds = await dbContext.IgnoredChannels
                    .AsNoTracking()
                    .Where(x => x.GuildId == command.GuildId)
                    .Where(x => incomingIgnoreChannelIds.Contains(x.ChannelId))
                    .Select(x => x.ChannelId)
                    .AsAsyncEnumerable()
                    .ToHashSetAsync(cancellationToken);

                var allChannelsToIgnore = command.Channels
                    .Where(x => !existingIgnoreChannelIds.Contains(x.Id))
                    .Select(x => new IgnoredChannel { ChannelId = x.Id, GuildId = command.GuildId }).ToArray();

                if (allChannelsToIgnore.Length != 0)
                    await dbContext.IgnoredChannels.AddRangeAsync(allChannelsToIgnore);
                response.IgnoredChannels = allChannelsToIgnore;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return response;
        }
    }
}
