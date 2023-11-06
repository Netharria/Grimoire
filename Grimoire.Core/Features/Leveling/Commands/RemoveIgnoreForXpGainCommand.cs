// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain;

public sealed class RemoveIgnoreForXpGainCommand : IUpdateIgnoreForXpGain
{
    public ulong GuildId { get; init; }
    public UserDto[] Users { get; set; } = Array.Empty<UserDto>();
    public RoleDto[] Roles { get; set; } = Array.Empty<RoleDto>();
    public ChannelDto[] Channels { get; set; } = Array.Empty<ChannelDto>();
    public string[] InvalidIds { get; set; } = Array.Empty<string>();
}

public class RemoveIgnoreForXpGainCommandHandler : ICommandHandler<RemoveIgnoreForXpGainCommand, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public RemoveIgnoreForXpGainCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<BaseResponse> Handle(RemoveIgnoreForXpGainCommand command, CancellationToken cancellationToken)
    {
        var newIgnoredItems = new StringBuilder();

        if (command.Users.Any())
        {
            var allUsersToIgnore = await this._grimoireDbContext.IgnoredMembers
                .WhereMembersHaveIds(command.Users.Select(x => x.Id).ToArray(), command.GuildId)
                .ToArrayAsync(cancellationToken);
            foreach (var ignorable in allUsersToIgnore)
            {
                newIgnoredItems.Append(UserExtensions.Mention(ignorable.UserId)).Append(' ');
            }
            if(allUsersToIgnore.Any())
                this._grimoireDbContext.IgnoredMembers.RemoveRange(allUsersToIgnore);
        }

        if (command.Roles.Any())
        {
            var rolesIds = command.Roles.Select(x => x.Id).ToArray();
            var allRolesToIgnore = await this._grimoireDbContext.IgnoredRoles
                .Where(x => rolesIds.Contains(x.RoleId))
                .ToArrayAsync(cancellationToken);
            foreach (var ignorable in allRolesToIgnore)
            {
                newIgnoredItems.Append(RoleExtensions.Mention(ignorable.RoleId)).Append(' ');
            }
            if(allRolesToIgnore.Any())
                this._grimoireDbContext.IgnoredRoles.RemoveRange(allRolesToIgnore);
        }

        if (command.Channels.Any())
        {
            var channelIds = command.Channels.Select(x => x.Id).ToArray();
            var allChannelsToIgnore = await this._grimoireDbContext.IgnoredChannels
                .Where(x => channelIds.Contains(x.ChannelId))
                .ToArrayAsync(cancellationToken);
            foreach (var ignorable in allChannelsToIgnore)
            {
                newIgnoredItems.Append(ChannelExtensions.Mention(ignorable.ChannelId)).Append(' ');
            }
            if(allChannelsToIgnore.Any())
                this._grimoireDbContext.IgnoredChannels.RemoveRange(allChannelsToIgnore);
        }

        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        var couldNotMatch = new StringBuilder();
        if (command.InvalidIds.Any())
            foreach (var id in command.InvalidIds)
                couldNotMatch.Append(id).Append(' ');

        var finalString = new StringBuilder();
        if (couldNotMatch.Length > 0) finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
        if (newIgnoredItems.Length > 0) finalString.Append(newIgnoredItems).Append(" are no longer ignored for xp gain.");
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
