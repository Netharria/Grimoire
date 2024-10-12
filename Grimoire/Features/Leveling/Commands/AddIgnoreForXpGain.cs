// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Leveling.Commands;


public interface IUpdateIgnoreForXpGain : ICommand<BaseResponse>
{
    public ulong GuildId { get; init; }
    public UserDto[] Users { get; set; }
    public RoleDto[] Roles { get; set; }
    public ChannelDto[] Channels { get; set; }
    public string[] InvalidIds { get; set; }
}


public sealed class AddIgnoreForXpGain
{
    public sealed record Command : IUpdateIgnoreForXpGain
    {
        public required ulong GuildId { get; init; }
        public UserDto[] Users { get; set; } = [];
        public RoleDto[] Roles { get; set; } = [];
        public ChannelDto[] Channels { get; set; } = [];
        public string[] InvalidIds { get; set; } = [];
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            await this._grimoireDbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);
            await this._grimoireDbContext.Members.AddMissingMembersAsync(
                command.Users.Select(x =>
                    new MemberDto
                    {
                        UserId = x.Id,
                        GuildId = command.GuildId,
                        Nickname = x.Nickname,
                        AvatarUrl = x.AvatarUrl
                    }), cancellationToken);
            await this._grimoireDbContext.Channels.AddMissingChannelsAsync(command.Channels, cancellationToken);
            await this._grimoireDbContext.Roles.AddMissingRolesAsync(command.Roles, cancellationToken);
            var newIgnoredItems = new StringBuilder();

            if (command.Users.Length != 0)
            {
                var allUsersToIgnore = command.Users
                .ExceptBy(this._grimoireDbContext.IgnoredMembers
                .AsNoTracking().Select(x => new { x.UserId, x.GuildId }),
                x => new { UserId = x.Id, command.GuildId })
                .Select(x => new IgnoredMember
                {
                    UserId = x.Id,
                    GuildId = command.GuildId
                }).ToArray();
                foreach (var ignorable in allUsersToIgnore)
                    newIgnoredItems.Append(UserExtensions.Mention(ignorable.UserId)).Append(' ');
                if (allUsersToIgnore.Length != 0)
                    await this._grimoireDbContext.IgnoredMembers.AddRangeAsync(allUsersToIgnore);
            }

            if (command.Roles.Length != 0)
            {
                var allRolesToIgnore = command.Roles
                .ExceptBy(this._grimoireDbContext.IgnoredRoles
                .AsNoTracking().Select(x => x.RoleId),
                x => x.Id)
                .Select(x => new IgnoredRole
                {
                    RoleId = x.Id,
                    GuildId = command.GuildId
                }).ToArray();
                foreach (var ignorable in allRolesToIgnore)
                    newIgnoredItems.Append(RoleExtensions.Mention(ignorable.RoleId)).Append(' ');
                if (allRolesToIgnore.Length != 0)
                    await this._grimoireDbContext.IgnoredRoles.AddRangeAsync(allRolesToIgnore);
            }

            if (command.Channels.Length != 0)
            {
                var allChannelsToIgnore = command.Channels
                .ExceptBy(this._grimoireDbContext.IgnoredChannels
                .AsNoTracking().Select(x => x.ChannelId),
                x => x.Id)
                .Select(x => new IgnoredChannel
                {
                    ChannelId = x.Id,
                    GuildId = command.GuildId
                }).ToArray();
                foreach (var ignorable in allChannelsToIgnore)
                    newIgnoredItems.Append(ChannelExtensions.Mention(ignorable.ChannelId)).Append(' ');
                if (allChannelsToIgnore.Length != 0)
                    await this._grimoireDbContext.IgnoredChannels.AddRangeAsync(allChannelsToIgnore);
            }

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            var couldNotMatch = new StringBuilder();
            if (command.InvalidIds.Length != 0)
                foreach (var id in command.InvalidIds)
                    couldNotMatch.Append(id).Append(' ');

            var finalString = new StringBuilder();
            if (couldNotMatch.Length > 0) finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
            if (newIgnoredItems.Length > 0) finalString.Append(newIgnoredItems).Append(" are now ignored for xp gain.");
            var modChannelLog = await this._grimoireDbContext.Guilds
                .AsNoTracking()
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog)
                .FirstOrDefaultAsync(cancellationToken);
            return new BaseResponse
            {
                Message = finalString.ToString(),
                LogChannelId = modChannelLog
            };
        }
    }
}
