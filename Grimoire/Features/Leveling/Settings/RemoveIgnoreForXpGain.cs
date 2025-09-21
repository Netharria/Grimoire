// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Leveling.Settings;

public sealed class RemoveIgnoreForXpGain
{
    public sealed record Command : IgnoreCommandGroup.IUpdateIgnoreForXpGain
    {
        public required GuildId GuildId { get; init; }
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
            var ignoreRemovedItems = new IgnoreCommandGroup.Response();

            if (command.Users.Count != 0)
            {
                var userIds = command.Users.Select(x => x.Id);
                var allUsersToRemoveIgnore = await dbContext.IgnoredMembers
                    .Where(x => x.GuildId == command.GuildId)
                    .Where(x => userIds.Contains(x.UserId))
                    .ToArrayAsync(cancellationToken);
                if (allUsersToRemoveIgnore.Length != 0)
                    dbContext.IgnoredMembers.RemoveRange(allUsersToRemoveIgnore);
                ignoreRemovedItems.IgnoredMembers = allUsersToRemoveIgnore;
            }

            if (command.Roles.Count != 0)
            {
                var rolesIds = command.Roles.Select(x => x.Id).ToArray();
                var allRolesToRemoveIgnore = await dbContext.IgnoredRoles
                    .Where(x => rolesIds.Contains(x.RoleId))
                    .ToArrayAsync(cancellationToken);
                if (allRolesToRemoveIgnore.Length != 0)
                    dbContext.IgnoredRoles.RemoveRange(allRolesToRemoveIgnore);
                ignoreRemovedItems.IgnoredRoles = allRolesToRemoveIgnore;
            }

            if (command.Channels.Count != 0)
            {
                var channelIds = command.Channels.Select(x => x.Id).ToArray();
                var allChannelsToRemoveIgnore = await dbContext.IgnoredChannels
                    .Where(x => channelIds.Contains(x.ChannelId))
                    .ToArrayAsync(cancellationToken);
                if (allChannelsToRemoveIgnore.Length != 0)
                    dbContext.IgnoredChannels.RemoveRange(allChannelsToRemoveIgnore);
                ignoreRemovedItems.IgnoredChannels = allChannelsToRemoveIgnore;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return ignoreRemovedItems;
        }
    }
}
