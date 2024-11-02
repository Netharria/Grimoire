// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Settings;

public sealed class RemoveIgnoreForXpGain
{
    public sealed record Command : IUpdateIgnoreForXpGain
    {
        public required ulong GuildId { get; init; }
        public IReadOnlyCollection<UserDto> Users { get; set; } = [];
        public IReadOnlyCollection<RoleDto> Roles { get; set; } = [];
        public IReadOnlyCollection<ChannelDto> Channels { get; set; } = [];
        public IReadOnlyCollection<string> InvalidIds { get; set; } = [];
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Command, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var newIgnoredItems = new StringBuilder();

            if (command.Users.Count != 0)
            {
                var userIds = command.Users.Select(x => x.Id);
                var allUsersToIgnore = await dbContext.IgnoredMembers
                    .Where(x => x.GuildId == command.GuildId)
                    .Where(x => userIds.Contains(x.UserId))
                    .ToArrayAsync(cancellationToken);
                foreach (var ignorable in allUsersToIgnore)
                    newIgnoredItems.Append(UserExtensions.Mention(ignorable.UserId)).Append(' ');
                if (allUsersToIgnore.Length != 0)
                    dbContext.IgnoredMembers.RemoveRange(allUsersToIgnore);
            }

            if (command.Roles.Any())
            {
                var rolesIds = command.Roles.Select(x => x.Id).ToArray();
                var allRolesToIgnore = await dbContext.IgnoredRoles
                    .Where(x => rolesIds.Contains(x.RoleId))
                    .ToArrayAsync(cancellationToken);
                foreach (var ignorable in allRolesToIgnore)
                    newIgnoredItems.Append(RoleExtensions.Mention(ignorable.RoleId)).Append(' ');
                if (allRolesToIgnore.Length != 0)
                    dbContext.IgnoredRoles.RemoveRange(allRolesToIgnore);
            }

            if (command.Channels.Any())
            {
                var channelIds = command.Channels.Select(x => x.Id).ToArray();
                var allChannelsToIgnore = await dbContext.IgnoredChannels
                    .Where(x => channelIds.Contains(x.ChannelId))
                    .ToArrayAsync(cancellationToken);
                foreach (var ignorable in allChannelsToIgnore)
                    newIgnoredItems.Append(ChannelExtensions.Mention(ignorable.ChannelId)).Append(' ');
                if (allChannelsToIgnore.Length != 0)
                    dbContext.IgnoredChannels.RemoveRange(allChannelsToIgnore);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var couldNotMatch = new StringBuilder();
            if (command.InvalidIds.Any())
                foreach (var id in command.InvalidIds)
                    couldNotMatch.Append(id).Append(' ');

            var finalString = new StringBuilder();
            if (couldNotMatch.Length > 0)
                finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
            if (newIgnoredItems.Length > 0)
                finalString.Append(newIgnoredItems).Append(" are no longer ignored for xp gain.");
            var modChannelLog = await dbContext.Guilds
                .AsNoTracking()
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog)
                .FirstOrDefaultAsync(cancellationToken);
            return new BaseResponse { Message = finalString.ToString(), LogChannelId = modChannelLog };
        }
    }
}
