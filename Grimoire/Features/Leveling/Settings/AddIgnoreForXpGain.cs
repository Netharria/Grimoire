// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Settings;

public interface IUpdateIgnoreForXpGain : IRequest<BaseResponse>
{
    public ulong GuildId { get; init; }
    public IReadOnlyCollection<UserDto> Users { get; set; }
    public IReadOnlyCollection<RoleDto> Roles { get; set; }
    public IReadOnlyCollection<ChannelDto> Channels { get; set; }
    public IReadOnlyCollection<string> InvalidIds { get; set; }
}

public sealed class AddIgnoreForXpGain
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
            var newIgnoredItems = new StringBuilder();

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
                foreach (var ignorable in allUsersToIgnore)
                    newIgnoredItems.Append(UserExtensions.Mention(ignorable.UserId)).Append(' ');
                if (allUsersToIgnore.Length != 0)
                    await dbContext.IgnoredMembers.AddRangeAsync(allUsersToIgnore);
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

                foreach (var ignorable in allRolesToIgnore)
                    newIgnoredItems.Append(RoleExtensions.Mention(ignorable.RoleId)).Append(' ');

                if (allRolesToIgnore.Length != 0)
                    await dbContext.IgnoredRoles.AddRangeAsync(allRolesToIgnore);
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
                foreach (var ignorable in allChannelsToIgnore)
                    newIgnoredItems.Append(ChannelExtensions.Mention(ignorable.ChannelId)).Append(' ');
                if (allChannelsToIgnore.Length != 0)
                    await dbContext.IgnoredChannels.AddRangeAsync(allChannelsToIgnore);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var couldNotMatch = new StringBuilder();
            if (command.InvalidIds.Count != 0)
                foreach (var id in command.InvalidIds)
                    couldNotMatch.Append(id).Append(' ');

            var finalString = new StringBuilder();
            if (couldNotMatch.Length > 0)
                finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
            if (newIgnoredItems.Length > 0) finalString.Append(newIgnoredItems).Append(" are now ignored for xp gain.");
            var modChannelLog = await dbContext.Guilds
                .AsNoTracking()
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog)
                .FirstOrDefaultAsync(cancellationToken);
            return new BaseResponse { Message = finalString.ToString(), LogChannelId = modChannelLog };
        }
    }
}
