// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain
{
    public class UpdateIgnoreStateForXpGainCommandHandler : ICommandHandler<UpdateIgnoreStateForXpGainCommand, BaseResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public UpdateIgnoreStateForXpGainCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<BaseResponse> Handle(UpdateIgnoreStateForXpGainCommand command, CancellationToken cancellationToken)
        {
            await this._grimoireDbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);
            await this._grimoireDbContext.Members.AddMissingMembersAsync(
                command.Users.Select(x =>
                    new MemberDto
                    {
                        UserId = x.Id,
                        GuildId = command.GuildId,
                        Nickname = x.Nickname,
                    }), cancellationToken);
            await this._grimoireDbContext.Channels.AddMissingChannelsAsync(command.Channels, cancellationToken);
            await this._grimoireDbContext.Roles.AddMissingRolesAsync(command.Roles, cancellationToken);

            var userIds = command.Users.Select(x => x.Id).ToArray();
            var roleIds = command.Roles.Select(x => x.Id).ToArray();
            var channelIds = command.Channels.Select(x => x.Id).ToArray();

            Member[]? allUsersToIgnore = null;
            Role[]? allRolesToIgnore = null;
            Channel[]? allChannelsToIgnore = null;
            var newIgnoredItems = new StringBuilder();

            if (command.Users.Any())
            {
                allUsersToIgnore = this._grimoireDbContext.Members
                    .WhereMembersHaveIds(userIds, command.GuildId)
                    .UpdateIgnoredStatus(command.ShouldIgnore, newIgnoredItems)
                    .ToArray();
                this._grimoireDbContext.Members.UpdateRange(allUsersToIgnore);
            }
                
            if (command.Roles.Any())
            {
                allRolesToIgnore = this._grimoireDbContext.Roles
                    .WhereIdsAre(roleIds)
                    .UpdateIgnoredStatus(command.ShouldIgnore, newIgnoredItems)
                    .ToArray();
                this._grimoireDbContext.Roles.UpdateRange(allRolesToIgnore);
            }

            if (command.Channels.Any())
            {
                allChannelsToIgnore = this._grimoireDbContext.Channels
                    .WhereIdsAre(channelIds)
                    .UpdateIgnoredStatus(command.ShouldIgnore, newIgnoredItems)
                    .ToArray();

                this._grimoireDbContext.Channels.UpdateRange(allChannelsToIgnore);
            }
            
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            var couldNotMatch = new StringBuilder();
            if (command.InvalidIds.Any())
                foreach (var id in command.InvalidIds)
                    couldNotMatch.Append(id).Append(' ');

            var finalString = new StringBuilder();
            if (couldNotMatch.Length > 0) finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
            if (newIgnoredItems.Length > 0) finalString.Append(newIgnoredItems).Append(command.ShouldIgnore ? " are now ignored for xp gain." : " are now being watched for xp gain.");

            return new BaseResponse { Message = finalString.ToString() };
        }


    }
}
