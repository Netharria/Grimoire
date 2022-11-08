// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Domain;
using Mediator;
using Cybermancy.Core.Responses;
using Cybermancy.Core.Features.Shared.SharedDtos;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain
{
    public class UpdateIgnoreStateForXpGainCommandHandler : ICommandHandler<UpdateIgnoreStateForXpGainCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UpdateIgnoreStateForXpGainCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<BaseResponse> Handle(UpdateIgnoreStateForXpGainCommand request, CancellationToken cancellationToken)
        {
            await this._cybermancyDbContext.Users.AddMissingUsersAsync(request.Users, cancellationToken);
            await this._cybermancyDbContext.Members.AddMissingMembersAsync(
                request.Users.Select(x =>
                    new MemberDto
                    {
                        UserId = x.Id,
                        GuildId = request.GuildId,
                        Nickname = x.Nickname,
                    }), cancellationToken);
            await this._cybermancyDbContext.Channels.AddMissingChannelsAsync(request.Channels, cancellationToken);
            await this._cybermancyDbContext.Roles.AddMissingRolesAsync(request.Roles, cancellationToken);

            var userIds = request.Users.Select(x => x.Id).ToArray();
            var roleIds = request.Roles.Select(x => x.Id).ToArray();
            var channelIds = request.Channels.Select(x => x.Id).ToArray();

            Member[]? allUsersToIgnore = null;
            Role[]? allRolesToIgnore = null;
            Channel[]? allChannelsToIgnore = null;
            var newIgnoredItems = new StringBuilder();

            if (request.Users.Any())
            {
                allUsersToIgnore = this._cybermancyDbContext.Members
                    .WhereMembersHaveIds(userIds, request.GuildId)
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();
                this._cybermancyDbContext.Members.UpdateRange(allUsersToIgnore);
            }
                
            if (request.Roles.Any())
            {
                allRolesToIgnore = this._cybermancyDbContext.Roles
                    .WhereIdsAre(roleIds)
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();
                this._cybermancyDbContext.Roles.UpdateRange(allRolesToIgnore);
            }

            if (request.Channels.Any())
            {
                allChannelsToIgnore = this._cybermancyDbContext.Channels
                    .WhereIdsAre(channelIds)
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();

                this._cybermancyDbContext.Channels.UpdateRange(allChannelsToIgnore);
            }
            
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            var couldNotMatch = new StringBuilder();
            if (request.InvalidIds.Any())
                foreach (var id in request.InvalidIds)
                    couldNotMatch.Append(id).Append(' ');

            var finalString = new StringBuilder();
            if (couldNotMatch.Length > 0) finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
            if (newIgnoredItems.Length > 0) finalString.Append(newIgnoredItems).Append(request.ShouldIgnore ? " are now ignored for xp gain." : " are now being watched for xp gain.");

            return new BaseResponse { Success = true, Message = finalString.ToString() };
        }


    }
}
